using Microsoft.AspNetCore.Mvc;
using Bff.Service.Services;

namespace Bff.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TtsController : ControllerBase
{
    private readonly ISpeechService _speechService;
    private readonly ILogger<TtsController> _logger;

    public TtsController(ISpeechService speechService, ILogger<TtsController> logger)
    {
        _speechService = speechService;
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
            var audioBytes = await _speechService.GenerateAudioAsync(text);
            
            if (audioBytes == null || audioBytes.Length == 0)
            {
                 _logger.LogError("TTS Service returned empty audio.");
                 return StatusCode(500, "TTS Generation Failed");
            }

            // Determine content type: Google usually returns MP3, Offline returns WAV
            string contentType = "audio/mpeg"; 
            if (audioBytes.Length > 3 && 
                audioBytes[0] == 'R' && audioBytes[1] == 'I' && audioBytes[2] == 'F' && audioBytes[3] == 'F')
            {
                contentType = "audio/wav";
            }

            return File(audioBytes, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate TTS");
            return StatusCode(500, "Internal TTS Error");
        }
    }

    [HttpPost("transcribe")]
    public async Task<IActionResult> Transcribe(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        try
        {
            using var stream = file.OpenReadStream();
            var text = await _speechService.TranscribeAudioAsync(stream);
            return Ok(new { text });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed");
            return StatusCode(500, "Internal Server Error");
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetAiStatus([FromServices] Services.AiChatService chatService)
    {
        var isReady = await chatService.CheckReadinessAsync();
        return Ok(new { ai_ready = isReady });
    }
}
