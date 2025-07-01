using System.IO;
using System.Windows;
using Microsoft.Win32;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfGenerator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private string? _fileName;
    private string[][]? _data;

    public MainWindow()
    {
        InitializeComponent();
        RadioRed.IsChecked = true;
    }

    private void ButtonChoose_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv"
        };
        if (openFileDialog.ShowDialog() != true) return;
        _fileName = openFileDialog.FileName;
        FilePathTextBlock.Text = openFileDialog.SafeFileName;

        var file = File.ReadAllLines(_fileName);
        var headers = file[0].Split(';');
        var desiredColumns = new[] { "name", "tickets" };
        var columnIndexes = desiredColumns
            .Select(col => Array.IndexOf(headers, col))
            .ToArray();
        var data = file.Skip(1)
            .Select(line => line.Split(';'))
            .Select(columns => columnIndexes.Select(index => columns[index]).ToArray())
            .ToArray();
        const int groupSize = 3;
        var result = new List<string[]>();

        for (var i = 0; i < data.GetLength(0); i += groupSize)
        {
            var chunk = data.Skip(i).Take(groupSize).ToList();
            while (chunk.Count < groupSize)
            {
                chunk.Add(Enumerable.Repeat(string.Empty, desiredColumns.Length).ToArray());
            }

            result.AddRange(chunk);
            result.AddRange(chunk);
        }

        _data = result.ToArray();
    }

    private void ButtonGenerate_OnClick(object sender, RoutedEventArgs e)
    {
        if (_fileName == null)
        {
            MessageBox.Show("Choose a file to generate PDF");
            return;
        }

        var outputPath = "";
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf"
        };
        if (saveFileDialog.ShowDialog() == true)
        {
            outputPath = saveFileDialog.FileName;
        }

        var templateName = RadioRed.IsChecked == true ? "templateRed.pdf" : "templateYellow.pdf";
        var templateUri = new Uri($"pack://application:,,,/templates/{templateName}");
        using var templateStream = Application.GetResourceStream(templateUri)?.Stream;
        using var outputDoc = new PdfDocument();
        using var templateDoc = PdfReader.Open(templateStream, PdfDocumentOpenMode.Import);
        var templatePage = templateDoc.Pages[0];

        const int noOfRows = 6;
        const int noOfColumns = 3;
        const int cardsPerPage = 18;
        const int xSpacing = 181;
        const double ySpacing = 107.75;

        const int fontSizeTitle = 12;
        const int fontSizeSubtitle = 18;

        const int titleXPadding = 10;
        const int titleYPadding = 20;
        const int titleWidth = 160;
        const int titleHeight = 35;
        const int subtitleXPadding = 20;
        var subtitleYPadding = RadioRed.IsChecked == true ? 73 : 77;
        const int subtitleWidth = 140;
        const int subtitleHeight = 31;

        if (_data != null)
        {
            var totalCards = _data.GetLength(0);
            var pageAmount = (int)Math.Ceiling(totalCards / (double)cardsPerPage);
            var dataIndex = 0;

            for (var i = 0; i < pageAmount; i++)
            {
                var page = outputDoc.AddPage(templatePage);
                using var gfx = XGraphics.FromPdfPage(page);
                var fontTitle = new XFont("Arial", fontSizeTitle, XFontStyle.Bold);
                var fontSubtitle = new XFont("Arial", fontSizeSubtitle, XFontStyle.Bold);

                for (var row = 0; row < noOfRows && dataIndex < totalCards; row++)
                {
                    for (var col = 0; col < noOfColumns && dataIndex < totalCards; col++)
                    {
                        var x = col * xSpacing;
                        var y = (int)(row * ySpacing);

                        var textRect = new XRect(x + titleXPadding, y + titleYPadding, titleWidth, titleHeight);
                        var textRect2 = new XRect(x + subtitleXPadding, y + subtitleYPadding, subtitleWidth,
                            subtitleHeight);

                        DrawWrappedCenteredText(gfx, _data[dataIndex][0].Replace("\"", ""), fontTitle, XBrushes.Black,
                            textRect);
                        DrawWrappedCenteredText(gfx, _data[dataIndex][1].Replace("\"", ""), fontSubtitle, XBrushes.Black,
                            textRect2);

                        dataIndex++;
                    }
                }
            }
        }

        outputDoc.Save(outputPath);
    }

    private static void DrawWrappedCenteredText(XGraphics gfx, string text, XFont font, XBrush brush, XRect rect,
        double lineSpacing = 1.2)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        var currentLine = "";
        var maxWidth = rect.Width;

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            var size = gfx.MeasureString(testLine, font);

            if (size.Width > maxWidth)
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentLine))
            lines.Add(currentLine);

        var lineHeight = font.GetHeight() * lineSpacing;
        var totalTextHeight = lineHeight * lines.Count;

        var y = rect.Top + (rect.Height - totalTextHeight) / 2;

        foreach (var line in lines)
        {
            var size = gfx.MeasureString(line, font);

            var x = rect.Left + (rect.Width - size.Width) / 2;

            gfx.DrawString(line, font, brush, new XPoint(x, y + font.Size));
            y += lineHeight;
        }
    }
}