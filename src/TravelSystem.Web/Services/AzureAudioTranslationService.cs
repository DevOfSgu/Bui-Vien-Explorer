using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TravelSystem.Web.Services;

public class AzureAudioTranslationService : IAudioTranslationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureAudioTranslationService> _logger;
    private readonly string _translatorKey;
    private readonly string _translatorRegion;
    private readonly string _translatorEndpoint;
    private readonly string _speechKey;
    private readonly string _speechRegion;

    public AzureAudioTranslationService(HttpClient httpClient, IConfiguration configuration, ILogger<AzureAudioTranslationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // NOTE: Trong Docker, dấu ":" phải dùng "__" (double underscore) trong biến môi trường.
        // Ví dụ: AzureAi__Translator__Key, AzureAi__Speech__Key
        _translatorKey = ResolveConfigValue(configuration, "AzureAi:Translator:Key", "AZURE_TRANSLATOR_KEY");
        _translatorRegion = ResolveConfigValue(configuration, "AzureAi:Translator:Region", "AZURE_TRANSLATOR_REGION");
        _translatorEndpoint = ResolveConfigValue(configuration, "AzureAi:Translator:Endpoint", "AZURE_TRANSLATOR_ENDPOINT", "https://api.cognitive.microsofttranslator.com");

        _speechKey = ResolveConfigValue(configuration, "AzureAi:Speech:Key", "AZURE_SPEECH_KEY");
        _speechRegion = ResolveConfigValue(configuration, "AzureAi:Speech:Region", "AZURE_SPEECH_REGION");
    }

    private static string ResolveConfigValue(IConfiguration configuration, string primaryKey, string envFallbackKey, string defaultValue = "")
    {
        var primary = configuration[primaryKey];
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary;
        }

        var envValue = Environment.GetEnvironmentVariable(envFallbackKey);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue;
        }

        return defaultValue;
    }

    public async Task<string> TranslateAsync(string text, string targetLanguageCode)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(_translatorKey) || string.IsNullOrWhiteSpace(_translatorRegion))
        {
            _logger.LogWarning("[AZURE_TRANSLATE] Missing Translator configuration for target={TargetLanguage}.", targetLanguageCode);
            return $"[Bản dịch {targetLanguageCode}] {text}";
        }

        var language = NormalizeLanguage(targetLanguageCode);
        var endpoint = _translatorEndpoint.TrimEnd('/');
        var isGlobalTranslatorEndpoint = endpoint.Contains("api.cognitive.microsofttranslator.com", StringComparison.OrdinalIgnoreCase);
        var path = isGlobalTranslatorEndpoint
            ? "translate"
            : "translator/text/v3.0/translate";
        var url = $"{endpoint}/{path}?api-version=3.0&from=vi&to={Uri.EscapeDataString(language)}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new[] { new { Text = text } })
        };

        request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", _translatorKey);
        request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Region", _translatorRegion);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorPayload = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[AZURE_TRANSLATE] API failed status={StatusCode}, region={Region}, target={TargetLanguage}, body={Body}",
                (int)response.StatusCode,
                _translatorRegion,
                language,
                Truncate(errorPayload, 300));
            return $"[Bản dịch {targetLanguageCode}] {text}";
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return text;
        }

        var first = doc.RootElement[0];
        if (!first.TryGetProperty("translations", out var translations) || translations.ValueKind != JsonValueKind.Array)
        {
            return text;
        }

        var translated = translations.EnumerateArray()
            .Select(x => x.TryGetProperty("text", out var textProp) ? textProp.GetString() : null)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        _logger.LogInformation("[AZURE_TRANSLATE] Success target={TargetLanguage}, translatedLength={Length}",
            language,
            translated?.Length ?? 0);

        return string.IsNullOrWhiteSpace(translated)
            ? $"[Bản dịch {targetLanguageCode}] {text}"
            : translated;
    }

    public async Task<string> GenerateTtsAsync(string text, string languageCode, int zoneId, string webRootPath)
    {
        var fileName = $"{zoneId}_{NormalizeLanguage(languageCode)}.mp3";
        var uploadDir = Path.Combine(webRootPath, "uploads", "audio");
        Directory.CreateDirectory(uploadDir);
        var filePath = Path.Combine(uploadDir, fileName);

        if (string.IsNullOrWhiteSpace(_speechKey) || string.IsNullOrWhiteSpace(_speechRegion))
        {
            _logger.LogWarning("[AZURE_TTS] Missing Speech configuration zone={ZoneId}, lang={Language}.", zoneId, NormalizeLanguage(languageCode));
            return await HandleTtsFailureAsync(filePath, fileName, zoneId, languageCode, "missing_configuration");
        }

        var voice = ResolveVoiceName(languageCode);
        var locale = GetSpeechLocale(languageCode);
        var endpoint = $"https://{_speechRegion}.tts.speech.microsoft.com/cognitiveservices/v1";

        // FIX: Tính độ dài SSML template trước để trừ ra khỏi giới hạn 1000 ký tự của Azure REST API.
        // Nếu không trừ, tổng (text + SSML tags) có thể vượt 1000 → Azure tự cắt audio, không báo lỗi.
        var ssmlTemplate = $"<speak version='1.0' xml:lang='{locale}'><voice xml:lang='{locale}' xml:gender='Female' name='{voice}'></voice></speak>";
        var maxTextLength = Math.Max(100, 900 - ssmlTemplate.Length); // 900 để an toàn, tối thiểu 100

        var chunks = SplitTextForTts(text, maxTextLength);
        var allBytes = new List<byte[]>(chunks.Count);

        _logger.LogInformation("[AZURE_TTS] Splitting zone={ZoneId}, lang={Language}, totalLength={Total}, chunkCount={Count}, maxTextLength={Max}",
            zoneId,
            NormalizeLanguage(languageCode),
            text?.Length ?? 0,
            chunks.Count,
            maxTextLength);

        for (var i = 0; i < chunks.Count; i++)
        {
            var escapedText = EscapeXml(chunks[i]);
            var ssml = $"<speak version='1.0' xml:lang='{locale}'><voice xml:lang='{locale}' xml:gender='Female' name='{voice}'>{escapedText}</voice></speak>";

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml")
            };

            request.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", _speechKey);
            request.Headers.TryAddWithoutValidation("X-Microsoft-OutputFormat", "audio-16khz-128kbitrate-mono-mp3");
            request.Headers.TryAddWithoutValidation("User-Agent", "TravelSystem.Web");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorPayload = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[AZURE_TTS] API failed status={StatusCode}, region={Region}, zone={ZoneId}, lang={Language}, voice={Voice}, chunk={ChunkIndex}/{ChunkCount}, body={Body}",
                    (int)response.StatusCode,
                    _speechRegion,
                    zoneId,
                    NormalizeLanguage(languageCode),
                    voice,
                    i + 1,
                    chunks.Count,
                    Truncate(errorPayload, 300));
                return await HandleTtsFailureAsync(filePath, fileName, zoneId, languageCode, $"api_failed_{(int)response.StatusCode}");
            }

            allBytes.Add(await response.Content.ReadAsByteArrayAsync());
        }

        var bytes = ConcatenateMp3Chunks(allBytes);
        await File.WriteAllBytesAsync(filePath, bytes);
        _logger.LogInformation("[AZURE_TTS] Success zone={ZoneId}, lang={Language}, voice={Voice}, bytes={Bytes}, file={File}",
            zoneId,
            NormalizeLanguage(languageCode),
            voice,
            bytes.Length,
            fileName);
        return $"/uploads/audio/{fileName}";
    }

    private async Task<string> HandleTtsFailureAsync(string filePath, string fileName, int zoneId, string languageCode, string reason)
    {
        if (File.Exists(filePath))
        {
            try
            {
                var info = new FileInfo(filePath);
                if (info.Length > 2048)
                {
                    _logger.LogWarning("[AZURE_TTS] Reusing previous audio file zone={ZoneId}, lang={Language}, bytes={Bytes}, reason={Reason}",
                        zoneId,
                        NormalizeLanguage(languageCode),
                        info.Length,
                        reason);
                    return $"/uploads/audio/{fileName}";
                }
            }
            catch
            {
            }
        }

        _logger.LogWarning("[AZURE_TTS] No reusable audio file zone={ZoneId}, lang={Language}, reason={Reason}",
            zoneId,
            NormalizeLanguage(languageCode),
            reason);

        // Keep return shape stable but avoid creating invalid placeholder MP3 files.
        await Task.CompletedTask;
        return string.Empty;
    }

    private static List<string> SplitTextForTts(string? text, int maxChunkLength)
    {
        var source = (text ?? string.Empty).Trim();
        if (source.Length <= maxChunkLength)
        {
            return new List<string> { source };
        }

        var result = new List<string>();
        var cursor = 0;

        while (cursor < source.Length)
        {
            var remaining = source.Length - cursor;
            if (remaining <= maxChunkLength)
            {
                result.Add(source[cursor..].Trim());
                break;
            }

            var take = source.Substring(cursor, maxChunkLength);
            var splitAt = Math.Max(
                take.LastIndexOfAny(new[] { '.', '!', '?', ';', ':', '\n' }),
                take.LastIndexOf(' '));

            if (splitAt < maxChunkLength / 3)
            {
                splitAt = maxChunkLength;
            }

            var chunk = source.Substring(cursor, splitAt).Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                result.Add(chunk);
            }

            cursor += splitAt;
            while (cursor < source.Length && char.IsWhiteSpace(source[cursor]))
            {
                cursor++;
            }
        }

        return result.Count == 0 ? new List<string> { source } : result;
    }

    private static byte[] ConcatenateMp3Chunks(IEnumerable<byte[]> chunks)
    {
        using var ms = new MemoryStream();
        var isFirstChunk = true;

        foreach (var chunk in chunks)
        {
            if (chunk.Length == 0)
            {
                continue;
            }

            var toWrite = isFirstChunk ? chunk : StripMp3Tags(chunk);
            if (toWrite.Length == 0)
            {
                continue;
            }

            ms.Write(toWrite, 0, toWrite.Length);
            isFirstChunk = false;
        }

        return ms.ToArray();
    }

    private static byte[] StripMp3Tags(byte[] bytes)
    {
        if (bytes.Length < 4)
        {
            return bytes;
        }

        var start = 0;
        var end = bytes.Length;

        // Remove ID3v2 tag at the beginning if present.
        if (bytes.Length >= 10 && bytes[0] == (byte)'I' && bytes[1] == (byte)'D' && bytes[2] == (byte)'3')
        {
            var size = (bytes[6] & 0x7F) << 21 | (bytes[7] & 0x7F) << 14 | (bytes[8] & 0x7F) << 7 | (bytes[9] & 0x7F);
            var headerAndTagSize = 10 + size;
            if (headerAndTagSize > 0 && headerAndTagSize < bytes.Length)
            {
                start = headerAndTagSize;
            }
        }

        // Remove ID3v1 tag at the end if present.
        if (end - start >= 128)
        {
            var tagIndex = end - 128;
            if (bytes[tagIndex] == (byte)'T' && bytes[tagIndex + 1] == (byte)'A' && bytes[tagIndex + 2] == (byte)'G')
            {
                end = tagIndex;
            }
        }

        var len = end - start;
        if (len <= 0)
        {
            return Array.Empty<byte>();
        }

        if (start == 0 && end == bytes.Length)
        {
            return bytes;
        }

        var result = new byte[len];
        Buffer.BlockCopy(bytes, start, result, 0, len);
        return result;
    }

    private static string NormalizeLanguage(string? languageCode)
    {
        return (languageCode ?? "vi").Trim().ToLowerInvariant() switch
        {
            "vi" => "vi",
            "en" => "en",
            "ja" => "ja",
            _ => "en"
        };
    }

    private static string GetSpeechLocale(string? languageCode)
    {
        return NormalizeLanguage(languageCode) switch
        {
            "vi" => "vi-VN",
            "ja" => "ja-JP",
            _ => "en-US"
        };
    }

    private static string ResolveVoiceName(string? languageCode)
    {
        return NormalizeLanguage(languageCode) switch
        {
            "vi" => "vi-VN-HoaiMyNeural",
            "ja" => "ja-JP-NanamiNeural",
            _ => "en-US-JennyNeural"
        };
    }

    private static string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value, "[<>&'\"]", match => match.Value switch
        {
            "<" => "&lt;",
            ">" => "&gt;",
            "&" => "&amp;",
            "'" => "&apos;",
            "\"" => "&quot;",
            _ => match.Value
        });
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength) + "...";
    }
}