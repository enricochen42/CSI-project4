using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace FlashcardApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TextStorageController : ControllerBase
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<TextStorageController> _logger;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30); // Store for 30 minutes

    public TextStorageController(IMemoryCache memoryCache, ILogger<TextStorageController> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Store text content temporarily and return a token
    /// </summary>
    [HttpPost("store")]
    [ProducesResponseType(typeof(StoreTextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult StoreText([FromBody] StoreTextRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest("Text content is required");
            }

            // Generate a unique token
            var token = Guid.NewGuid().ToString();

            // Store in cache with expiration
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration,
                SlidingExpiration = TimeSpan.FromMinutes(10)
            };

            _memoryCache.Set(token, request.Text, cacheEntryOptions);

            _logger.LogInformation("Stored text with token {Token}, length: {Length}", token, request.Text.Length);

            return Ok(new StoreTextResponse(token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing text");
            return StatusCode(500, "An error occurred while storing text");
        }
    }

    /// <summary>
    /// Retrieve text content using a token
    /// </summary>
    [HttpGet("retrieve/{token}")]
    [ProducesResponseType(typeof(RetrieveTextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult RetrieveText(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest("Token is required");
            }

            if (_memoryCache.TryGetValue(token, out string? text))
            {
                return Ok(new RetrieveTextResponse(text ?? string.Empty));
            }

            return NotFound("Text content not found or expired. Please upload your slides again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving text with token {Token}", token);
            return StatusCode(500, "An error occurred while retrieving text");
        }
    }
}

public record StoreTextRequest(string Text);
public record StoreTextResponse(string Token);
public record RetrieveTextResponse(string Text);

