using Pgvector;

namespace InvoiceAI.Api.Data;

public class InvoiceChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public int Index { get; set; }
    public string Content { get; set; } = "";
    public Vector Embedding { get; set; } = null!;
}