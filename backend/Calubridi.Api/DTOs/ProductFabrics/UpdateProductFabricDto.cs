namespace Calubridi.Api.DTOs.ProductFabrics;

public class UpdateProductFabricDto
{
    public decimal Price { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; }
}