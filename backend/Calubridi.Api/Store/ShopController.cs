using System.ComponentModel.DataAnnotations;
using Calubridi.Api.Data;
using Calubridi.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Calubridi.Api.Store;

[ApiController]
[Route("api/shop")]
public sealed class ShopController(ApplicationDbContext db, UserManager<StoreUser> users, IConfiguration config) : ControllerBase
{
    private IQueryable<Product> Published => db.Products.AsNoTracking().Where(p => p.IsActive && p.Category!.IsActive);
    private string UserId => users.GetUserId(User)!;

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog()
    {
        var products = await Published.OrderByDescending(p => p.CreatedAt).Select(p => new
        {
            p.Id, p.Name, p.Description,
            Price = p.Fabrics.Where(f => f.IsActive && f.Fabric!.IsActive)
                .Select(f => (decimal?)f.Price).Min() ?? p.Price,
            p.EstimatedProductionDays,
            Category = p.Category!.Name,
            Cover = p.CoverUrl ?? p.Fabrics
                .Where(f => f.IsActive && f.Fabric!.IsActive)
                .SelectMany(f => f.Colors.Where(c => c.IsActive && c.Color!.IsActive))
                .SelectMany(c => c.Media).Where(m => m.MediaType == "image")
                .OrderByDescending(m => m.IsPrimary).ThenBy(m => m.DisplayOrder).ThenBy(m => m.Id)
                .Select(m => m.Url).FirstOrDefault()
        }).ToListAsync();
        return Ok(new { currency = StoreSetup.Currency(config), products });
    }

    [HttpGet("catalog/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var product = await Published.Where(p => p.Id == id).Select(p => new
        {
            p.Id, p.Name, p.Description, p.Price, p.EstimatedProductionDays, p.CoverUrl,
            Category = p.Category!.Name,
            Fabrics = p.Fabrics.Where(f => f.IsActive && f.Fabric!.IsActive)
                .OrderByDescending(f => f.IsDefault).ThenBy(f => f.Id).Select(f => new
            {
                f.Id, Name = f.Fabric!.Name, f.Price,
                Colors = f.Colors.Where(c => c.IsActive && c.Color!.IsActive)
                    .OrderByDescending(c => c.IsDefault).ThenBy(c => c.Id).Select(c => new
                {
                    c.Id, Name = c.Color!.Name, c.Color.HexCode,
                    Images = c.Media.Where(m => m.MediaType == "image").OrderByDescending(m => m.IsPrimary)
                        .ThenBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.Url).ToList()
                }).ToList()
            }).ToList(),
            Videos = p.Videos.OrderBy(v => v.DisplayOrder).ThenBy(v => v.Id).Select(v => v.Url).ToList()
        }).AsSplitQuery().SingleOrDefaultAsync();
        return product is null ? NotFound() : Ok(product);
    }

    [Authorize, HttpGet("favorites")]
    public async Task<IActionResult> Favorites() =>
        Ok(await db.Favorites.Where(f => f.UserId == UserId).Select(f => f.ProductId).ToListAsync());

    [Authorize, HttpPut("favorites/{id:int}")]
    public async Task<IActionResult> AddFavorite(int id)
    {
        if (!await Published.AnyAsync(p => p.Id == id)) return NotFound();
        var userId = UserId;
        if (await db.Favorites.FindAsync(userId, id) is not null) return NoContent();
        db.Favorites.Add(new Favorite { UserId = userId, ProductId = id });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException e) when (IsUniqueViolation(e)) { }
        return NoContent();
    }

    [Authorize, HttpDelete("favorites/{id:int}")]
    public async Task<IActionResult> RemoveFavorite(int id)
    {
        await db.Favorites.Where(f => f.UserId == UserId && f.ProductId == id).ExecuteDeleteAsync();
        return NoContent();
    }

    public sealed record Item(int ProductId, int? FabricId, int? ColorId, [Range(1, 25)] int Quantity, decimal? ExpectedUnitPrice);

    public sealed record Checkout(
        Guid Key,
        [Required, StringLength(120)] string Customer,
        [Required, StringLength(40)] string Phone,
        [Required, StringLength(300)] string Delivery,
        [StringLength(1500)] string? Notes,
        [Required, MinLength(1), MaxLength(30)] List<Item> Items);

    [Authorize, HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(Checkout input)
    {
        if (input.Key == Guid.Empty || input.Items.Any(i => i is null) ||
            input.Items.GroupBy(i => (i.ProductId, i.FabricId, i.ColorId)).Any(g => g.Count() > 1))
            return BadRequest(new { message = "Revisá las combinaciones y cantidades del carrito." });

        var userId = UserId;
        var existing = await FindOrder(userId, input.Key);
        if (existing is not null) return Ok(ToReceipt(existing));

        var ids = input.Items.Select(i => i.ProductId).Distinct().ToArray();
        var products = await Published.Where(p => ids.Contains(p.Id))
            .Include(p => p.Fabrics).ThenInclude(f => f.Fabric)
            .Include(p => p.Fabrics).ThenInclude(f => f.Colors).ThenInclude(c => c.Color)
            .AsSplitQuery().ToDictionaryAsync(p => p.Id);
        var order = new Order
        {
            UserId = userId, CheckoutKey = input.Key, Customer = input.Customer.Trim(),
            Phone = input.Phone.Trim(), Delivery = input.Delivery.Trim(), Notes = input.Notes?.Trim() ?? "",
            Currency = StoreSetup.Currency(config)
        };
        foreach (var item in input.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product)) return Changed();
            var fabrics = product.Fabrics.Where(f => f.IsActive && f.Fabric!.IsActive).ToList();
            var fabric = fabrics.Find(f => f.Id == item.FabricId);
            if (fabric is null && (item.FabricId is not null || fabrics.Count > 0)) return Changed();
            var colors = fabric?.Colors.Where(c => c.IsActive && c.Color!.IsActive).ToList() ?? [];
            var color = colors.Find(c => c.Id == item.ColorId);
            if (color is null && (item.ColorId is not null || colors.Count > 0)) return Changed();
            var price = fabric?.Price ?? product.Price;
            if (price <= 0 || product.EstimatedProductionDays <= 0) return Changed();
            if (item.ExpectedUnitPrice != price)
                return Conflict(new { message = "Cambió el precio de un diseño. Quitalo del carrito y volvé a elegirlo para ver el precio actual." });
            order.Lines.Add(new OrderLine
            {
                ProductId = product.Id, Name = product.Name, Fabric = fabric?.Fabric?.Name ?? "",
                Color = color?.Color?.Name ?? "", Quantity = item.Quantity,
                UnitPrice = price, ProductionDays = product.EstimatedProductionDays
            });
        }

        order.Total = order.Lines.Sum(l => l.UnitPrice * l.Quantity);
        db.Orders.Add(order);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            var duplicate = await FindOrder(userId, input.Key);
            if (duplicate is not null) return Ok(ToReceipt(duplicate));
            throw;
        }
        return StatusCode(201, ToReceipt(order));
    }

    [Authorize, HttpGet("orders")]
    public async Task<IActionResult> Orders() =>
        Ok((await db.Orders.AsNoTracking().Where(o => o.UserId == UserId)
            .OrderByDescending(o => o.CreatedAt).Include(o => o.Lines).ToListAsync()).Select(ToReceipt));

    private Task<Order?> FindOrder(string userId, Guid key) => db.Orders.AsNoTracking()
        .Include(o => o.Lines).SingleOrDefaultAsync(o => o.UserId == userId && o.CheckoutKey == key);

    private ConflictObjectResult Changed() =>
        Conflict(new { message = "Hay un producto o una variante que cambió. Quitalo del carrito y volvé a elegirlo." });

    internal static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    internal static object ToReceipt(Order order) => new
    {
        order.Id, order.CreatedAt, order.Customer, order.Phone, order.Delivery, order.Notes,
        order.Currency, order.Total, order.Status, order.PaidAt,
        Lines = order.Lines.Select(l => new { l.ProductId, l.Name, l.Fabric, l.Color, l.Quantity, l.UnitPrice, l.ProductionDays })
    };
}
