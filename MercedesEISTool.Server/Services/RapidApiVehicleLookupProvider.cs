using System.Net;
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

    private static readonly HashSet<string> WrapperPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "result",
        "results",
        "data",
        "vehicle",
        "vehicles",
        "response",
        "item",
        "items",
        "record",
        "records"
    };

    private static readonly HashSet<string> VehicleFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "registration",
        "reg",
        "registrationnumber",
        "regnumber",
        "plate",
        "licenseplate",
        "licencenumber",
        "vin",
        "vehicleidentificationnumber",
        "manufacturer",
        "manufacturername",
        "make",
        "makename",
        "brand",
        "brandname",
        "vehiclemanufacturer",
        "model",
        "modelname",
        "modeldescription",
        "vehiclemodel",
        "type",
        "year",
        "modelyear",
        "firstregistration",
        "first_registration",
        "registrationdate",
        "fuel",
        "fueltype",
        "fueltypename",
        "power",
        "powerhp",
        "powerkw",
        "engine",
        "enginecapacity",
        "enginesize",
        "enginecode",
        "transmission",
        "drivetype",
        "drive",
        "bodytype",
        "body",
        "color",
        "mass",
        "inspectiondate",
        "inspection_date"
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
            return CreateError(string.Empty, "missing_registration", "A registration number is required.");
        }

        var normalizedRegistration = NormalizeRegistration(registration);
        if (_cache.TryGetValue(normalizedRegistration, out VehicleInfoDto? cached) && cached is not null)
        {
            return cached;
        }

        if (string.IsNullOrWhiteSpace(_options.RapidApiBaseUrl) || string.IsNullOrWhiteSpace(_options.RapidApiHost) || string.IsNullOrWhiteSpace(_options.RapidApiKey))
        {
            var notConfigured = CreateError(normalizedRegistration, "provider_not_configured", "Vehicle lookup provider is not configured.");
            _cache.Set(normalizedRegistration, notConfigured, TimeSpan.FromMinutes(_options.NegativeCacheDurationMinutes));
            return notConfigured;
        }

        var requestUri = BuildRequestUri(registration.Trim(), normalizedRegistration);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Content-Type", "application/json");
        request.Headers.Add("x-rapidapi-host", _options.RapidApiHost);
        request.Headers.Add("x-rapidapi-key", _options.RapidApiKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var providerStatus = $"{(int)response.StatusCode} {response.StatusCode}";
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                var authError = CreateError(normalizedRegistration, "authentication_failed", "RapidAPI authentication failed.", providerStatus);
                _cache.Set(normalizedRegistration, authError, TimeSpan.FromMinutes(_options.NegativeCacheDurationMinutes));
                return authError;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var quotaError = CreateError(normalizedRegistration, "quota_exceeded", "RapidAPI quota exceeded.", providerStatus);
                _cache.Set(normalizedRegistration, quotaError, TimeSpan.FromMinutes(_options.NegativeCacheDurationMinutes));
                return quotaError;
            }

            if (!response.IsSuccessStatusCode)
            {
                var providerError = CreateError(normalizedRegistration, "provider_error", "Vehicle lookup request failed.", providerStatus);
                _cache.Set(normalizedRegistration, providerError, TimeSpan.FromMinutes(_options.NegativeCacheDurationMinutes));
                return providerError;
            }

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                var emptyError = CreateError(normalizedRegistration, "no_vehicle_found", "Vehicle not found.", providerStatus, "empty-response");
                _cache.Set(normalizedRegistration, emptyError, TimeSpan.FromMinutes(_options.NegativeCacheDurationMinutes));
                return emptyError;
            }

            try
            {
                using var document = JsonDocument.Parse(rawBody);
                var result = NormalizePayload(document.RootElement, normalizedRegistration, providerStatus, rawBody);
                var cacheDuration = result.Found ? TimeSpan.FromMinutes(_options.CacheDurationMinutes) : TimeSpan.FromMinutes(_options.NegativeCacheDurationMinutes);
                _cache.Set(normalizedRegistration, result, cacheDuration);
                return result;
            }
            catch (JsonException ex)
            {
                var parseError = CreateError(normalizedRegistration, "provider_schema_mismatch", "Vehicle provider response could not be parsed.", providerStatus, "malformed-json", errorDetail: ex.Message);
                _cache.Set(normalizedRegistration, parseError, TimeSpan.FromMinutes(_options.NegativeCacheDurationMinutes));
                return parseError;
            }
        }
        catch (TaskCanceledException ex)
        {
            var timeoutError = CreateError(normalizedRegistration, "provider_timeout", "Vehicle lookup timed out.", "timed-out", "timeout", errorDetail: ex.Message);
            _cache.Set(normalizedRegistration, timeoutError, TimeSpan.FromMinutes(_options.NegativeCacheDurationMinutes));
            return timeoutError;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vehicle lookup failed for registration {Registration}", normalizedRegistration);
            var genericError = CreateError(normalizedRegistration, "provider_error", "Vehicle lookup failed.", "exception", "exception", errorDetail: ex.Message);
            _cache.Set(normalizedRegistration, genericError, TimeSpan.FromMinutes(_options.NegativeCacheDurationMinutes));
            return genericError;
        }
    }

    private string BuildRequestUri(string requestRegistration, string normalizedRegistration)
    {
        var baseUri = _options.RapidApiBaseUrl.TrimEnd('/');
        var registrationValue = string.IsNullOrWhiteSpace(requestRegistration) ? normalizedRegistration : requestRegistration.Trim();
        return $"{baseUri}/api/Search?m=ModuleBasic&registration={Uri.EscapeDataString(registrationValue)}&reg={Uri.EscapeDataString(registrationValue)}";
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

    private static VehicleInfoDto NormalizePayload(JsonElement payload, string registration, string? providerStatus, string rawBody)
    {
        var dto = new VehicleInfoDto
        {
            Registration = registration,
            Found = false,
            ProviderStatus = providerStatus,
            ParseStatus = "parsed",
            AdditionalFields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };

        var candidate = FindVehicleCandidate(payload);
        if (candidate is null)
        {
            dto.ParseStatus = "no-vehicle-data";
            dto.ErrorCode = "no_vehicle_found";
            dto.ErrorMessage = "Vehicle not found.";
            return dto;
        }

        var jsonObject = candidate.Value;
        if (jsonObject.ValueKind != JsonValueKind.Object)
        {
            dto.ParseStatus = "unsupported-payload";
            dto.ErrorCode = "provider_schema_mismatch";
            dto.ErrorMessage = "Vehicle provider response could not be parsed.";
            return dto;
        }

        foreach (var property in jsonObject.EnumerateObject())
        {
            var lowercaseName = property.Name.ToLowerInvariant();
            switch (lowercaseName)
            {
                case "registration":
                case "reg":
                case "registrationnumber":
                case "regnumber":
                case "plate":
                case "licenseplate":
                case "licenceplate":
                case "licencenumber":
                    dto.Registration = NormalizeRegistration(GetStringValue(property.Value) ?? registration);
                    break;
                case "vin":
                case "vehicleidentificationnumber":
                case "vehicleid":
                    dto.Vin = GetStringValue(property.Value);
                    break;
                case "manufacturer":
                case "manufacturername":
                case "make":
                case "makename":
                case "brand":
                case "brandname":
                case "vehiclemanufacturer":
                    dto.Manufacturer = GetStringValue(property.Value);
                    break;
                case "model":
                case "modelname":
                case "modeldescription":
                case "vehiclemodel":
                    dto.Model = GetStringValue(property.Value);
                    break;
                case "type":
                    dto.Type = GetStringValue(property.Value);
                    break;
                case "year":
                case "modelyear":
                    dto.Year = GetIntValue(property.Value);
                    break;
                case "fuel":
                case "fueltype":
                case "fueltypename":
                    dto.Fuel = GetStringValue(property.Value);
                    break;
                case "power":
                case "powerhp":
                case "powerkw":
                    dto.Power = GetStringValue(property.Value);
                    break;
                case "engine":
                case "enginecapacity":
                case "enginesize":
                    dto.Engine = GetStringValue(property.Value);
                    break;
                case "enginecode":
                    dto.EngineCode = GetStringValue(property.Value);
                    break;
                case "transmission":
                    dto.Transmission = GetStringValue(property.Value);
                    break;
                case "drivetype":
                case "drive":
                    dto.DriveType = GetStringValue(property.Value);
                    break;
                case "firstregistration":
                case "first_registration":
                case "registrationdate":
                    dto.FirstRegistration = GetStringValue(property.Value);
                    break;
                case "color":
                    dto.Color = GetStringValue(property.Value);
                    break;
                case "mass":
                    dto.Mass = GetStringValue(property.Value);
                    break;
                case "bodytype":
                case "body":
                    dto.BodyType = GetStringValue(property.Value);
                    break;
                case "inspectiondate":
                case "inspection_date":
                    dto.InspectionDate = GetStringValue(property.Value);
                    break;
                case "message":
                case "error":
                case "status":
                case "code":
                    dto.ErrorMessage ??= GetStringValue(property.Value);
                    break;
                default:
                    dto.AdditionalFields[property.Name] = property.Value.ToString();
                    break;
            }
        }

        dto.Found = HasUsefulData(dto);
        if (!dto.Found)
        {
            dto.ParseStatus = "no-vehicle-data";
            dto.ErrorCode = dto.ErrorCode ?? "no_vehicle_found";
            dto.ErrorMessage ??= "Vehicle not found.";
        }

        return dto;
    }

    private static JsonElement? FindVehicleCandidate(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (ContainsVehicleData(element))
            {
                return element;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (WrapperPropertyNames.Contains(property.Name) && (property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array))
                {
                    var candidate = FindVehicleCandidate(property.Value);
                    if (candidate.HasValue)
                    {
                        return candidate.Value;
                    }
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array)
                {
                    var candidate = FindVehicleCandidate(property.Value);
                    if (candidate.HasValue)
                    {
                        return candidate.Value;
                    }
                }
            }

            return null;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var candidate = FindVehicleCandidate(item);
                if (candidate.HasValue)
                {
                    return candidate.Value;
                }
            }
        }

        return null;
    }

    private static bool ContainsVehicleData(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (VehicleFieldNames.Contains(property.Name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUsefulData(VehicleInfoDto dto)
        => !string.IsNullOrWhiteSpace(dto.Vin)
            || !string.IsNullOrWhiteSpace(dto.Manufacturer)
            || !string.IsNullOrWhiteSpace(dto.Model)
            || !string.IsNullOrWhiteSpace(dto.FirstRegistration)
            || !string.IsNullOrWhiteSpace(dto.Fuel)
            || !string.IsNullOrWhiteSpace(dto.Power)
            || !string.IsNullOrWhiteSpace(dto.Engine)
            || !string.IsNullOrWhiteSpace(dto.BodyType)
            || dto.Year.HasValue;

    private static string? GetStringValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    private static int? GetIntValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static VehicleInfoDto CreateError(string registration, string errorCode, string errorMessage, string? providerStatus = null, string? parseStatus = null, string? errorDetail = null)
    {
        return new VehicleInfoDto
        {
            Registration = registration,
            Found = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ProviderStatus = providerStatus,
            ParseStatus = parseStatus,
            AdditionalFields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };
    }
}
