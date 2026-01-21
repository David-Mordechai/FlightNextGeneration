using Microsoft.AspNetCore.Mvc;

namespace Bff.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TtsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TtsController> _logger;

    public TtsController(IHttpClientFactory httpClientFactory, ILogger<TtsController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetSpeech([FromQuery] string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest("Text is required");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            
            // Google Translate TTS - Most reliable free source
            // client=tw-ob is the public endpoint used by the Android app
            var url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={Uri.EscapeDataString(text)}&tl=en&client=tw-ob";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            // Critical: Spoof standard browser headers
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Referer", "https://translate.google.com/");
            request.Headers.Add("Accept", "*/*");

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TTS Provider failed with status: {Status}", response.StatusCode);
                return StatusCode((int)response.StatusCode, "TTS Provider failed");
            }

            var stream = await response.Content.ReadAsStreamAsync();
            return File(stream, "audio/mpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy TTS request");
            return StatusCode(500, "Internal TTS Error");
        }
    }
}
