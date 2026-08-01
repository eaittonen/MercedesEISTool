using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MercedesEISTool.Contracts.Models;
using MercedesEISTool.Server.Options;

namespace MercedesEISTool.Server.Services;

public sealed class RapidApiVehicleLookupProvider : IVehicleLookupProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly VehicleLookupOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RapidApiVehicleLookupProvider> _logger;

    public RapidApiVehicleLookupProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<VehicleLookupOptions> options,
        IMemoryCache cache,
        ILogger<RapidApiVehicleLookupProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(RapidApiVehicleLookupProvider));
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<VehicleInfoDto> LookupAsync(string registration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(registration))
        {
            return new VehicleInfoDto();
        }

        var normalizedRegistration = NormalizeRegistration(registration);
        if (_cache.TryGetValue(normalizedRegistration, out VehicleInfoDto? cached))
        {
            return cached ?? new VehicleInfoDto();
        }

        var requestUri = BuildRequestUri(normalizedRegistration);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("x-rapidapi-host", _options.RapidApiHost);
        request.Headers.Add("x-rapidapi-key", _options.RapidApiKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<JsonElement>(json, SerializerOptions);
            var result = NormalizePayload(payload, normalizedRegistration);
            _cache.Set(normalizedRegistration, result, TimeSpan.FromDays(30));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vehicle lookup failed for registration {Registration}", normalizedRegistration);
            return new VehicleInfoDto { Registration = normalizedRegistration };
        }
    }

    private string BuildRequestUri(string registration)
    {
        var baseUri = _options.RapidApiBaseUrl.TrimEnd('/');
        return $"{baseUri}/api/Search?m=ModuleBasic&registration={Uri.EscapeDataString(registration)}";
    }

    private static string NormalizeRegistration(string registration)
    {
        if (string.IsNullOrWhiteSpace(registration))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(registration.Length);
        foreach (var character in registration.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static VehicleInfoDto NormalizePayload(JsonElement payload, string registration)
    {
        var dto = new VehicleInfoDto
        {
            Registration = registration,
            AdditionalFields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };

        if (payload.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in payload.EnumerateObject())
            {
                var lowercaseName = property.Name.ToLowerInvariant();
                switch (lowercaseName)
                {
                    case "registration":
                    case "plate":
                    case "reg":
                        dto.Registration = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : dto.Registration;
                        break;
                    case "vin":
                    case "vehicleidentificationnumber":
                        dto.Vin = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "manufacturer":
                    case "make":
                        dto.Manufacturer = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "model":
                        dto.Model = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "type":
                        dto.Type = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "year":
                        if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var year))
                        {
                            dto.Year = year;
                        }
                        break;
                    case "fuel":
                    case "fueltype":
                        dto.Fuel = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "power":
                        dto.Power = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "engine":
                    case "enginecapacity":
                        dto.Engine = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "enginecode":
                        dto.EngineCode = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "transmission":
                        dto.Transmission = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "drivetype":
                    case "drive":
                        dto.DriveType = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "firstregistration":
                    case "first_registration":
                        dto.FirstRegistration = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "color":
                        dto.Color = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "mass":
                        dto.Mass = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "bodytype":
                    case "body":
                        dto.BodyType = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    case "inspectiondate":
                    case "inspection_date":
                        dto.InspectionDate = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        break;
                    default:
                        dto.AdditionalFields[property.Name] = property.Value.ToString();
                        break;
                }
            }
        }

        return dto;
    }
}
