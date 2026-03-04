using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace Bff.Service.Services;

public class GoogleSpeechService : ISpeechService
{
    private readonly ILogger<GoogleSpeechService> _logger;
    private readonly HttpClient _httpClient;
    private readonly OfflineSpeechService _offlineFallback; // For STT fallback

    public GoogleSpeechService(ILogger<GoogleSpeechService> logger, IHttpClientFactory httpClientFactory, OfflineSpeechService offlineFallback)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _offlineFallback = offlineFallback;
        _logger.LogInformation("Google Speech Service (Unofficial API) Initialized.");
    }

    public async Task<byte[]> GenerateAudioAsync(string text)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<byte>();

            // Use the unofficial Google Translate TTS endpoint which requires no authentication key.
            // Note: This is an undocumented API and may be subject to rate limiting or changes.
            // URL format: https://translate.google.com/translate_tts?ie=UTF-8&q={text}&tl=en&client=tw-ob
            
            var encodedText = HttpUtility.UrlEncode(text);
            var url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={encodedText}&tl=en&client=tw-ob";

            _logger.LogInformation("Requesting TTS from Google Translate API...");
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Google Translate TTS failed with status code: {StatusCode}", response.StatusCode);
                // Fallback to offline TTS if Google fails?
                // The interface expects byte[], let's return empty or throw to trigger fallback logic if implemented upstream.
                // But upstream (TtsController) just returns 500 on empty.
                // Let's try offline fallback here?
                // _logger.LogWarning("Falling back to Offline TTS...");
                // return await _offlineFallback.GenerateAudioAsync(text);
                return Array.Empty<byte>();
            }

            var mp3Bytes = await response.Content.ReadAsByteArrayAsync();
            _logger.LogInformation("Received {Bytes} bytes from Google Translate API.", mp3Bytes.Length);

            return mp3Bytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating audio via Google Translate API");
            return Array.Empty<byte>();
        }
    }

    public Task<string> TranscribeAudioAsync(Stream audioStream)
    {
        // Delegate STT to the offline service (Whisper) as Google STT requires cloud credentials.
        return _offlineFallback.TranscribeAudioAsync(audioStream);
    }
}
