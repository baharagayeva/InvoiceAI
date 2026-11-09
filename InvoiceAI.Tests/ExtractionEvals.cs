using InvoiceAI.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceAI.Tests;

[Trait("Category", "Eval")]
public class ExtractionEvals(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    public static TheoryData<string, string, decimal, string, bool> Cases => new()
    {
        { "invoice_01_en.pdf",     "INV-2026-0451", 47691.00m, "2026-10-31", false },
        { "invoice_02_az.pdf",     "AQ-0917",       33984.00m, "2027-01-14", false },
        { "invoice_03_tricky.pdf", "NTS/26/1183",   11847.20m, "2026-09-17", true  },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Extracts_key_fields(
        string file, string number, decimal total, string dueDate, bool dueDateCalculated)
    {
        using var scope = factory.Services.CreateScope();
        var extractor = scope.ServiceProvider.GetRequiredService<InvoiceExtractor>();

        await using var stream = File.OpenRead(Path.Combine("TestData", file));
        var text = PdfTextExtractor.Extract(stream);

        var result = await extractor.ExtractAsync(text, CancellationToken.None);

        Assert.NotNull(result.Invoice);
        Assert.Empty(result.Warnings);
        Assert.Equal(number, result.Invoice!.InvoiceNumber);
        Assert.Equal(total, result.Invoice.TotalAmount);
        Assert.Equal(DateOnly.Parse(dueDate), result.Invoice.DueDate);
        Assert.Equal(dueDateCalculated, result.Invoice.DueDateCalculated);
    }
}