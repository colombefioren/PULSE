using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public class CorrelationIdTests(PulseApi factory)
{
    private const string Header = "X-Correlation-Id";
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task A_correlation_id_is_generated_when_the_caller_sends_none()
    {
        var response = await _client.GetAsync("/health/live");

        response.Headers.GetValues(Header).Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_callers_correlation_id_is_echoed_back()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(Header, "trace-123");

        var response = await _client.SendAsync(request);

        response.Headers.GetValues(Header).Should().ContainSingle().Which.Should().Be("trace-123");
    }

    [Fact]
    public async Task An_oversized_correlation_id_is_replaced_instead_of_trusted()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(Header, new string('x', 200));

        var response = await _client.SendAsync(request);

        response.Headers.GetValues(Header).Single().Length.Should().BeLessThanOrEqualTo(64);
    }
}
