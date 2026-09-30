using AgenticUrlShortener.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgenticUrlShortener.Api.Controllers;

[ApiController]
public sealed class RedirectController(IUrlShortenerService service) : ControllerBase
{
    [HttpGet("/{code}")]
    public async Task<IActionResult> Go(string code)
    {
        var item = await service.ResolveAsync(code, countClick: true);
        return item is null ? NotFound() : Redirect(item.OriginalUrl);
    }
}
