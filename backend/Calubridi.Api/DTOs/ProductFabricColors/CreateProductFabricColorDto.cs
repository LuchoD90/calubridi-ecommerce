namespace Calubridi.Api.DTOs.ProductFabricColors;

public class CreateProductFabricColorDto
{
    public int ColorId { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
}