using System.ComponentModel;

namespace InvoiceAI.Api.Models;

public record Party(
    string Name,
    [property: Description("Tax ID (VÖEN / TIN) if present")] string? TaxId);

public record ExtractedInvoice(
    string? InvoiceNumber,
    Party? Seller,
    Party? Buyer,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    [property: Description("True if due date was not written but calculated from payment terms")]
    bool DueDateCalculated,
    decimal? Subtotal,
    decimal? VatAmount,
    decimal? TotalAmount,
    [property: Description("ISO 4217 code, e.g. AZN, USD, EUR")] string? Currency,
    string? PaymentTerms,
    string? LatePenaltyTerms);