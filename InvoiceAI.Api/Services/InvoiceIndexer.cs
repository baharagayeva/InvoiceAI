using InvoiceAI.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Pgvector;

namespace InvoiceAI.Api.Services;

public class InvoiceIndexer(
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    AppDbContext db)
{
    public async Task<int> IndexAsync(Invoice invoice, CancellationToken ct)
    {
        var header =
            $"Invoice {invoice.InvoiceNumber}. " +
            $"Seller: {invoice.SellerName} (VÖEN {invoice.SellerTaxId}). " +
            $"Buyer: {invoice.BuyerName} (VÖEN {invoice.BuyerTaxId}).";

        var inputs = TextChunker.Split(invoice.RawText)
            .Select(part => $"{header}\n{part}")
            .ToList();

        // Bütün hissələr bir sorğuda göndərilir: daha sürətli və ucuz
        var embeddings = await embedder.GenerateAsync(inputs, cancellationToken: ct);

        // Yenidən indeksləyəndə köhnə hissələri silirik
        await db.InvoiceChunks
            .Where(c => c.InvoiceId == invoice.Id)
            .ExecuteDeleteAsync(ct);

        for (var i = 0; i < inputs.Count; i++)
        {
            db.InvoiceChunks.Add(new InvoiceChunk
            {
                InvoiceId = invoice.Id,
                Index = i,
                Content = inputs[i],
                Embedding = new Vector(embeddings[i].Vector)
            });
        }

        await db.SaveChangesAsync(ct);
        return inputs.Count;
    }
}