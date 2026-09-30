using Xunit;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgenticUrlShortener.IntegrationTests;

public sealed class ApiIntegrationTests :
    IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }

    [Fact]
    public async Task Health_Returns_200()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Url_Can_Be_Created()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/urls",
            new { url = "https://example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Greenfield_Workflow_Starts_End_To_End()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/orchestration/runs",
            new
            {
                scenario = "greenfield",
                requirement = "Add expiration dates to short URLs."
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("WaitingForApproval", body);
        Assert.Contains("validation", body);
        Assert.Contains("release", body);
    }

    [Fact]
    public async Task Metrics_Endpoint_Returns_200()
    {
        var response = await _client.GetAsync("/api/orchestration/metrics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
