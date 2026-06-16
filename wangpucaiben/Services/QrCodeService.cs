using QRCoder;

namespace wangpucaiben.Services;

public sealed class QrCodeService
{
    public string BuildSvgMarkup(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new SvgQRCode(data);
        return qrCode.GetGraphic(10);
    }
}
