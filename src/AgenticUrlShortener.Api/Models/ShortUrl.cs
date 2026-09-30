namespace AgenticUrlShortener.Api.Models;

public sealed class ShortUrl
{
    public required string Code { get; init; }
    public required string OriginalUrl { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public int Clicks { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }
}
