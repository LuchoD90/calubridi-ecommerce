namespace Calubridi.Api.Models;

public class ProductMedia
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string MediaType { get; set; } = string.Empty;

    public bool IsPrimary { get; set; } = false;

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
}