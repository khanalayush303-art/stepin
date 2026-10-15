using SixLabors.ImageSharp;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Validates an uploaded image or PDF before it ever reaches <c>IAidxMediaStorage</c>. Layered,
/// not magic-bytes-only: an extension/content-type allow-list, then an actual decode (via
/// ImageSharp — see the facts-to-verify note this phase's plan raised: no image-decoding library
/// existed in this codebase before) that rejects a corrupt or polyglot file outright, and a cap on
/// decoded pixel dimensions against a decompression-bomb-style resource exhaustion (a small file
/// that decodes to an enormous bitmap).
/// </summary>
public static class AidxMediaValidation
{
    public const long MaxImageSizeBytes = 5 * 1024 * 1024;
    public const long MaxPdfSizeBytes = 10 * 1024 * 1024;
    private const int MaxDecodedDimension = 8000;

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp",
    };

    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp",
    };

    public sealed record ValidationResult(bool Success, string? Error, string? Extension, byte[]? Content);

    /// <summary>Reads the whole file into memory (capped by <see cref="MaxImageSizeBytes"/> before
    /// this is ever called) and decodes it. The decode is the real check — a renamed non-image
    /// file, or one with a forged header, fails here even if its extension and declared
    /// Content-Type both look legitimate.</summary>
    public static async Task<ValidationResult> ValidateImageAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedImageExtensions.Contains(extension))
        {
            return new ValidationResult(false, "Only .jpg, .jpeg, .png or .webp images are accepted.", null, null);
        }

        if (string.IsNullOrEmpty(file.ContentType) || !AllowedImageContentTypes.Contains(file.ContentType))
        {
            return new ValidationResult(false, "Unrecognised image content type.", null, null);
        }

        if (file.Length <= 0 || file.Length > MaxImageSizeBytes)
        {
            return new ValidationResult(false, $"Image must be no larger than {MaxImageSizeBytes / (1024 * 1024)} MB.", null, null);
        }

        byte[] content;
        using (var buffer = new MemoryStream())
        {
            await file.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }

        try
        {
            using var image = Image.Load(content);
            if (image.Width > MaxDecodedDimension || image.Height > MaxDecodedDimension)
            {
                return new ValidationResult(false, $"Image dimensions must be no larger than {MaxDecodedDimension}x{MaxDecodedDimension} pixels.", null, null);
            }
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            return new ValidationResult(false, "The file could not be decoded as a valid image.", null, null);
        }

        return new ValidationResult(true, null, extension.ToLowerInvariant(), content);
    }

    /// <summary>A real signature check (the first five bytes of a PDF are always literally
    /// <c>%PDF-</c>), not just an extension/Content-Type check. PDFs are never "decoded" the way
    /// images are here — only served with safe headers (see AidxMediaEndpoints), which is the
    /// actual mitigation for a malicious PDF, not a parse-and-reject step this layer would have to
    /// implement a full PDF parser to do meaningfully.</summary>
    public static async Task<ValidationResult> ValidatePdfAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationResult(false, "Only .pdf files are accepted.", null, null);
        }

        if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationResult(false, "Unrecognised PDF content type.", null, null);
        }

        if (file.Length <= 0 || file.Length > MaxPdfSizeBytes)
        {
            return new ValidationResult(false, $"PDF must be no larger than {MaxPdfSizeBytes / (1024 * 1024)} MB.", null, null);
        }

        byte[] content;
        using (var buffer = new MemoryStream())
        {
            await file.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }

        if (content.Length < 5 || content[0] != '%' || content[1] != 'P' || content[2] != 'D' || content[3] != 'F' || content[4] != '-')
        {
            return new ValidationResult(false, "The file is not a valid PDF.", null, null);
        }

        return new ValidationResult(true, null, ".pdf", content);
    }
}
