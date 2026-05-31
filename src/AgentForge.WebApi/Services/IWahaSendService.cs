using AgentForge.Verticals.Abstractions;
using AgentForge.WebApi.Models;

namespace AgentForge.WebApi.Services;

/// <summary>
/// Abstracts WhatsApp message sending across WAHA Core (free) and WAHA Plus (paid) tiers.
/// The active implementation is resolved at startup from the <c>WAHA_TIER</c> environment
/// variable and registered as a keyed singleton in DI.
/// </summary>
public interface IWahaSendService : IMessageSender
{
    /// <summary>
    /// Sends an interactive list menu.
    /// <list type="bullet">
    ///   <item><b>Plus (WEBJS/WPP)</b>: native WhatsApp list UI via <c>POST /api/sendList</c>.</item>
    ///   <item><b>Core or NOWEB</b>: formats as a numbered text list — NOWEB does not support
    ///   <c>sendList</c> even with Plus; text fallback is always used with NOWEB engine.</item>
    /// </list>
    /// Not yet wired to AI output — reserved for future interactive-menu feature.
    /// </summary>
    Task SendListAsync(string chatId, string title, string body, string footer,
        string buttonText, ListSection[] sections, CancellationToken ct = default);

    /// <summary>
    /// Sends quick-reply buttons.
    /// <list type="bullet">
    ///   <item><b>Plus (WEBJS only)</b>: native reply buttons via <c>POST /api/send/buttons/reply</c>.</item>
    ///   <item><b>Core or NOWEB</b>: formats options as numbered text — NOWEB does not support
    ///   buttons even with Plus; text fallback is always used with NOWEB engine.</item>
    /// </list>
    /// Not yet wired to AI output — reserved for future booking-confirmation feature.
    /// </summary>
    Task SendButtonsAsync(string chatId, string body, ButtonItem[] buttons, CancellationToken ct = default);
}
