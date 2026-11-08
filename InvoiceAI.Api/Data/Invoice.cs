using InvoiceAI.Api.Services;

namespace InvoiceAI.Api.Data;

public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "";
    public string RawText { get; set; } = "";

    public string? InvoiceNumber { get; set; }
    public string? SellerName { get; set; }
    public string? SellerTaxId { get; set; }
    public string? BuyerName { get; set; }
    public string? BuyerTaxId { get; set; }

    public DateOnly? IssueDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool DueDateCalculated { get; set; }

    public decimal? Subtotal { get; set; }
    public decimal? VatAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? Currency { get; set; }

    public string? PaymentTerms { get; set; }
    public string? LatePenaltyTerms { get; set; }

    public string Status { get; set; } = InvoiceStatus.Processed;
    public List<string> Warnings { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public List<InvoiceChunk> Chunks { get; set; } = [];

    public static Invoice From(string fileName, string rawText, ExtractionResult result)
    {
        var x = result.Invoice!;
        return new Invoice
        {
            FileName = fileName,
            RawText = rawText,
            InvoiceNumber = x.InvoiceNumber,
            SellerName = x.Seller?.Name,
            SellerTaxId = x.Seller?.TaxId,
            BuyerName = x.Buyer?.Name,
            BuyerTaxId = x.Buyer?.TaxId,
            IssueDate = x.IssueDate,
            DueDate = x.DueDate,
            DueDateCalculated = x.DueDateCalculated,
            Subtotal = x.Subtotal,
            VatAmount = x.VatAmount,
            TotalAmount = x.TotalAmount,
            Currency = x.Currency,
            PaymentTerms = x.PaymentTerms,
            LatePenaltyTerms = x.LatePenaltyTerms,
            Warnings = result.Warnings,
            Status = result.Warnings.Count == 0 ? InvoiceStatus.Processed : InvoiceStatus.NeedsReview
        };
    }
}

public static class InvoiceStatus
{
    public const string Processed = "Processed";
    public const string NeedsReview = "NeedsReview";
}