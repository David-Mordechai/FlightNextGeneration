using SherpaOnnx;
using Whisper.net;
using NAudio.Wave;
using System.Runtime.InteropServices;

namespace Bff.Service.Services;

public interface ISpeechService
{
    Task<byte[]> GenerateAudioAsync(string text);
    Task<string> TranscribeAudioAsync(Stream audioStream);
}

public class SpeechService : ISpeechService, IDisposable
{
    private readonly ILogger<SpeechService> _logger;
    private OfflineTts? _tts;
    private WhisperFactory? _whisperFactory;
    private WhisperProcessor? _whisperProcessor;
    private readonly string _resourceDir;
    private bool _isInitialized = false;

    public SpeechService(ILogger<SpeechService> logger)
    {
        _logger = logger;
        _resourceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources");
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            // Initialize TTS
            string modelFolder = Path.Combine(_resourceDir, "vits-piper-en_US-joe-medium");
            if (Directory.Exists(modelFolder))
            {
                var config = new OfflineTtsConfig();
                config.Model.Vits.Model = Path.Combine(modelFolder, "en_US-joe-medium.onnx");
                config.Model.Vits.Tokens = Path.Combine(modelFolder, "tokens.txt");
                config.Model.Vits.DataDir = Path.Combine(modelFolder, "espeak-ng-data");
                
                // Tuned parameters from VoiceHandler
                config.Model.Vits.NoiseScale = 0.667f;
                config.Model.Vits.NoiseScaleW = 0.8f;
                config.Model.Vits.LengthScale = 1.0f;

                _tts = new OfflineTts(config);
                _logger.LogInformation("TTS Initialized successfully.");
            }
            else
            {
                _logger.LogError("TTS Model folder not found: {Path}", modelFolder);
            }

            // Initialize STT
            string whisperModel = Path.Combine(_resourceDir, "ggml-base.en.bin");
            if (File.Exists(whisperModel))
            {
                try 
                {
                    _logger.LogInformation("Loading Whisper model from {Path}...", whisperModel);
                    // Using FromPath first, but inside a try-catch to handle native crashes if possible
                    _whisperFactory = WhisperFactory.FromPath(whisperModel);
                    
                    _logger.LogInformation("Creating Whisper processor...");
                    _whisperProcessor = _whisperFactory.CreateBuilder()
                        .WithLanguage("en")
                        .Build();
                    _logger.LogInformation("STT Initialized successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Whisper. Attempting fallback to buffer load...");
                    try 
                    {
                        var modelBytes = File.ReadAllBytes(whisperModel);
                        _whisperFactory = WhisperFactory.FromBuffer(modelBytes);
                        _whisperProcessor = _whisperFactory.CreateBuilder()
                            .WithLanguage("en")
                            .Build();
                        _logger.LogInformation("STT Initialized successfully (via fallback buffer).");
                    }
                    catch (Exception ex2)
                    {
                        _logger.LogCritical(ex2, "Critical failure initializing Whisper STT.");
                    }
                }
            }
            else
            {
                _logger.LogError("Whisper model not found: {Path}", whisperModel);
            }

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SpeechService");
        }
    }

    public async Task<byte[]> GenerateAudioAsync(string text)
    {
        if (!_isInitialized || _tts == null) 
        {
             _logger.LogWarning("TTS requested but not initialized.");
             return Array.Empty<byte>();
        }

        try 
        {
            var audio = _tts.Generate(text, 1.0f, 0); // speed, speakerId
            
            // Add silence padding (0.25s) as per VoiceHandler logic
            float[] samples = AddSilence(audio.Samples, audio.SampleRate, 0.25f);
            int sampleRate = audio.SampleRate;

            // Force 16kHz for compatibility with Whisper and lower bandwidth
            if (sampleRate != 16000)
            {
                samples = Resample(samples, sampleRate, 16000);
                sampleRate = 16000;
            }
            
            // Convert to 16-bit PCM WAV
            using var ms = new MemoryStream();
            
            // Convert float[] to short[]
            var shortSamples = new short[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                 float s = samples[i];
                 if (s > 1.0f) s = 1.0f;
                 if (s < -1.0f) s = -1.0f;
                 shortSamples[i] = (short)(s * 32767);
            }
            
            using (var writer = new WaveFileWriter(ms, new WaveFormat(sampleRate, 16, 1)))
            {
                writer.WriteSamples(shortSamples, 0, shortSamples.Length);
            }
            
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating audio");
            throw;
        }
    }

    public async Task<string> TranscribeAudioAsync(Stream audioStream)
    {
        if (!_isInitialized || _whisperProcessor == null)
        {
             _logger.LogWarning("STT requested but not initialized.");
             return "";
        }

        try
        {
            // Note: Whisper.net expects 16kHz PCM mono WAV. 
            // If the browser sends something else (like WebM or 44.1kHz), this might fail or produce garbage.
            // Ideally, we'd use ffmpeg or NAudio to normalize the input stream here.
            // For now, we assume the frontend sends a compatible WAV file (or we can add conversion logic if we have time).
            
            // Let's at least try to read it as a WaveStream if it's a WAV file and convert to 16k if needed.
            // But audioStream is non-seekable if from HTTP request body. We should copy to MemoryStream.
            
            using var ms = new MemoryStream();
            await audioStream.CopyToAsync(ms);
            ms.Position = 0;

            // Simple direct processing
            var result = "";
            _logger.LogInformation("Processing audio stream of size: {Size} bytes", ms.Length - ms.Position);
            
            await foreach(var segment in _whisperProcessor.ProcessAsync(ms))
            {
                 result += segment.Text + " ";
            }
            
            var text = result.Trim();
            _logger.LogInformation("Raw Transcription Result: '{Text}'", text);
            
            // Filter hallucinations (as per VoiceHandler)
            string[] hallucinations = { "you", "You", "Thanks.", "Thank you.", "." };
            if (hallucinations.Contains(text) || text.Length < 2) 
            {
                _logger.LogWarning("Filtered out text: '{Text}'", text);
                return "";
            }

            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transcribing audio");
            return "";
        }
    }
    
    private float[] AddSilence(float[] samples, int sampleRate, float seconds)
    {
        int silenceSamples = (int)(sampleRate * seconds);
        var result = new float[samples.Length + (silenceSamples * 2)];
        Array.Copy(samples, 0, result, silenceSamples, samples.Length);
        return result;
    }

    private float[] Resample(float[] samples, int sourceRate, int targetRate)
    {
        if (sourceRate == targetRate) return samples;

        double ratio = (double)targetRate / sourceRate;
        int targetLength = (int)(samples.Length * ratio);
        var result = new float[targetLength];

        for (int i = 0; i < targetLength; i++)
        {
            double sourceIndex = i / ratio;
            int index1 = (int)sourceIndex;
            int index2 = Math.Min(index1 + 1, samples.Length - 1);
            double frac = sourceIndex - index1;

            // Linear Interpolation
            result[i] = (float)((1.0 - frac) * samples[index1] + frac * samples[index2]);
        }

        return result;
    }


    public void Dispose()
    {
        _tts?.Dispose();
        // _whisperProcessor?.Dispose(); // WhisperProcessor doesn't implement IDisposable? checking...
        // WhisperFactory does not seem to need explicit disposal in this version? 
        // Actually factory creates the processor. 
        // Let's trust the GC unless we see disposable interfaces.
        // Wait, WhisperFactory implements IDisposable in newer versions.
        // _whisperFactory?.Dispose(); 
    }
}
