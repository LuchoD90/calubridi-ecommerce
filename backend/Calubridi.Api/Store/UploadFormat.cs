namespace Calubridi.Api.Store;

internal static class UploadFormat
{
    public static async Task<string?> ImageExtension(IFormFile? file)
    {
        if (file is null || file.Length is < 12 or > 10 * 1024 * 1024) return null;
        using var stream = file.OpenReadStream();
        var header = new byte[12];
        await stream.ReadExactlyAsync(header);
        if (header.AsSpan(0, 3).SequenceEqual(new byte[] { 255, 216, 255 })) return ".jpg";
        if (header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ".png";
        if (header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }

    public static async Task<bool> IsVideo(IFormFile file, string extension)
    {
        if (file.Length < 12) return false;
        using var stream = file.OpenReadStream();
        var header = new byte[12];
        await stream.ReadExactlyAsync(header);
        return extension == ".mp4" && header.AsSpan(4, 4).SequenceEqual("ftyp"u8) ||
            extension == ".webm" && header.AsSpan(0, 4).SequenceEqual(new byte[] { 26, 69, 223, 163 });
    }
}
