using Microsoft.AspNetCore.Identity;

namespace Calubridi.Api.Store;

public sealed class StoreUser : IdentityUser { }

public sealed class Favorite
{
    public string UserId { get; set; } = "";
    public int ProductId { get; set; }
}

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public Guid CheckoutKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Customer { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Delivery { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Currency { get; set; } = "";
    public decimal Total { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime? PaidAt { get; set; }
    public int Revision { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
}

public sealed class OrderLine
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Fabric { get; set; } = "";
    public string Color { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int ProductionDays { get; set; }
}

public sealed class SupplierPurchase
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Supplier { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Total { get; set; }
    public string Currency { get; set; } = "";
}
