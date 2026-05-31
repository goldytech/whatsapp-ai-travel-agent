namespace Aspire.Hosting;

/// <summary>
/// Specifies the WAHA engine to use for WhatsApp connectivity.
/// </summary>
public enum WahaEngine
{
    /// <summary>Browser-based engine via Puppeteer + Chromium (WhatsApp Web JS). Default in WAHA.</summary>
    WEBJS,

    /// <summary>Browser-based engine via Puppeteer using the WPP library.</summary>
    WPP,

    /// <summary>WebSocket-based engine (no Chromium). Lighter on resources; native poll support.</summary>
    NOWEB,

    /// <summary>WebSocket-based engine written in Go. Next-generation; x86 only (no ARM image).</summary>
    GOWS,
}
