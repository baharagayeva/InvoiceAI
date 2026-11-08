using System.ComponentModel;
using InvoiceAI.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace InvoiceAI.Api.Services;

public record InvoiceRow(
    string? InvoiceNumber, string? Seller, string? Buyer,
    decimal? Total, string? Currency, DateOnly? IssueDate, DateOnly? DueDate,
    bool IsOverdue, int? DaysOverdue);

public record BuyerExposure(
    string? BuyerTaxId, string? BuyerName, int InvoiceCount,
    decimal TotalAmount, decimal OverdueAmount);

public record DocumentHit(string? InvoiceNumber, string Content);

// Agentin istifadə etdiyi read-only alətlər. Heç biri datanı dəyişmir.
public class InvoiceTools(
    AppDbContext db,
    IEmbeddingGenerator<string, Embedding<float>> embedder)
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Description("Find invoices by filters. Use for questions about dates, due dates, overdue invoices, amounts and companies.")]
    public async Task<List<InvoiceRow>> FindInvoices(
        [Description("Part of seller or buyer company name")] string? companyName = null,
        [Description("Only invoices whose due date has passed")] bool overdueOnly = false,
        [Description("Due date from (inclusive)")] DateOnly? dueFrom = null,
        [Description("Due date to (inclusive)")] DateOnly? dueTo = null,
        [Description("Minimum total amount")] decimal? minAmount = null,
        CancellationToken ct = default)
    {
        var today = Today;
        var q = db.Invoices.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(companyName))
            q = q.Where(i =>
                (i.SellerName != null && EF.Functions.ILike(i.SellerName, $"%{companyName}%")) ||
                (i.BuyerName != null && EF.Functions.ILike(i.BuyerName, $"%{companyName}%")));

        if (overdueOnly) q = q.Where(i => i.DueDate < today);
        if (dueFrom is not null) q = q.Where(i => i.DueDate >= dueFrom);
        if (dueTo is not null) q = q.Where(i => i.DueDate <= dueTo);
        if (minAmount is not null) q = q.Where(i => i.TotalAmount >= minAmount);

        var rows = await q.OrderBy(i => i.DueDate).Take(50).ToListAsync(ct);

        return rows.Select(i =>
        {
            var overdue = i.DueDate is { } d && d < today;
            return new InvoiceRow(
                i.InvoiceNumber, i.SellerName, i.BuyerName, i.TotalAmount, i.Currency,
                i.IssueDate, i.DueDate, overdue,
                overdue ? today.DayNumber - i.DueDate!.Value.DayNumber : null);
        }).ToList();
    }

    [Description("Total amount owed by each buyer (debtor), including overdue amount. Use for risk and exposure questions.")]
    public async Task<List<BuyerExposure>> GetBuyerExposure(CancellationToken ct = default)
    {
        var today = Today;

        var rows = await db.Invoices.AsNoTracking()
            .GroupBy(i => i.BuyerTaxId)
            .Select(g => new
            {
                BuyerTaxId = g.Key,
                BuyerName = g.Max(i => i.BuyerName),
                InvoiceCount = g.Count(),
                TotalAmount = g.Sum(i => i.TotalAmount ?? 0),
                OverdueAmount = g.Sum(i => i.DueDate < today ? (i.TotalAmount ?? 0) : 0)
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToListAsync(ct);

        return rows.Select(x => new BuyerExposure(
            x.BuyerTaxId, x.BuyerName, x.InvoiceCount, x.TotalAmount, x.OverdueAmount)).ToList();
    }

    [Description("Semantic search in invoice texts. Use for contract terms: penalties, interest, payment conditions, notes. Write the query in English.")]
    public async Task<List<DocumentHit>> SearchDocuments(
        [Description("Short search query in English")] string query,
        [Description("Optional: search only in these invoice numbers")] string[]? invoiceNumbers = null,
        CancellationToken ct = default)
    {
        var qv = new Vector(await embedder.GenerateVectorAsync(query, cancellationToken: ct));

        var chunks = db.InvoiceChunks.AsNoTracking();
        if (invoiceNumbers is { Length: > 0 })
            chunks = chunks.Where(c => invoiceNumbers.Contains(c.Invoice.InvoiceNumber));

        return await chunks
            .OrderBy(c => c.Embedding.CosineDistance(qv))
            .Take(5)
            .Select(c => new DocumentHit(c.Invoice.InvoiceNumber, c.Content))
            .ToListAsync(ct);
    }
}