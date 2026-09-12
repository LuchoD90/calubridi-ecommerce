using Calubridi.Api.Data;
using Calubridi.Api.DTOs.ProductFabricColors;
using Calubridi.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Calubridi.Api.Controllers;

[ApiController]
[Route("api/products/{productId:int}/fabrics/{productFabricId:int}/colors")]
public class ProductFabricColorsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProductFabricColorsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/products/7/fabrics/1/colors
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductFabricColorResponseDto>>> GetColors(
        int productId,
        int productFabricId)
    {
        var productFabricExists = await _context.ProductFabrics
            .AnyAsync(pf =>
                pf.Id == productFabricId &&
                pf.ProductId == productId);

        if (!productFabricExists)
        {
            return NotFound(new
            {
                message = "La tela indicada no está asociada a este producto."
            });
        }

        var colors = await _context.ProductFabricColors
            .AsNoTracking()
            .Where(pfc => pfc.ProductFabricId == productFabricId)
            .OrderByDescending(pfc => pfc.IsDefault)
            .ThenBy(pfc => pfc.Color!.Name)
            .Select(pfc => new ProductFabricColorResponseDto
            {
                Id = pfc.Id,
                ProductFabricId = pfc.ProductFabricId,
                ColorId = pfc.ColorId,
                ColorName = pfc.Color!.Name,
                HexCode = pfc.Color.HexCode,
                IsDefault = pfc.IsDefault,
                IsActive = pfc.IsActive
            })
            .ToListAsync();

        return Ok(colors);
    }

    // POST: api/products/7/fabrics/1/colors
    [HttpPost]
    public async Task<ActionResult<ProductFabricColorResponseDto>> AddColor(
        int productId,
        int productFabricId,
        CreateProductFabricColorDto request)
    {
        var productFabric = await _context.ProductFabrics
            .FirstOrDefaultAsync(pf =>
                pf.Id == productFabricId &&
                pf.ProductId == productId);

        if (productFabric is null)
        {
            return NotFound(new
            {
                message = "La tela indicada no está asociada a este producto."
            });
        }

        var color = await _context.Colors
            .FirstOrDefaultAsync(c => c.Id == request.ColorId);

        if (color is null)
        {
            return BadRequest(new
            {
                message = "El color indicado no existe."
            });
        }

        var alreadyExists = await _context.ProductFabricColors
            .AnyAsync(pfc =>
                pfc.ProductFabricId == productFabricId &&
                pfc.ColorId == request.ColorId);

        if (alreadyExists)
        {
            return Conflict(new
            {
                message = "El color ya está asociado a esta tela del producto."
            });
        }

        if (request.IsDefault)
        {
            var currentDefaults = await _context.ProductFabricColors
                .Where(pfc =>
                    pfc.ProductFabricId == productFabricId &&
                    pfc.IsDefault)
                .ToListAsync();

            foreach (var item in currentDefaults)
            {
                item.IsDefault = false;
            }
        }

        var productFabricColor = new ProductFabricColor
        {
            ProductFabricId = productFabricId,
            ColorId = request.ColorId,
            IsDefault = request.IsDefault,
            IsActive = request.IsActive
        };

        _context.ProductFabricColors.Add(productFabricColor);
        await _context.SaveChangesAsync();

        var response = new ProductFabricColorResponseDto
        {
            Id = productFabricColor.Id,
            ProductFabricId = productFabricColor.ProductFabricId,
            ColorId = productFabricColor.ColorId,
            ColorName = color.Name,
            HexCode = color.HexCode,
            IsDefault = productFabricColor.IsDefault,
            IsActive = productFabricColor.IsActive
        };

        return Created(
            $"/api/products/{productId}/fabrics/{productFabricId}/colors/{productFabricColor.Id}",
            response
        );
    }

    // PUT: api/products/7/fabrics/1/colors/1
    [HttpPut("{productFabricColorId:int}")]
    public async Task<ActionResult<ProductFabricColorResponseDto>> UpdateColor(
    int productId,
    int productFabricId,
    int productFabricColorId,
    UpdateProductFabricColorDto request)
{
    var productFabricExists = await _context.ProductFabrics
        .AnyAsync(pf =>
            pf.Id == productFabricId &&
            pf.ProductId == productId);

    if (!productFabricExists)
    {
        return NotFound(new
        {
            message = "La tela indicada no está asociada a este producto."
        });
    }

    var productFabricColor = await _context.ProductFabricColors
        .FirstOrDefaultAsync(pfc =>
            pfc.Id == productFabricColorId &&
            pfc.ProductFabricId == productFabricId);

    if (productFabricColor is null)
    {
        return NotFound(new
        {
            message = "El color asociado a esta tela no existe."
        });
    }

    // Un color predeterminado necesariamente debe estar activo.
    if (request.IsDefault && !request.IsActive)
    {
        return BadRequest(new
        {
            message = "Un color predeterminado no puede estar inactivo."
        });
    }

    // Si estamos quitando el DEFAULT o desactivando el color que actualmente
    // es DEFAULT, debe existir otro color activo que pueda ocupar su lugar.
    if (productFabricColor.IsDefault &&
        (!request.IsDefault || !request.IsActive))
    {
        var nextDefault = await _context.ProductFabricColors
            .Where(pfc =>
                pfc.ProductFabricId == productFabricId &&
                pfc.Id != productFabricColorId &&
                pfc.IsActive)
            .OrderBy(pfc => pfc.Id)
            .FirstOrDefaultAsync();

        if (nextDefault is null)
        {
            return Conflict(new
            {
                message = "No se puede quitar o desactivar el único color predeterminado. Debe existir otro color activo."
            });
        }

        nextDefault.IsDefault = true;
    }

    // Si este color pasa a ser DEFAULT, quitamos el DEFAULT anterior.
    if (request.IsDefault)
    {
        var currentDefaults = await _context.ProductFabricColors
            .Where(pfc =>
                pfc.ProductFabricId == productFabricId &&
                pfc.Id != productFabricColorId &&
                pfc.IsDefault)
            .ToListAsync();

        foreach (var item in currentDefaults)
        {
            item.IsDefault = false;
        }
    }

    productFabricColor.IsDefault = request.IsDefault;
    productFabricColor.IsActive = request.IsActive;

    await _context.SaveChangesAsync();

    var color = await _context.Colors
        .AsNoTracking()
        .FirstAsync(c => c.Id == productFabricColor.ColorId);

    var response = new ProductFabricColorResponseDto
    {
        Id = productFabricColor.Id,
        ProductFabricId = productFabricColor.ProductFabricId,
        ColorId = productFabricColor.ColorId,
        ColorName = color.Name,
        HexCode = color.HexCode,
        IsDefault = productFabricColor.IsDefault,
        IsActive = productFabricColor.IsActive
    };

    return Ok(response);
    }

    // DELETE: api/products/7/fabrics/1/colors/1
    [HttpDelete("{productFabricColorId:int}")]
    public async Task<IActionResult> DeleteColor(
        int productId,
        int productFabricId,
        int productFabricColorId)
    {
        var productFabricExists = await _context.ProductFabrics
            .AnyAsync(pf =>
                pf.Id == productFabricId &&
                pf.ProductId == productId);

        if (!productFabricExists)
        {
            return NotFound(new
            {
                message = "La tela indicada no está asociada a este producto."
            });
        }

        var productFabricColor = await _context.ProductFabricColors
            .FirstOrDefaultAsync(pfc =>
                pfc.Id == productFabricColorId &&
                pfc.ProductFabricId == productFabricId);

        if (productFabricColor is null)
        {
            return NotFound(new
            {
                message = "El color asociado a esta tela no existe."
            });
        }

        var wasDefault = productFabricColor.IsDefault;

        _context.ProductFabricColors.Remove(productFabricColor);
        await _context.SaveChangesAsync();

        if (wasDefault)
        {
            var nextColor = await _context.ProductFabricColors
                .Where(pfc =>
                    pfc.ProductFabricId == productFabricId &&
                    pfc.IsActive)
                .OrderBy(pfc => pfc.Id)
                .FirstOrDefaultAsync();

            if (nextColor is not null)
            {
                nextColor.IsDefault = true;
                await _context.SaveChangesAsync();
            }
        }

        return NoContent();
    }
}