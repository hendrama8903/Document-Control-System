using System.Drawing;
using System.Drawing.Printing;

public class PrintingService
{
    public void Print(string filePath)
    {
        string defaultPrinter = GetDefaultPrinter();
        PrintDocument printDocument = new PrintDocument
        {
            PrinterSettings = { PrinterName = defaultPrinter },
            DocumentName = filePath
        };
        printDocument.PrintPage += PrintPage;

        // Start printing
        printDocument.Print();
    }

    private string GetDefaultPrinter()
    {
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            var settings = new PrinterSettings();
            settings.PrinterName = printer;
            if (settings.IsDefaultPrinter)
                return printer;
        }

        throw new InvalidOperationException("Default printer not found.");
    }

    private void PrintPage(object sender, PrintPageEventArgs e)
    {
        // Load the image from the file and print it
        var image = Image.FromFile(((PrintDocument)sender).DocumentName);
        e.Graphics.DrawImage(image, e.MarginBounds);
        e.HasMorePages = false;  // Set to false since we're printing a single page
    }
}
