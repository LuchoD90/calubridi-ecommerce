namespace Calubridi.Api.DTOs.ProductFabrics;

public class ProductFabricResponseDto
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int FabricId { get; set; }

    public string FabricName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; }
}