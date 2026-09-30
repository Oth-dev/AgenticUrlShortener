using AgenticUrlShortener.Api.Contracts;
using AgenticUrlShortener.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticUrlShortener.Api.Controllers;

[ApiController]
[Route("api/urls")]
public sealed class UrlsController(IUrlShortenerService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateUrlRequest request)
    {
        try
        {
            var item = await service.CreateAsync(request.Url);
            return Ok(new
            {
                item.Code,
                item.OriginalUrl,
                shortUrl = $"{Request.Scheme}://{Request.Host}/{item.Code}",
                item.CreatedAt
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{code}/analytics")]
    public async Task<IActionResult> Analytics(string code)
    {
        var item = await service.ResolveAsync(code);
        return item is null
            ? NotFound()
            : Ok(new
            {
                item.Code,
                item.OriginalUrl,
                item.Clicks,
                item.CreatedAt,
                item.LastAccessedAt
            });
    }
}
