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

    public DbSet<Fabric> Fabrics { get; set; }

    public DbSet<Color> Colors { get; set; }

    public DbSet<ProductFabric> ProductFabrics { get; set; }

    public DbSet<ProductFabricColor> ProductFabricColors { get; set; }

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

        modelBuilder.Entity<ProductFabric>()
            .HasOne(pf => pf.Product)
            .WithMany(p => p.Fabrics)
            .HasForeignKey(pf => pf.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductFabric>()
            .HasOne(pf => pf.Fabric)
            .WithMany(f => f.ProductFabrics)
            .HasForeignKey(pf => pf.FabricId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductFabricColor>()
            .HasOne(pfc => pfc.ProductFabric)
            .WithMany(pf => pf.Colors)
            .HasForeignKey(pfc => pfc.ProductFabricId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductFabricColor>()
            .HasOne(pfc => pfc.Color)
            .WithMany(c => c.ProductFabricColors)
            .HasForeignKey(pfc => pfc.ColorId)
            .OnDelete(DeleteBehavior.Restrict);

            // Un producto no puede tener la misma tela dos veces
        modelBuilder.Entity<ProductFabric>()
            .HasIndex(pf => new { pf.ProductId, pf.FabricId })
            .IsUnique();

        // Solo una tela puede ser default por producto
        modelBuilder.Entity<ProductFabric>()
            .HasIndex(pf => pf.ProductId)
            .IsUnique()
            .HasFilter("\"IsDefault\" = true");

        // Una tela del producto no puede tener el mismo color dos veces
        modelBuilder.Entity<ProductFabricColor>()
            .HasIndex(pfc => new
            {
                pfc.ProductFabricId,
                pfc.ColorId
            })
            .IsUnique();

        // Solo un color puede ser default dentro de cada tela
        modelBuilder.Entity<ProductFabricColor>()
            .HasIndex(pfc => pfc.ProductFabricId)
            .IsUnique()
            .HasFilter("\"IsDefault\" = true");
    }
}