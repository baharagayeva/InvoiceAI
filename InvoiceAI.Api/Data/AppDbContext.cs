using Microsoft.EntityFrameworkCore;

namespace InvoiceAI.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder b)
    {
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
    }
}