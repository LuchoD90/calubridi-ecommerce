using Calubridi.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Calubridi.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }

    public DbSet<Category> Categories { get; set; }

    public DbSet<ProductMedia> ProductMedia { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProductMedia>()
            .HasOne(pm => pm.Product)
            .WithMany(p => p.Media)
            .HasForeignKey(pm => pm.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}