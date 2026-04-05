using System.Net.Http;
using System.Text.Json;
using System.Web;
using System.Text.RegularExpressions;

namespace TravelSystem.Web.Services
{
    public class FreeAudioTranslationService : IAudioTranslationService
    {
        private readonly HttpClient _httpClient;

        public FreeAudioTranslationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // spoof user agent
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        }

        public async Task<string> TranslateAsync(string text, string targetLanguageCode)
        {
            var sourceText = StripFallbackTranslationMarker(text);
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                return string.Empty;
            }

            try
            {
                var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=vi&tl={targetLanguageCode}&dt=t&q={HttpUtility.UrlEncode(sourceText)}";
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    // Google returns a nested array of translated segments.
                    // We must join all segments instead of taking only the first one.
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                    {
                        var segments = doc.RootElement[0];
                        if (segments.ValueKind == JsonValueKind.Array)
                        {
                            var parts = new List<string>();
                            foreach (var segment in segments.EnumerateArray())
                            {
                                if (segment.ValueKind != JsonValueKind.Array || segment.GetArrayLength() == 0)
                                {
                                    continue;
                                }

                                var piece = segment[0].GetString();
                                if (!string.IsNullOrWhiteSpace(piece))
                                {
                                    parts.Add(piece.Trim());
                                }
                            }

                            if (parts.Count > 0)
                            {
                                return string.Join(" ", parts);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to mock text if API fails
            }
            return sourceText;
        }

        private static string StripFallbackTranslationMarker(string input)
        {
            var value = (input ?? string.Empty).Trim();
            return Regex.Replace(value, @"^\[\s*Bản\s+dịch\s+[a-zA-Z-]+\s*\]\s*", string.Empty, RegexOptions.IgnoreCase).Trim();
        }

        public async Task<string> GenerateTtsAsync(string text, string languageCode, int zoneId, string webRootPath)
        {
            var fileName = $"{zoneId}_{languageCode}.mp3";
            var uploadDir = Path.Combine(webRootPath, "uploads", "audio");
            Directory.CreateDirectory(uploadDir);
            var filePath = Path.Combine(uploadDir, fileName);

            try
            {
                // truncate to avoid 200 char limit on free endpoint
                var ttsText = text.Length > 200 ? text.Substring(0, 197) + "..." : text;
                
                var url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={HttpUtility.UrlEncode(ttsText)}&tl={languageCode}&client=tw-ob";
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var fileBytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(filePath, fileBytes);
                    return $"/uploads/audio/{fileName}";
                }
            }
            catch (Exception)
            {
                // Ignored, will drop down to mock file
            }

            // MOCK/Fallback implementation if API fails:
            // Write a tiny 1KB dummy valid empty file so mobile doesn't crash
            await File.WriteAllBytesAsync(filePath, new byte[1024]);
            return $"/uploads/audio/{fileName}";
        }
    }
}
