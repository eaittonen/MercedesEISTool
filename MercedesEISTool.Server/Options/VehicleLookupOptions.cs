namespace MercedesEISTool.Server.Options;

public sealed class VehicleLookupOptions
{
    public const string SectionName = "VehicleLookup";

    public string RapidApiBaseUrl { get; set; } = "https://ajoneuvon-tiedot.p.rapidapi.com";
    public string RapidApiHost { get; set; } = "ajoneuvon-tiedot.p.rapidapi.com";
    public string RapidApiKey { get; set; } = string.Empty;
}
