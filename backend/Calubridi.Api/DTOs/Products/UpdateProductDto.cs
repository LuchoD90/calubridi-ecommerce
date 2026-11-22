using System.ComponentModel.DataAnnotations;

namespace Calubridi.Api.DTOs.Products;

public class UpdateProductDto
{
    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2500)]
    public string Description { get; set; } = string.Empty;

    [Range(1, 999999999)]
    public decimal Price { get; set; }

    [Range(1, 365)]
    public int EstimatedProductionDays { get; set; }

    public bool IsActive { get; set; }

    public int? CategoryId { get; set; }
}