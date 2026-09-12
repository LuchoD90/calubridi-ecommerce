namespace Calubridi.Api.Models;

public class ProductFabric
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int FabricId { get; set; }

    public decimal Price { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public Product? Product { get; set; }

    public Fabric? Fabric { get; set; }

    public ICollection<ProductFabricColor> Colors { get; set; }
        = new List<ProductFabricColor>();
}