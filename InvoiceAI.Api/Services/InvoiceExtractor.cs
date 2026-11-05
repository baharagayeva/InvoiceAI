using System.Diagnostics;
using InvoiceAI.Api.Models;
using Microsoft.Extensions.AI;

namespace InvoiceAI.Api.Services;

public record ExtractionResult(
    ExtractedInvoice? Invoice,
    List<string> Warnings,
    long? InputTokens,
    long? OutputTokens,
    long ElapsedMs);

public class InvoiceExtractor(IChatClient ai)
{
    private const string SystemPrompt = """
        You extract structured data from invoices for a factoring company.
        Rules:
        - Use only information present in the document. Never guess.
        - If a field is missing, return null.
        - Convert all dates to ISO format (YYYY-MM-DD).
        - Amounts: return plain numbers. "33 984,00" and "33,984.00" both mean 33984.00.
        - If the due date is not written but payment terms say "within N days",
          calculate it from the issue date and set dueDateCalculated to true.
        - Documents may be in English or Azerbaijani.
        """;

    public async Task<ExtractionResult> ExtractAsync(string documentText, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        List<ChatMessage> messages =
        [
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, $"Invoice text:\n\n{documentText}")
        ];

        var response = await ai.GetResponseAsync<ExtractedInvoice>(
            messages, cancellationToken: ct);

        sw.Stop();

        var warnings = new List<string>();
        if (!response.TryGetResult(out var invoice))
        {
            warnings.Add("Model did not return valid JSON.");
        }
        else
        {
            warnings.AddRange(Validate(invoice));
        }

        return new ExtractionResult(
            invoice, warnings,
            response.Usage?.InputTokenCount,
            response.Usage?.OutputTokenCount,
            sw.ElapsedMilliseconds);
    }

    private static IEnumerable<string> Validate(ExtractedInvoice inv)
    {
        if (inv.TotalAmount is null) yield return "Total amount is missing.";
        if (inv.TotalAmount <= 0) yield return "Total amount must be positive.";

        if (inv.Subtotal is { } sub && inv.VatAmount is { } vat && inv.TotalAmount is { } total
            && Math.Abs(sub + vat - total) > 0.01m)
            yield return $"Subtotal + VAT ({sub + vat}) does not match total ({total}).";

        if (inv.IssueDate is { } issue && inv.DueDate is { } due && due < issue)
            yield return "Due date is before issue date.";

        if (inv.DueDate is null) yield return "Due date is missing.";
    }
}