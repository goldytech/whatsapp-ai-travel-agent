namespace Aspire.Hosting;

/// <summary>
/// Selects the WAHA subscription tier, which controls both the Docker image used and
/// which message-sending capabilities are active in <c>Waha.WebApi</c>.
/// </summary>
/// <remarks>
/// <b>Core</b> (default): <c>devlikeapro/waha</c> — free, open-source. Only <c>sendText</c>
/// and other non-media APIs are available. Media sends fall back to link-preview text.<br/>
/// <b>Plus</b>: <c>devlikeapro/waha-plus</c> — paid ($19/month via Patreon). Enables
/// <c>sendImage</c>, <c>sendFile</c>, <c>sendVoice</c>, and <c>sendVideo</c> on all engines.
/// Requires a one-time <c>docker login -u devlikeapro -p {PATRON_KEY}</c> to pull the image.
/// </remarks>
public enum WahaTier
{
    /// <summary>Free open-source image (<c>devlikeapro/waha</c>). Media sends fall back to text.</summary>
    Core,

    /// <summary>Paid Plus image (<c>devlikeapro/waha-plus</c>). Enables native media sending.</summary>
    Plus,
}
