using System.ComponentModel.DataAnnotations;
using Calubridi.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Calubridi.Api.Store;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminController(ApplicationDbContext db, IConfiguration config, IWebHostEnvironment host) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(DateOnly? from, DateOnly? to, [Range(1, 1000000)] int page = 1)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
        var start = from ?? today.AddDays(-29);
        var end = to ?? today;
        if (start > end || end.DayNumber - start.DayNumber > 366 || start.Year < 2000 || end.Year >= 9999)
            return BadRequest(new { message = "Elegí un período válido de hasta un año." });
        var since = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(3);
        var until = end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(3);
        var selected = db.Orders.AsNoTracking().Where(o => o.CreatedAt >= since && o.CreatedAt < until);
        var orders = await selected.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip((page - 1) * 30).Take(30).Include(o => o.Lines).ToListAsync();
        var ids = orders.Select(o => o.UserId).Distinct().ToArray();
        var emails = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email);
        var purchases = db.SupplierPurchases.AsNoTracking().Where(p => p.Date >= start && p.Date <= end);
        var spent = await purchases.GroupBy(p => p.Currency)
            .Select(g => new { currency = g.Key, total = g.Sum(p => p.Total) }).ToListAsync();
        var income = await db.Orders.AsNoTracking().Where(o => o.PaidAt >= since && o.PaidAt < until)
            .GroupBy(o => o.Currency).Select(g => new { currency = g.Key, total = g.Sum(o => o.Total) }).ToListAsync();
        return Ok(new
        {
            from = start, to = end, page, orderCount = await selected.CountAsync(),
            statuses = await selected.GroupBy(o => o.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(),
            income, spent, purchaseCount = await purchases.CountAsync(),
            purchases = await purchases.OrderByDescending(p => p.Date).ThenByDescending(p => p.Id)
                .Take(100).ToListAsync(),
            orders = orders.Select(o => new { receipt = ShopController.ToReceipt(o), email = emails.GetValueOrDefault(o.UserId), o.Revision })
        });
    }

    public sealed record StatusInput([Required] string Status, bool MarkPaid, int Revision);

    [HttpPatch("orders/{id:guid}")]
    public async Task<IActionResult> UpdateOrder(Guid id, StatusInput input)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null) return NotFound();
        if (order.Revision != input.Revision)
            return Conflict(new { message = "El pedido cambió. Actualizá el panel antes de guardar." });
        var valid = order.Status == input.Status || (order.Status, input.Status) switch
        {
            ("pending", "confirmed" or "canceled") => true,
            ("confirmed", "production" or "canceled") => true,
            ("production", "ready" or "canceled") => true,
            ("ready", "delivered") => true,
            _ => false
        };
        if (!valid || ((order.PaidAt.HasValue || input.MarkPaid) && input.Status == "canceled"))
            return BadRequest(new { message = "Ese cambio de estado no está disponible para este pedido." });

        order.Status = input.Status;
        if (input.MarkPaid) order.PaidAt ??= DateTime.UtcNow;
        order.Revision++;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "El pedido cambió. Actualizá el panel." }); }
        return NoContent();
    }

    public sealed record PurchaseInput(
        DateOnly Date,
        [Required, StringLength(120)] string Supplier,
        [Required, StringLength(280)] string Description,
        [Range(0.01, 999999999)] decimal Total);

    [HttpPost("purchases")]
    public async Task<IActionResult> CreatePurchase(PurchaseInput input)
    {
        if (input.Date.Year < 2000 || input.Date > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3)))
            return BadRequest(new { message = "La fecha de la compra no es válida." });
        db.SupplierPurchases.Add(new SupplierPurchase
        {
            Date = input.Date, Supplier = input.Supplier.Trim(), Description = input.Description.Trim(),
            Total = decimal.Round(input.Total, 2), Currency = StoreSetup.Currency(config)
        });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("products/{id:int}/cover"), RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> UploadCover(int id, IFormFile file)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();
        var extension = await UploadFormat.ImageExtension(file);
        if (extension is null) return BadRequest(new { message = "Elegí un JPG, PNG o WebP válido de hasta 10 MB." });

        var folder = Path.Combine(host.WebRootPath, "uploads", "covers");
        Directory.CreateDirectory(folder);
        var name = Guid.NewGuid().ToString("N") + extension;
        var path = Path.Combine(folder, name);
        var previous = product.CoverUrl;
        try
        {
            await using (var stream = System.IO.File.Create(path)) await file.CopyToAsync(stream);
            product.CoverUrl = "/uploads/covers/" + name;
            await db.SaveChangesAsync();
        }
        catch
        {
            System.IO.File.Delete(path);
            throw;
        }
        if (previous?.StartsWith("/uploads/covers/", StringComparison.Ordinal) == true)
            System.IO.File.Delete(Path.Combine(folder, Path.GetFileName(previous)));
        return Ok(new { product.CoverUrl });
    }
}
