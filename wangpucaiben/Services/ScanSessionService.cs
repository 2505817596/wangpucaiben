namespace wangpucaiben.Services;

public sealed class ScanSessionService
{
    private readonly QrCodeService _qrCodeService;
    private readonly string _sessionId;

    public ScanSessionService(BarcodeScanBridgeService scanBridgeService, QrCodeService qrCodeService)
    {
        _qrCodeService = qrCodeService;
        _sessionId = scanBridgeService.CreateSessionId();
    }

    public string SessionId => _sessionId;

    public string BuildScannerUrl(string baseUri)
    {
        var encodedSessionId = Uri.EscapeDataString(_sessionId);
        return $"{baseUri.TrimEnd('/')}/scanner?sessionId={encodedSessionId}";
    }

    public string BuildScannerQrCodeMarkup(string baseUri)
    {
        return _qrCodeService.BuildSvgMarkup(BuildScannerUrl(baseUri));
    }
}
