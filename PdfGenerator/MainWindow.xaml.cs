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
    private string[][] _data;

    public MainWindow()
    {
        InitializeComponent();
        RadioRed.IsChecked = true;
    }

    private void ButtonChoose_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = "CSV files (*.csv)|*.csv";
        if (openFileDialog.ShowDialog() == true)
        {
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
            int groupSize = 3;
            var result = new List<string[]>();

            for (int i = 0; i < data.GetLength(0); i += groupSize)
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
    }

    private void ButtonGenerate_OnClick(object sender, RoutedEventArgs e)
    {
        if (_fileName == null)
        {
            MessageBox.Show("Choose a file to generate PDF");
            return;
        }

        string outputPath = "";
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf"
        };
        if (saveFileDialog.ShowDialog() == true)
        {
            outputPath = saveFileDialog.FileName;
        }

        string templateName = RadioRed.IsChecked == true ? "templateRed.pdf" : "templateYellow.pdf";
        var templateUri = new Uri($"pack://application:,,,/templates/{templateName}");
        using var templateStream = Application.GetResourceStream(templateUri)?.Stream;
        using var outputDoc = new PdfDocument();
        using var templateDoc = PdfReader.Open(templateStream, PdfDocumentOpenMode.Import);
        var templatePage = templateDoc.Pages[0];

        const int cardsPerPage = 21;
        const int xSpacing = 181;
        const int ySpacing = 108;

        const int titleXPadding = 10;
        const int titleYPadding = 20;
        const int titleWidth = 160;
        const int titleHeight = 35;
        const int subtitleXPadding = 20;
        const int subtitleYPadding = 71;
        const int subtitleWidth = 140;
        const int subtitleHeight = 31;

        int totalCards = _data.GetLength(0);
        int pageAmount = (int)Math.Ceiling(totalCards / (double)cardsPerPage);
        int dataIndex = 0;

        for (int i = 0; i < pageAmount; i++)
        {
            var page = outputDoc.AddPage(templatePage);
            using var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Arial", 12, XFontStyle.Bold);

            for (int row = 0; row < 7 && dataIndex < totalCards; row++)
            {
                for (int col = 0; col < 3 && dataIndex < totalCards; col++)
                {
                    int x = col * xSpacing;
                    int y = row * ySpacing;

                    var textRect = new XRect(x + titleXPadding, y + titleYPadding, titleWidth, titleHeight);
                    var textRect2 = new XRect(x + subtitleXPadding, y + subtitleYPadding, subtitleWidth,
                        subtitleHeight);

                    DrawWrappedCenteredText(gfx, _data[dataIndex][0].Replace("\"", ""), font, XBrushes.Black, textRect);
                    DrawWrappedCenteredText(gfx, _data[dataIndex][1].Replace("\"", ""), font, XBrushes.Black, textRect2);

                    dataIndex++;
                }
            }
        }

        outputDoc.Save(outputPath);
    }
    void DrawWrappedCenteredText(XGraphics gfx, string text, XFont font, XBrush brush, XRect rect,
        double lineSpacing = 1.2)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        string currentLine = "";
        double maxWidth = rect.Width;

        foreach (var word in words)
        {
            string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
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

        double lineHeight = font.GetHeight() * lineSpacing;
        double totalTextHeight = lineHeight * lines.Count;

        double y = rect.Top + (rect.Height - totalTextHeight) / 2;

        foreach (var line in lines)
        {
            var size = gfx.MeasureString(line, font);

            double x = rect.Left + (rect.Width - size.Width) / 2;

            gfx.DrawString(line, font, brush, new XPoint(x, y + font.Size));
            y += lineHeight;
        }
    }
}