using Xunit;
using AgenticUrlShortener.Api.Repositories;
using AgenticUrlShortener.Api.Services;

namespace AgenticUrlShortener.UnitTests;

public sealed class UrlShortenerTests
{
    [Fact]
    public async Task Creates_And_Resolves_Url()
    {
        var service = new UrlShortenerService(new InMemoryUrlRepository());
        var created = await service.CreateAsync("https://example.com");

        var resolved = await service.ResolveAsync(created.Code, true);

        Assert.NotNull(resolved);
        Assert.Equal("https://example.com", resolved!.OriginalUrl);
        Assert.Equal(1, resolved.Clicks);
        Assert.NotNull(resolved.LastAccessedAt);
    }

    [Fact]
    public async Task Rejects_Invalid_Url()
    {
        var service = new UrlShortenerService(new InMemoryUrlRepository());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync("not-a-url"));
    }
}
