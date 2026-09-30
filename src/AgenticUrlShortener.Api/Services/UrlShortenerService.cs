using System.Security.Cryptography;
using AgenticUrlShortener.Api.Models;
using AgenticUrlShortener.Api.Repositories;

namespace AgenticUrlShortener.Api.Services;

public interface IUrlShortenerService
{
    Task<ShortUrl> CreateAsync(string url);
    Task<ShortUrl?> ResolveAsync(string code, bool countClick = false);
}

public sealed class UrlShortenerService(IUrlRepository repository)
    : IUrlShortenerService
{
    private const string Characters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public async Task<ShortUrl> CreateAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Please provide a valid http/https URL.");
        }

        var code = await GenerateUniqueCodeAsync();

        var item = new ShortUrl
        {
            Code = code,
            OriginalUrl = url
        };

        await repository.AddAsync(item);
        return item;
    }

    public async Task<ShortUrl?> ResolveAsync(string code, bool countClick = false)
    {
        var item = await repository.GetAsync(code);

        if (item is not null && countClick)
            await repository.IncrementClickAsync(code);

        return item;
    }

    private async Task<string> GenerateUniqueCodeAsync()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(7);
            var chars = bytes.Select(b => Characters[b % Characters.Length]).ToArray();
            var code = new string(chars);

            if (!await repository.ExistsAsync(code))
                return code;
        }

        throw new InvalidOperationException("Could not generate a unique short code.");
    }
}
