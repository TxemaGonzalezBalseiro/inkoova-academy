using QRCoder;

namespace Inkoova.Academy.Infrastructure.Documents;

/// <summary>
/// QR as a PNG byte array. <see cref="PngByteQRCode"/> is the cross-platform generator:
/// the bitmap-based one in QRCoder depends on System.Drawing, which is Windows-only and
/// would break the Linux container.
/// </summary>
internal static class QrCode
{
    public static byte[] Generate(string payload)
    {
        using var generator = new QRCodeGenerator();
        // Level Q tolerates ~25% damage: enough for a printed certificate that gets scanned
        // from a photo or a photocopy.
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(pixelsPerModule: 12);
    }
}
