using Calubridi.Api.Data;
using Calubridi.Api.DTOs.ProductMedia;
using Calubridi.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Calubridi.Api.Controllers;

[ApiController]
[Route("api/products/{productId:int}/media")]
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

    [HttpPost]
    public async Task<ActionResult<ProductMediaResponseDto>> UploadMedia(
        int productId,
        IFormFile file,
        bool isPrimary = false,
        int displayOrder = 0)
    {
        var productExists = await _context.Products
            .AnyAsync(p => p.Id == productId);

        if (!productExists)
        {
            return NotFound(new
            {
                message = "El producto indicado no existe."
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

        var videoExtensions = new[]
        {
            ".mp4",
            ".webm"
        };

        var extension = Path
            .GetExtension(file.FileName)
            .ToLowerInvariant();

        string mediaType;

        if (imageExtensions.Contains(extension))
        {
            mediaType = "image";
        }
        else if (videoExtensions.Contains(extension))
        {
            mediaType = "video";
        }
        else
        {
            return BadRequest(new
            {
                message = "Formato no permitido. Se aceptan JPG, JPEG, PNG, WEBP, MP4 y WEBM."
            });
        }

        const long maxImageSize = 10 * 1024 * 1024;
        const long maxVideoSize = 100 * 1024 * 1024;

        if (mediaType == "image" && file.Length > maxImageSize)
        {
            return BadRequest(new
            {
                message = "La imagen no puede superar los 10 MB."
            });
        }

        if (mediaType == "video" && file.Length > maxVideoSize)
        {
            return BadRequest(new
            {
                message = "El video no puede superar los 100 MB."
            });
        }

        if (mediaType == "video" && isPrimary)
        {
            return BadRequest(new
            {
                message = "Un video no puede establecerse como imagen principal."
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

        if (isPrimary)
        {
            var currentPrimaryMedia = await _context.ProductMedia
                .Where(pm =>
                    pm.ProductId == productId &&
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
            ProductId = productId,
            Url = relativeUrl,
            MediaType = mediaType,
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
            ProductId = productMedia.ProductId,
            Url = productMedia.Url,
            MediaType = productMedia.MediaType,
            IsPrimary = productMedia.IsPrimary,
            DisplayOrder = productMedia.DisplayOrder,
            CreatedAt = productMedia.CreatedAt
        };

        return Created(
            $"/api/products/{productId}/media/{productMedia.Id}",
            response
        );
    }

    [HttpDelete("{mediaId:int}")]
    public async Task<IActionResult> DeleteMedia(
        int productId,
        int mediaId)
    {
        var media = await _context.ProductMedia
            .FirstOrDefaultAsync(pm =>
                pm.Id == mediaId &&
                pm.ProductId == productId);

        if (media is null)
        {
            return NotFound(new
            {
                message = "El archivo multimedia indicado no existe para este producto."
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
                    pm.ProductId == productId &&
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

        // Reordenar 1, 2, 3...
        var remainingMedia = await _context.ProductMedia
            .Where(pm => pm.ProductId == productId)
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

    [HttpPut("{mediaId:int}/primary")]
    public async Task<IActionResult> SetPrimaryMedia(
        int productId,
        int mediaId)
    {
        var media = await _context.ProductMedia
            .FirstOrDefaultAsync(pm =>
                pm.Id == mediaId &&
                pm.ProductId == productId);

        if (media is null)
        {
            return NotFound(new
            {
                message = "El archivo multimedia indicado no existe para este producto."
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
                pm.ProductId == productId &&
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