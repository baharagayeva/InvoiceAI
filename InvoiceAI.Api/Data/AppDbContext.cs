using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceChunk> InvoiceChunks => Set<InvoiceChunk>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("vector");

        b.Entity<Invoice>(e =>
        {
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.VatAmount).HasPrecision(18, 2);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasIndex(x => x.SellerTaxId);
            e.HasIndex(x => x.BuyerTaxId);
            e.HasIndex(x => x.DueDate);
        });

        b.Entity<InvoiceChunk>(e =>
        {
            e.Property(x => x.Embedding).HasColumnType("vector(1536)");
            e.HasIndex(x => x.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
            e.HasOne(x => x.Invoice)
                .WithMany(i => i.Chunks)
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}