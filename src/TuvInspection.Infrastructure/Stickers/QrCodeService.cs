using QRCoder;

namespace TuvInspection.Infrastructure.Stickers;

/// <summary>
/// Renders QR codes as PNG bytes. Encoding is the public verification URL
/// for the given sticker number, so a phone scan opens the validation page directly.
/// </summary>
public sealed class QrCodeService
{
    public byte[] PngFor(string verificationUrl, int pixelsPerModule = 8)
    {
        if (string.IsNullOrWhiteSpace(verificationUrl))
            throw new ArgumentException("URL is required.", nameof(verificationUrl));

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(verificationUrl, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }
}
