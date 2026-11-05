using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace InvoiceAI.Api.Services;

public static class PdfTextExtractor
{
    public static string Extract(Stream pdfStream)
    {
        using var document = PdfDocument.Open(pdfStream);
        var pages = document.GetPages().Select(page => ContentOrderTextExtractor.GetText(page));
        return string.Join("\n\n", pages);
    }
}