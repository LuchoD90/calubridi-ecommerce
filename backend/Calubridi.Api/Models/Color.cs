namespace Calubridi.Api.Models;

public class Color
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string HexCode { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProductFabricColor> ProductFabricColors { get; set; }
        = new List<ProductFabricColor>();
}