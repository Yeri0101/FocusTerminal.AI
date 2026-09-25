using System.Collections.Generic;
using System.Threading.Tasks;
using FocusTerminal.AI.AI;
using FocusTerminal.AI.Core.Models;
using Xunit;

namespace FocusTerminal.AI.Tests
{
    public class SmartAiRouterTests
    {
        [Fact]
        public async Task AnalyzeFocusAsync_ShouldFallbackToHeuristic_WhenNoKeysOrEndpointsConfigured()
        {
            var settings = new AppSettings
            {
                AiProvider = "Auto",
                Ollama = new OllamaConfig { Endpoint = "http://localhost:99999" }, // Inalcanzable
                OpenRouter = new OpenRouterConfig { ApiKey = "" },
                Gemini = new GeminiConfig { ApiKey = "" },
                CustomApi = new CustomApiConfig { Endpoint = "" }
            };

            var router = new SmartAiRouter(settings);
            var snippets = new List<string> { "Console.WriteLine('Hello World');" };

            var result = await router.AnalyzeFocusAsync(snippets, "C# programming");

            Assert.NotNull(result);
            Assert.True(result.IsFocused);
        }

        [Fact]
        public async Task GetPlaylistRecommendationAsync_ShouldProvideGenre_EvenInFallback()
        {
            var settings = new AppSettings { AiProvider = "Heuristic" };
            var router = new SmartAiRouter(settings);

            var playlist = await router.GetPlaylistRecommendationAsync("study", "Studying for math exam");

            Assert.NotNull(playlist);
            Assert.False(string.IsNullOrWhiteSpace(playlist.Name));
            Assert.False(string.IsNullOrWhiteSpace(playlist.Genre));
        }
    }
}
