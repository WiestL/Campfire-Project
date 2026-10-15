using Campfire.Api.Options;
using Campfire.Api.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace Campfire.Api.Controllers;

/// <summary>
/// Serves locally stored media (the LocalMediaStorage dev fallback) behind the
/// same signed-URL check a real Bunny.net Pull Zone would enforce. Not used
/// once Cdn:Provider is switched to "Bunny" — the Pull Zone serves those files
/// directly.
/// </summary>
[ApiController]
[Route("media")]
public class MediaController(IWebHostEnvironment env, IOptions<CdnOptions> options) : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    [HttpGet("{fileName}")]
    public IActionResult Get(string fileName, [FromQuery] string? token, [FromQuery] long expires)
    {
        var urlPath = "/" + fileName;
        if (!BunnyUrlSigner.Validate(options.Value.SigningKey, urlPath, token, expires))
        {
            return Unauthorized("Invalid or expired signed URL.");
        }

        var filePath = Path.Combine(env.WebRootPath, "media", fileName);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        if (!ContentTypeProvider.TryGetContentType(filePath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }
}
