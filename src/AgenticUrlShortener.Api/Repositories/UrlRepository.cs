using System.Collections.Concurrent;
using AgenticUrlShortener.Api.Models;

namespace AgenticUrlShortener.Api.Repositories;

public interface IUrlRepository
{
    Task AddAsync(ShortUrl item);
    Task<ShortUrl?> GetAsync(string code);
    Task<bool> ExistsAsync(string code);
    Task IncrementClickAsync(string code);
}

public sealed class InMemoryUrlRepository : IUrlRepository
{
    private readonly ConcurrentDictionary<string, ShortUrl> _items =
        new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(ShortUrl item)
    {
        if (!_items.TryAdd(item.Code, item))
            throw new InvalidOperationException("Short code already exists.");

        return Task.CompletedTask;
    }

    public Task<ShortUrl?> GetAsync(string code)
    {
        _items.TryGetValue(code, out var item);
        return Task.FromResult(item);
    }

    public Task<bool> ExistsAsync(string code) =>
        Task.FromResult(_items.ContainsKey(code));

    public Task IncrementClickAsync(string code)
    {
        if (_items.TryGetValue(code, out var item))
        {
            lock (item)
            {
                item.Clicks++;
                item.LastAccessedAt = DateTimeOffset.UtcNow;
            }
        }
        return Task.CompletedTask;
    }
}
