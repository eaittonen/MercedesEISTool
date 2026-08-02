using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MercedesEISTool.Server.Options;
using MercedesEISTool.Server.Services;

namespace MercedesEISTool.Tests;

public sealed class VehicleLookupTests
{
    [Fact]
    public async Task Lookup_NormalizesRegistrationAndUsesExpectedRequestUri()
    {
        var handler = new StubHttpMessageHandler("""
        {
          "result": {
            "vin": "WDB00000000000000",
            "manufacturer": "Mercedes-Benz",
            "model": "C 200"
          }
        }
        """);
        var provider = CreateProvider(handler);

        var result = await provider.LookupAsync("NLU-846", CancellationToken.None);

        Assert.True(result.Found);
        Assert.Equal("NLU846", result.Registration);
        Assert.Equal("WDB00000000000000", result.Vin);
        Assert.Equal("Mercedes-Benz", result.Manufacturer);
        Assert.Equal("C 200", result.Model);
        Assert.Equal("https://example.test/api/Search?m=ModuleBasic&registration=NLU-846&reg=NLU-846", handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task Lookup_SendsRapidApiHeaders()
    {
        var handler = new StubHttpMessageHandler("""
        {
          "result": {
            "vin": "WDB00000000000000",
            "manufacturer": "Mercedes-Benz",
            "model": "C 200"
          }
        }
        """);
        var provider = CreateProvider(handler);

        await provider.LookupAsync("ABC123", CancellationToken.None);

        var requestHeaders = handler.LastRequestHeaders;
        Assert.NotNull(requestHeaders);
        var headers = requestHeaders!;
        var hostValues = headers.GetValues("x-rapidapi-host").ToArray();
        var keyValues = headers.GetValues("x-rapidapi-key").ToArray();
        Assert.Contains("example.test", hostValues);
        Assert.Contains("test-key", keyValues);
        var acceptedHeaders = headers.Accept?.ToArray() ?? Array.Empty<MediaTypeWithQualityHeaderValue>();
        Assert.NotEmpty(acceptedHeaders);
        Assert.Contains(acceptedHeaders, header => header.MediaType == "application/json");
    }

    [Fact]
    public async Task Lookup_MapsWrappedPayloadToVehicleInfoDto()
    {
        var handler = new StubHttpMessageHandler("""
        {
          "data": {
            "vehicle": {
              "registration": "ABC-123",
              "vin": "WDB00000000000001",
              "manufacturer": "Mercedes-Benz",
              "model": "E 220",
              "firstRegistration": "2020-01-01",
              "fuel": "Diesel",
              "power": "170 hp"
            }
          }
        }
        """);
        var provider = CreateProvider(handler);

        var result = await provider.LookupAsync("abc-123", CancellationToken.None);

        Assert.True(result.Found);
        Assert.Equal("ABC123", result.Registration);
        Assert.Equal("WDB00000000000001", result.Vin);
        Assert.Equal("Mercedes-Benz", result.Manufacturer);
        Assert.Equal("E 220", result.Model);
        Assert.Equal("2020-01-01", result.FirstRegistration);
        Assert.Equal("Diesel", result.Fuel);
    }

    [Fact]
    public async Task Lookup_ReturnsAuthenticationErrorFor401()
    {
        var handler = new StubHttpMessageHandler("{\"message\":\"Unauthorized\"}", HttpStatusCode.Unauthorized);
        var provider = CreateProvider(handler);

        var result = await provider.LookupAsync("ABC123", CancellationToken.None);

        Assert.False(result.Found);
        Assert.Equal("authentication_failed", result.ErrorCode);
        Assert.Contains("authentication", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_DoesNotCacheEmptyResultAsSuccess()
    {
        var handler = new StubHttpMessageHandler("{\"message\":\"No vehicle found\"}", HttpStatusCode.OK);
        var provider = CreateProvider(handler);

        var first = await provider.LookupAsync("ABC123", CancellationToken.None);
        var second = await provider.LookupAsync("ABC123", CancellationToken.None);

        Assert.False(first.Found);
        Assert.False(second.Found);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Lookup_ReturnsQuotaErrorFor429()
    {
        var handler = new StubHttpMessageHandler("{\"message\":\"Too many requests\"}", HttpStatusCode.TooManyRequests);
        var provider = CreateProvider(handler);

        var result = await provider.LookupAsync("ABC123", CancellationToken.None);

        Assert.False(result.Found);
        Assert.Equal("quota_exceeded", result.ErrorCode);
        Assert.Contains("quota", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_PreservesRegistrationAndAcceptsAlternativeVehicleFieldNames()
    {
        var handler = new StubHttpMessageHandler("""
        {
          "result": {
            "registrationNumber": "ANY-363",
            "vin": "WDB00000000000000",
            "vehicleManufacturer": "Mercedes-Benz",
            "vehicleModel": "C 200",
            "fuelTypeName": "Diesel"
          }
        }
        """);
        var provider = CreateProvider(handler);

        var result = await provider.LookupAsync("ANY-363", CancellationToken.None);

        Assert.True(result.Found);
        Assert.Equal("ANY363", result.Registration);
        Assert.Equal("WDB00000000000000", result.Vin);
        Assert.Equal("Mercedes-Benz", result.Manufacturer);
        Assert.Equal("C 200", result.Model);
        Assert.Equal("Diesel", result.Fuel);
        Assert.Equal("https://example.test/api/Search?m=ModuleBasic&registration=ANY-363&reg=ANY-363", handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task Lookup_DoesNotExposeApiKeyInVehicleInfoDto()
    {
        var handler = new StubHttpMessageHandler("""
        {
          "result": {
            "vin": "WDB00000000000000",
            "manufacturer": "Mercedes-Benz",
            "model": "C 200"
          }
        }
        """);
        var provider = CreateProvider(handler);

        var result = await provider.LookupAsync("ABC123", CancellationToken.None);

        Assert.False(result.AdditionalFields.ContainsKey("RapidApiKey"));
        Assert.False(result.AdditionalFields.ContainsKey("x-rapidapi-key"));
        Assert.DoesNotContain("test-key", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static RapidApiVehicleLookupProvider CreateProvider(StubHttpMessageHandler handler)
    {
        var factory = new StubHttpClientFactory(new HttpClient(handler));
        var options = Options.Create(new VehicleLookupOptions
        {
            RapidApiBaseUrl = "https://example.test",
            RapidApiHost = "example.test",
            RapidApiKey = "test-key"
        });
        var cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        return new RapidApiVehicleLookupProvider(factory, options, cache, NullLogger<RapidApiVehicleLookupProvider>.Instance);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public StubHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public HttpClient CreateClient(string name) => _httpClient;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _payload;
        private readonly HttpStatusCode _statusCode;

        public StubHttpMessageHandler(string payload, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _payload = payload;
            _statusCode = statusCode;
        }

        public int RequestCount { get; private set; }
        public Uri? LastRequestUri { get; private set; }
        public HttpRequestHeaders? LastRequestHeaders { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;
            LastRequestHeaders = request.Headers;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_payload, Encoding.UTF8, "application/json")
            });
        }
    }
}
