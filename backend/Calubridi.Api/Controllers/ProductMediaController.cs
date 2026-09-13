using Calubridi.Api.Data;
using Calubridi.Api.DTOs.ProductMedia;
using Calubridi.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Calubridi.Api.Controllers;

[ApiController]
[Route("api/products/{productId:int}/fabrics/{productFabricId:int}/colors/{productFabricColorId:int}/media")]
public class ProductMediaController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public ProductMediaController(
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // =========================================================
    // GET - Obtener multimedia de una combinación tela + color
    // =========================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductMediaResponseDto>>> GetMedia(
        int productId,
        int productFabricId,
        int productFabricColorId)
    {
        var combinationExists = await _context.ProductFabricColors
            .AnyAsync(pfc =>
                pfc.Id == productFabricColorId &&
                pfc.ProductFabricId == productFabricId &&
                pfc.ProductFabric!.ProductId == productId);

        if (!combinationExists)
        {
            return NotFound(new
            {
                message = "La combinación de producto, tela y color indicada no existe."
            });
        }

        var media = await _context.ProductMedia
            .AsNoTracking()
            .Where(pm =>
                pm.ProductFabricColorId == productFabricColorId)
            .OrderByDescending(pm => pm.IsPrimary)
            .ThenBy(pm => pm.DisplayOrder)
            .ThenBy(pm => pm.Id)
            .Select(pm => new ProductMediaResponseDto
            {
                Id = pm.Id,
                ProductFabricColorId = pm.ProductFabricColorId,
                Url = pm.Url,
                MediaType = pm.MediaType,
                IsPrimary = pm.IsPrimary,
                DisplayOrder = pm.DisplayOrder,
                CreatedAt = pm.CreatedAt
            })
            .ToListAsync();

        return Ok(media);
    }

    // =========================================================
    // POST - Subir imagen
    // =========================================================

    [HttpPost]
    public async Task<ActionResult<ProductMediaResponseDto>> UploadMedia(
        int productId,
        int productFabricId,
        int productFabricColorId,
        IFormFile file,
        bool isPrimary = false,
        int displayOrder = 0)
    {
        var combinationExists = await _context.ProductFabricColors
            .AnyAsync(pfc =>
                pfc.Id == productFabricColorId &&
                pfc.ProductFabricId == productFabricId &&
                pfc.ProductFabric!.ProductId == productId);

        if (!combinationExists)
        {
            return NotFound(new
            {
                message = "La combinación de producto, tela y color indicada no existe."
            });
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "Debe seleccionar un archivo."
            });
        }

        var imageExtensions = new[]
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        var extension = Path
            .GetExtension(file.FileName)
            .ToLowerInvariant();

        if (!imageExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                message = "Formato no permitido. Se aceptan JPG, JPEG, PNG y WEBP."
            });
        }

        const long maxImageSize = 10 * 1024 * 1024;

        if (file.Length > maxImageSize)
        {
            return BadRequest(new
            {
                message = "La imagen no puede superar los 10 MB."
            });
        }

        var uploadsFolder = Path.Combine(
            _environment.ContentRootPath,
            "wwwroot",
            "uploads",
            "products"
        );

        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}{extension}";

        var filePath = Path.Combine(
            uploadsFolder,
            fileName
        );

        await using (var stream = new FileStream(
            filePath,
            FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // La imagen principal se controla por combinación tela + color.
        if (isPrimary)
        {
            var currentPrimaryMedia = await _context.ProductMedia
                .Where(pm =>
                    pm.ProductFabricColorId == productFabricColorId &&
                    pm.IsPrimary)
                .ToListAsync();

            foreach (var media in currentPrimaryMedia)
            {
                media.IsPrimary = false;
            }
        }

        var relativeUrl =
            $"/uploads/products/{fileName}";

        var productMedia = new ProductMedia
        {
            ProductFabricColorId = productFabricColorId,
            Url = relativeUrl,
            MediaType = "image",
            IsPrimary = isPrimary,
            DisplayOrder = displayOrder,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProductMedia.Add(productMedia);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            throw;
        }

        var response = new ProductMediaResponseDto
        {
            Id = productMedia.Id,
            ProductFabricColorId = productMedia.ProductFabricColorId,
            Url = productMedia.Url,
            MediaType = productMedia.MediaType,
            IsPrimary = productMedia.IsPrimary,
            DisplayOrder = productMedia.DisplayOrder,
            CreatedAt = productMedia.CreatedAt
        };

        return Created(
            $"/api/products/{productId}/fabrics/{productFabricId}/colors/{productFabricColorId}/media/{productMedia.Id}",
            response
        );
    }

    // =========================================================
    // DELETE - Eliminar multimedia
    // =========================================================

    [HttpDelete("{mediaId:int}")]
    public async Task<IActionResult> DeleteMedia(
        int productId,
        int productFabricId,
        int productFabricColorId,
        int mediaId)
    {
        var combinationExists = await _context.ProductFabricColors
            .AnyAsync(pfc =>
                pfc.Id == productFabricColorId &&
                pfc.ProductFabricId == productFabricId &&
                pfc.ProductFabric!.ProductId == productId);

        if (!combinationExists)
        {
            return NotFound(new
            {
                message = "La combinación de producto, tela y color indicada no existe."
            });
        }

        var media = await _context.ProductMedia
            .FirstOrDefaultAsync(pm =>
                pm.Id == mediaId &&
                pm.ProductFabricColorId == productFabricColorId);

        if (media is null)
        {
            return NotFound(new
            {
                message = "El archivo multimedia indicado no existe para esta combinación."
            });
        }

        var wasPrimary = media.IsPrimary;

        var relativePath = media.Url
            .TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);

        var filePath = Path.Combine(
            _environment.ContentRootPath,
            "wwwroot",
            relativePath
        );

        _context.ProductMedia.Remove(media);

        if (wasPrimary)
        {
            var nextPrimary = await _context.ProductMedia
                .Where(pm =>
                    pm.ProductFabricColorId == productFabricColorId &&
                    pm.Id != mediaId &&
                    pm.MediaType == "image")
                .OrderBy(pm => pm.DisplayOrder)
                .ThenBy(pm => pm.Id)
                .FirstOrDefaultAsync();

            if (nextPrimary is not null)
            {
                nextPrimary.IsPrimary = true;
            }
        }

        await _context.SaveChangesAsync();

        // Reordenar solamente la multimedia de esta combinación.
        var remainingMedia = await _context.ProductMedia
            .Where(pm =>
                pm.ProductFabricColorId == productFabricColorId)
            .OrderBy(pm => pm.DisplayOrder)
            .ThenBy(pm => pm.Id)
            .ToListAsync();

        for (int i = 0; i < remainingMedia.Count; i++)
        {
            remainingMedia[i].DisplayOrder = i + 1;
        }

        await _context.SaveChangesAsync();

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }

        return NoContent();
    }

    // =========================================================
    // PUT - Establecer imagen principal
    // =========================================================

    [HttpPut("{mediaId:int}/primary")]
    public async Task<IActionResult> SetPrimaryMedia(
        int productId,
        int productFabricId,
        int productFabricColorId,
        int mediaId)
    {
        var combinationExists = await _context.ProductFabricColors
            .AnyAsync(pfc =>
                pfc.Id == productFabricColorId &&
                pfc.ProductFabricId == productFabricId &&
                pfc.ProductFabric!.ProductId == productId);

        if (!combinationExists)
        {
            return NotFound(new
            {
                message = "La combinación de producto, tela y color indicada no existe."
            });
        }

        var media = await _context.ProductMedia
            .FirstOrDefaultAsync(pm =>
                pm.Id == mediaId &&
                pm.ProductFabricColorId == productFabricColorId);

        if (media is null)
        {
            return NotFound(new
            {
                message = "El archivo multimedia indicado no existe para esta combinación."
            });
        }

        if (media.MediaType != "image")
        {
            return BadRequest(new
            {
                message = "Solo una imagen puede establecerse como principal."
            });
        }

        var currentPrimaryMedia = await _context.ProductMedia
            .Where(pm =>
                pm.ProductFabricColorId == productFabricColorId &&
                pm.IsPrimary)
            .ToListAsync();

        foreach (var item in currentPrimaryMedia)
        {
            item.IsPrimary = false;
        }

        media.IsPrimary = true;

        await _context.SaveChangesAsync();

        return NoContent();
    }
}