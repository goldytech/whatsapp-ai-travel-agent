using System.Text;
using AgentForge.Verticals.Abstractions;

namespace AgentForge.Verticals.Travel;

internal sealed class ResolvedTravelVerticalDescriptor(
    TravelCustomerProfileOptions options,
    string prompt) : IVerticalDescriptor
{
    public string VerticalId => "travel";

    public string DisplayName => options.DisplayName;

    public string AgentName => options.AgentName;

    public string AgentDescription => options.AgentDescription;

    public string SystemPrompt { get; } = BuildSystemPrompt(options, prompt);

    public string McpServerName => options.McpServerName;

    public string AssetPathPrefix => options.AssetPathPrefix;

    public string PreviewTitle => options.PreviewTitle;

    public string PreviewDescription => options.PreviewDescription;

    private static string BuildSystemPrompt(TravelCustomerProfileOptions options, string prompt)
    {
        var builder = new StringBuilder();
        builder.AppendLine(prompt.Trim());
        builder.AppendLine();
        builder.AppendLine("RUNTIME CUSTOMER PROFILE:");
        builder.AppendLine($"- Business name: {options.DisplayName}");
        builder.AppendLine($"- Agent name: {options.AgentName}");
        builder.AppendLine($"- Agent description: {options.AgentDescription}");
        builder.AppendLine($"- Asset path prefix: {options.AssetPathPrefix}");
        builder.AppendLine($"- Tone: {options.BusinessProfile.Tone}");
        builder.AppendLine($"- Business overview: {options.BusinessProfile.Overview}");
        builder.AppendLine($"- Primary audience: {options.BusinessProfile.PrimaryAudience}");
        AppendList(builder, "Supported languages", options.BusinessProfile.SupportedLanguages);
        AppendList(builder, "Specialties", options.BusinessProfile.Specialties);
        AppendList(builder, "Upsell offers", options.BusinessProfile.UpsellOffers);
        builder.AppendLine();
        builder.AppendLine("LEAD CAPTURE FIELDS:");
        AppendList(builder, "Required", options.LeadCapture.RequiredFields);
        AppendList(builder, "Optional", options.LeadCapture.OptionalFields);
        builder.AppendLine();
        builder.AppendLine("ENABLED CAPABILITIES:");
        builder.AppendLine($"- Sightseeing images: {ToEnabledDisabled(options.Capabilities.ShowSightseeingImages)}");
        builder.AppendLine($"- Hotel images: {ToEnabledDisabled(options.Capabilities.ShowHotelImages)}");
        builder.AppendLine($"- Booking inquiries: {ToEnabledDisabled(options.Capabilities.CreateBookingInquiries)}");
        builder.AppendLine($"- Post-trip feedback: {ToEnabledDisabled(options.Capabilities.CollectPostTripFeedback)}");
        builder.AppendLine($"- Upsell suggestions: {ToEnabledDisabled(options.Capabilities.SuggestUpsells)}");
        builder.AppendLine();
        builder.AppendLine("BUSINESS HOURS:");
        builder.AppendLine($"- Time zone: {options.BusinessHours.TimeZone}");
        foreach (var schedule in options.BusinessHours.WeeklySchedule)
        {
            builder.AppendLine($"- {schedule.Key}: {schedule.Value}");
        }

        builder.AppendLine();
        builder.AppendLine("HANDOFF RULES:");
        builder.AppendLine($"- Enabled: {ToEnabledDisabled(options.Handoff.Enabled)}");
        builder.AppendLine($"- Destination: {options.Handoff.Destination}");
        builder.AppendLine($"- Response window: {options.Handoff.ResponseWindow}");
        AppendList(builder, "Escalation rules", options.Handoff.Rules);
        builder.AppendLine();
        builder.AppendLine("Treat this runtime customer profile as authoritative over any generic assumptions.");

        return builder.ToString().TrimEnd();
    }

    private static void AppendList(StringBuilder builder, string label, IReadOnlyCollection<string> items)
    {
        builder.AppendLine($"- {label}: {string.Join(", ", items)}");
    }

    private static string ToEnabledDisabled(bool value) => value ? "Enabled" : "Disabled";
}
