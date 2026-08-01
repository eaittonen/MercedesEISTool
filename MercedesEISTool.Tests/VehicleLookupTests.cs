using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MercedesEISTool.Server.Options;
using MercedesEISTool.Server.Services;

namespace MercedesEISTool.Tests;

public sealed class VehicleLookupTests
{
    [Fact]
    public async Task Lookup_CachesResultsByRegistration()
    {
        var handler = new StubHttpMessageHandler("""
        {
          "registration": "ABC-123",
          "vin": "WDB00000000000000",
          "manufacturer": "Mercedes-Benz",
          "model": "C 200",
          "year": 2020,
          "fuel": "Diesel"
        }
        """);
        var factory = new StubHttpClientFactory(new HttpClient(handler));
        var options = Options.Create(new VehicleLookupOptions
        {
            RapidApiBaseUrl = "https://example.test",
            RapidApiHost = "example.test",
            RapidApiKey = "test-key"
        });
        var cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        var provider = new RapidApiVehicleLookupProvider(factory, options, cache, NullLogger<RapidApiVehicleLookupProvider>.Instance);

        var first = await provider.LookupAsync("ABC-123", CancellationToken.None);
        var second = await provider.LookupAsync("abc 123", CancellationToken.None);

        Assert.Equal("ABC-123", first.Registration);
        Assert.Equal("WDB00000000000000", first.Vin);
        Assert.Equal("ABC-123", second.Registration);
        Assert.Equal(1, handler.RequestCount);
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

        public StubHttpMessageHandler(string payload)
        {
            _payload = payload;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_payload, Encoding.UTF8, "application/json")
            });
        }
    }
}
