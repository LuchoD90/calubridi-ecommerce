namespace Calubridi.Api.DTOs.ProductMedia;

public class ProductMediaResponseDto
{
    public int Id { get; set; }

    public int ProductFabricColorId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string MediaType { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }
}