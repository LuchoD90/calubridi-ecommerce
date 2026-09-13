namespace Calubridi.Api.Models;

public class ProductFabricColor
{
    public int Id { get; set; }

    public int ProductFabricId { get; set; }

    public int ColorId { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; }

    public ProductFabric? ProductFabric { get; set; }

    public Color? Color { get; set; }

    public ICollection<ProductMedia> Media { get; set; }
        = new List<ProductMedia>();
}