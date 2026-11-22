using System.ComponentModel.DataAnnotations;
using Calubridi.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Calubridi.Api.Store;

[ApiController]
[Route("api/inquiries")]
public sealed class InquiryController(ApplicationDbContext db) : ControllerBase
{
    public sealed record Input(
        [Required, StringLength(100)] string Name,
        [Required, StringLength(32)] string Phone,
        [Required, RegularExpression("arreglos|pedidos|otra")] string Topic,
        [Required, StringLength(1500, MinimumLength = 8)] string Message);

    [HttpPost, EnableRateLimiting("inquiries")]
    public async Task<IActionResult> Create(Input input, CancellationToken ct)
    {
        var phone = input.Phone.Trim();
        var digits = phone.Count(char.IsAsciiDigit);
        if (digits is < 8 or > 15 || !phone.All(c => char.IsAsciiDigit(c) || "+ ().-".Contains(c))
            || phone.Skip(1).Contains('+') || input.Message.Trim().Length < 8)
            return BadRequest(new { message = "Revisá el teléfono y escribí una consulta de al menos 8 caracteres." });
        db.Inquiries.Add(new Inquiry
        {
            Name = input.Name.Trim(), Phone = phone, Topic = input.Topic, Message = input.Message.Trim()
        });
        await db.SaveChangesAsync(ct);
        return StatusCode(201);
    }

    [HttpGet, Authorize(Roles = "Admin")]
    public async Task<IActionResult> List([Range(1, 100000)] int page = 1, CancellationToken ct = default)
    {
        var rows = await db.Inquiries.AsNoTracking().OrderBy(i => i.AnsweredAt.HasValue)
            .ThenByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .Skip((page - 1) * 30).Take(31).ToListAsync(ct);
        return Ok(new { page, hasMore = rows.Count > 30, items = rows.Take(30) });
    }

    [HttpPatch("{id:guid}/answered"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarkAnswered(Guid id, CancellationToken ct)
    {
        var item = await db.Inquiries.FindAsync([id], ct);
        if (item is null) return NotFound();
        item.AnsweredAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
