namespace Waha.McpServer.Models;

public record DestinationGuide(
    string Destination, string Region,
    string[] BestMonths, string PeakSeason, string OffSeason,
    decimal AvgBudgetPerDayINR, WeatherInfo Weather,
    string[] TopAttractions, string[] Cuisine,
    VisaPolicy VisaPolicy, PackingEssentials PackingEssentials
);
public record WeatherInfo(string WinterTemp, string SummerTemp, string MonsoonRainfall);
public record VisaPolicy(string IndianNationals, string ForeignNationals);
public record PackingEssentials(string[] Clothing, string[] Gear, string[] Documents, string[] Health);
