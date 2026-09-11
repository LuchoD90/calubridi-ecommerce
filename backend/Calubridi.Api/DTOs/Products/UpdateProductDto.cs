namespace Calubridi.Api.DTOs.Products;

public class UpdateProductDto
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int EstimatedProductionDays { get; set; }

    public bool IsActive { get; set; }

    public int? CategoryId { get; set; }
}