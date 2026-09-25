using System;
using System.IO;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Models;
using FocusTerminal.AI.Services;
using Xunit;

namespace FocusTerminal.AI.Tests
{
    public class ConfigServiceTests : IDisposable
    {
        private readonly string _testConfigFile;

        public ConfigServiceTests()
        {
            _testConfigFile = Path.Combine(Path.GetTempPath(), $"test_appsettings_{Guid.NewGuid():N}.json");
        }

        [Fact]
        public void HasConfiguredLlm_ShouldReturnFalse_WhenNoKeysConfigured()
        {
            var configService = new ConfigService(_testConfigFile);
            var settings = new AppSettings
            {
                Gemini = new GeminiConfig { ApiKey = "AQUÍ_VA_TU_API_KEY" },
                OpenRouter = new OpenRouterConfig { ApiKey = "" }
            };

            bool hasLlm = configService.HasConfiguredLlm(settings);

            Assert.False(hasLlm);
        }

        [Fact]
        public void HasConfiguredLlm_ShouldReturnTrue_WhenGeminiKeyIsPresent()
        {
            var configService = new ConfigService(_testConfigFile);
            var settings = new AppSettings
            {
                Gemini = new GeminiConfig { ApiKey = "AIzaSyRealApiKeyForTesting12345" }
            };

            bool hasLlm = configService.HasConfiguredLlm(settings);

            Assert.True(hasLlm);
        }

        [Fact]
        public async Task SaveAndLoadSettings_ShouldPersistCorrectly()
        {
            var configService = new ConfigService(_testConfigFile);
            var settings = new AppSettings
            {
                AiProvider = "OpenRouter",
                OpenRouter = new OpenRouterConfig
                {
                    ApiKey = "sk-or-v1-testkey12345",
                    Model = "meta-llama/llama-3.2-3b-instruct:free"
                }
            };

            await configService.SaveSettingsAsync(settings);
            var loaded = configService.LoadSettings();

            Assert.Equal("OpenRouter", loaded.AiProvider);
            Assert.Equal("sk-or-v1-testkey12345", loaded.OpenRouter.ApiKey);
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testConfigFile)) File.Delete(_testConfigFile);
            }
            catch
            {
                // Silencioso
            }
        }
    }
}
