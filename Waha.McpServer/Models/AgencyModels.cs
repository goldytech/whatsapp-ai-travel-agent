namespace Waha.McpServer.Models;

public record AgencyInfo(
    string Name, string Tagline, string Description,
    AgencyContact Contact, Dictionary<string, string> Social,
    AgencyPayment Payment, CancellationPolicy CancellationPolicy,
    GroupDiscountTier[] GroupDiscountTiers
);
public record AgencyContact(string Phone, string Whatsapp, string Email, string Website, string Office, string OfficeHours);
public record AgencyPayment(string BankName, string AccountName, string AccountNumber, string Ifsc, string UpiId);
public record CancellationPolicy(CancellationTier[] Tiers, string Note);
public record CancellationTier(int DaysBeforeDeparture, int RefundPercent, string Label);
public record GroupDiscountTier(int MinPax, int MaxPax, int DiscountPercent);
