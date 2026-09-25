using System;

namespace FocusTerminal.AI.Core.Models
{
    public class AppSettings
    {
        public string AiProvider { get; set; } = "Auto"; // Auto, Ollama, OpenRouter, Custom, Gemini, Heuristic
        public OllamaConfig Ollama { get; set; } = new();
        public OpenRouterConfig OpenRouter { get; set; } = new();
        public CustomApiConfig CustomApi { get; set; } = new();
        public GeminiConfig Gemini { get; set; } = new();
        public PrivacyConfig Privacy { get; set; } = new();
        public WeatherConfig Weather { get; set; } = new();
        public AudioConfig Audio { get; set; } = new();

        // Legacy compatibility
        public string? ApiKey { get; set; }
    }

    public class OllamaConfig
    {
        public string Endpoint { get; set; } = "http://localhost:11434";
        public string Model { get; set; } = "llama3.2:3b";
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class OpenRouterConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "meta-llama/llama-3.2-3b-instruct:free";
        public string Endpoint { get; set; } = "https://openrouter.ai/api/v1";
        public string SiteUrl { get; set; } = "https://github.com/Yeri0101/FocusTerminal.AI";
        public string SiteName { get; set; } = "FocusTerminal.AI";
    }

    public class CustomApiConfig
    {
        public string Endpoint { get; set; } = "http://localhost:1234/v1";
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "local-model";
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class GeminiConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-1.5-flash";
    }

    public class PrivacyConfig
    {
        public bool SanitizeClipboard { get; set; } = true;
        public bool StrictPrivacyMode { get; set; } = false;
        public int MaxSnippetLength { get; set; } = 150;
    }

    public class WeatherConfig
    {
        public string City { get; set; } = "Lausanne";
        public double Latitude { get; set; } = 46.52;
        public double Longitude { get; set; } = 6.63;
        public bool AutoDetectLocation { get; set; } = false;
    }

    public class AudioConfig
    {
        public bool EnableSoundAlerts { get; set; } = true;
    }
}
