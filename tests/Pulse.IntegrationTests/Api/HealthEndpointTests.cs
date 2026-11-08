using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pulse.IntegrationTests.Api;

public class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_answer_200_when_the_service_is_healthy(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unknown_routes_answer_404()
    {
        var response = await _client.GetAsync("/health/nope");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
