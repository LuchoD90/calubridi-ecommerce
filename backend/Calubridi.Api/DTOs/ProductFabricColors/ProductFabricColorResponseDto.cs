namespace Calubridi.Api.DTOs.ProductFabricColors;

public class ProductFabricColorResponseDto
{
    public int Id { get; set; }

    public int ProductFabricId { get; set; }

    public int ColorId { get; set; }

    public string ColorName { get; set; } = string.Empty;

    public string HexCode { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; }
}