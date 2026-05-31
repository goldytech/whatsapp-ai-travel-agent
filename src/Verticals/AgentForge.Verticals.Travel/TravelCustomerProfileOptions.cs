using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AgentForge.Verticals.Travel;

public sealed class TravelCustomerProfileOptions
{
    public const string SectionName = "TravelCustomerProfile";

    public string DisplayName { get; set; } = string.Empty;

    public string AgentName { get; set; } = string.Empty;

    public string AgentDescription { get; set; } = string.Empty;

    public string McpServerName { get; set; } = string.Empty;

    public string AssetPathPrefix { get; set; } = string.Empty;

    public string PreviewTitle { get; set; } = string.Empty;

    public string PreviewDescription { get; set; } = string.Empty;

    public string PromptFileName { get; set; } = "prompt.md";

    public TravelBusinessProfileOptions BusinessProfile { get; set; } = new();

    public TravelLeadCaptureOptions LeadCapture { get; set; } = new();

    public TravelCapabilityOptions Capabilities { get; set; } = new();

    public TravelBusinessHoursOptions BusinessHours { get; set; } = new();

    public TravelHandoffOptions Handoff { get; set; } = new();
}

public sealed class TravelBusinessProfileOptions
{
    public string Overview { get; set; } = string.Empty;

    public string PrimaryAudience { get; set; } = string.Empty;

    public string Tone { get; set; } = string.Empty;

    public List<string> SupportedLanguages { get; set; } = [];

    public List<string> Specialties { get; set; } = [];

    public List<string> UpsellOffers { get; set; } = [];
}

public sealed class TravelLeadCaptureOptions
{
    public List<string> RequiredFields { get; set; } = [];

    public List<string> OptionalFields { get; set; } = [];
}

public sealed class TravelCapabilityOptions
{
    public bool ShowSightseeingImages { get; set; } = true;

    public bool ShowHotelImages { get; set; } = true;

    public bool CreateBookingInquiries { get; set; } = true;

    public bool CollectPostTripFeedback { get; set; } = true;

    public bool SuggestUpsells { get; set; } = true;
}

public sealed class TravelBusinessHoursOptions
{
    public string TimeZone { get; set; } = string.Empty;

    public Dictionary<string, string> WeeklySchedule { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TravelHandoffOptions
{
    public bool Enabled { get; set; } = true;

    public string Destination { get; set; } = string.Empty;

    public string ResponseWindow { get; set; } = string.Empty;

    public List<string> Rules { get; set; } = [];
}

internal static class TravelCustomerProfileBinder
{
    public static string ResolveRequiredMcpServerName(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var customerProfileSection = configuration.GetRequiredSection(TravelCustomerProfileOptions.SectionName);
        var mcpServerName = customerProfileSection[nameof(TravelCustomerProfileOptions.McpServerName)];

        if (string.IsNullOrWhiteSpace(mcpServerName))
        {
            throw new OptionsValidationException(
                Options.DefaultName,
                typeof(TravelCustomerProfileOptions),
                [$"{nameof(TravelCustomerProfileOptions.McpServerName)} is required."]);
        }

        return mcpServerName;
    }
}

internal sealed class TravelCustomerProfileOptionsValidator : IValidateOptions<TravelCustomerProfileOptions>
{
    public ValidateOptionsResult Validate(string? name, TravelCustomerProfileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        ValidateRequired(options.DisplayName, nameof(options.DisplayName), failures);
        ValidateRequired(options.AgentName, nameof(options.AgentName), failures);
        ValidateRequired(options.AgentDescription, nameof(options.AgentDescription), failures);
        ValidateRequired(options.McpServerName, nameof(options.McpServerName), failures);
        ValidateRequired(options.AssetPathPrefix, nameof(options.AssetPathPrefix), failures);
        ValidateRequired(options.PreviewTitle, nameof(options.PreviewTitle), failures);
        ValidateRequired(options.PreviewDescription, nameof(options.PreviewDescription), failures);
        ValidateRequired(options.PromptFileName, nameof(options.PromptFileName), failures);
        ValidateRequired(options.BusinessProfile.Overview, $"{nameof(options.BusinessProfile)}.{nameof(options.BusinessProfile.Overview)}", failures);
        ValidateRequired(options.BusinessProfile.PrimaryAudience, $"{nameof(options.BusinessProfile)}.{nameof(options.BusinessProfile.PrimaryAudience)}", failures);
        ValidateRequired(options.BusinessProfile.Tone, $"{nameof(options.BusinessProfile)}.{nameof(options.BusinessProfile.Tone)}", failures);
        ValidateRequired(options.BusinessHours.TimeZone, $"{nameof(options.BusinessHours)}.{nameof(options.BusinessHours.TimeZone)}", failures);
        ValidateRequired(options.Handoff.Destination, $"{nameof(options.Handoff)}.{nameof(options.Handoff.Destination)}", failures);
        ValidateRequired(options.Handoff.ResponseWindow, $"{nameof(options.Handoff)}.{nameof(options.Handoff.ResponseWindow)}", failures);

        if (!options.AssetPathPrefix.StartsWith("/", StringComparison.Ordinal))
            failures.Add($"{nameof(options.AssetPathPrefix)} must start with '/'.");

        if (Path.IsPathRooted(options.PromptFileName) || ContainsTraversal(options.PromptFileName))
            failures.Add($"{nameof(options.PromptFileName)} must stay inside the configuration folder.");

        if (options.BusinessProfile.Specialties.Count == 0)
            failures.Add($"{nameof(options.BusinessProfile)}.{nameof(options.BusinessProfile.Specialties)} must contain at least one value.");

        if (options.BusinessProfile.SupportedLanguages.Count == 0)
            failures.Add($"{nameof(options.BusinessProfile)}.{nameof(options.BusinessProfile.SupportedLanguages)} must contain at least one value.");

        if (options.LeadCapture.RequiredFields.Count == 0)
            failures.Add($"{nameof(options.LeadCapture)}.{nameof(options.LeadCapture.RequiredFields)} must contain at least one value.");

        if (options.BusinessHours.WeeklySchedule.Count == 0)
            failures.Add($"{nameof(options.BusinessHours)}.{nameof(options.BusinessHours.WeeklySchedule)} must contain at least one value.");

        if (options.Handoff.Enabled && options.Handoff.Rules.Count == 0)
            failures.Add($"{nameof(options.Handoff)}.{nameof(options.Handoff.Rules)} must contain at least one value when handoff is enabled.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateRequired(string? value, string propertyName, ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
            failures.Add($"{propertyName} is required.");
    }

    private static bool ContainsTraversal(string path)
    {
        return path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.None)
            .Any(segment => segment.Equals("..", StringComparison.Ordinal));
    }
}
