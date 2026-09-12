namespace Calubridi.Api.DTOs.ProductFabrics;

public class CreateProductFabricDto
{
    public int FabricId { get; set; }

    public decimal Price { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
}