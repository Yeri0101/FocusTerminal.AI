using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Models;
using Microsoft.Extensions.Configuration;

namespace FocusTerminal.AI.Services
{
    public class ConfigService
    {
        private readonly string _configFilePath;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public ConfigService(string? customPath = null)
        {
            _configFilePath = customPath ?? Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        }

        public AppSettings LoadSettings()
        {
            var settings = new AppSettings();

            if (File.Exists(_configFilePath))
            {
                try
                {
                    var config = new ConfigurationBuilder()
                        .SetBasePath(Path.GetDirectoryName(_configFilePath) ?? Directory.GetCurrentDirectory())
                        .AddJsonFile(Path.GetFileName(_configFilePath), optional: true, reloadOnChange: false)
                        .Build();

                    config.Bind(settings);

                    // Compatibilidad hacia atrás si la ApiKey está en la raíz
                    if (!string.IsNullOrWhiteSpace(settings.ApiKey) && string.IsNullOrWhiteSpace(settings.Gemini.ApiKey))
                    {
                        settings.Gemini.ApiKey = settings.ApiKey;
                    }
                }
                catch
                {
                    // Fallback a defaults
                }
            }

            return settings;
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            try
            {
                // Sincronizar ApiKey raíz para compatibilidad
                if (!string.IsNullOrWhiteSpace(settings.Gemini.ApiKey))
                {
                    settings.ApiKey = settings.Gemini.ApiKey;
                }

                var json = JsonSerializer.Serialize(settings, JsonOptions);
                await File.WriteAllTextAsync(_configFilePath, json);
            }
            catch
            {
                // Silencioso
            }
        }

        public bool HasConfiguredLlm(AppSettings settings)
        {
            bool hasGemini = !string.IsNullOrWhiteSpace(settings.Gemini?.ApiKey) &&
                             !settings.Gemini.ApiKey.Contains("AQUÍ_VA_TU_API_KEY", StringComparison.OrdinalIgnoreCase) &&
                             !settings.Gemini.ApiKey.Contains("YOUR_GEMINI_API_KEY", StringComparison.OrdinalIgnoreCase);

            bool hasOpenRouter = !string.IsNullOrWhiteSpace(settings.OpenRouter?.ApiKey) &&
                                 !settings.OpenRouter.ApiKey.Contains("YOUR_OPENROUTER_API_KEY", StringComparison.OrdinalIgnoreCase);

            bool hasCustom = !string.IsNullOrWhiteSpace(settings.CustomApi?.Endpoint) &&
                             settings.CustomApi.Endpoint != "http://localhost:1234/v1";

            return hasGemini || hasOpenRouter || hasCustom;
        }
    }
}
