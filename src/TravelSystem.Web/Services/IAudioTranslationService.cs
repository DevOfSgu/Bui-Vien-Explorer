namespace TravelSystem.Web.Services
{
    public interface IAudioTranslationService
    {
        Task<string> TranslateAsync(string text, string targetLanguageCode);
        Task<string> GenerateTtsAsync(string text, string languageCode, int narrationId, string webRootPath);
    }
}
