using System.Collections.Generic;
using System.Threading.Tasks;
using FocusTerminal.AI.AI;
using Xunit;

namespace FocusTerminal.AI.Tests
{
    public class HeuristicAiEngineTests
    {
        [Fact]
        public async Task AnalyzeFocusAsync_ShouldDetectFocus_WhenTechnicalKeywordsCopied()
        {
            var engine = new HeuristicAiEngine();
            var snippets = new List<string>
            {
                "public async Task<int> CalculateAsync()",
                "git commit -m 'feat: improve model'",
                "https://learn.microsoft.com/dotnet/api"
            };

            var result = await engine.AnalyzeFocusAsync(snippets, new List<string> { "Visual Studio Code" }, "Refactoring C# application");

            Assert.True(result.IsFocused);
            Assert.Contains("Patrones de trabajo", result.Message);
        }

        [Fact]
        public async Task AnalyzeFocusAsync_ShouldDetectDistraction_WhenDistractingSitesCopied()
        {
            var engine = new HeuristicAiEngine();
            var snippets = new List<string>
            {
                "https://www.youtube.com/watch?v=funny-cat-video",
                "https://www.netflix.com/browse",
                "https://www.twitch.tv/streamer"
            };

            var result = await engine.AnalyzeFocusAsync(snippets, new List<string>(), "Writing technical documentation");

            Assert.False(result.IsFocused);
            Assert.Contains("distracción", result.Message);
        }

        [Fact]
        public async Task AnalyzeFocusAsync_ShouldDetectDistraction_WhenActiveWindowIsSocialMedia()
        {
            var engine = new HeuristicAiEngine();
            var windows = new List<string>
            {
                "redes sociale s - Buscar con Google - Google Chrome"
            };

            var result = await engine.AnalyzeFocusAsync(new List<string>(), windows, "investigacion universidades informatica");

            Assert.False(result.IsFocused);
            Assert.Contains("distracción", result.Message);
        }

        [Fact]
        public async Task GetMotivationalQuoteAsync_ShouldReturnNonEmptyQuote()
        {
            var engine = new HeuristicAiEngine();

            var quote = await engine.GetMotivationalQuoteAsync();

            Assert.False(string.IsNullOrWhiteSpace(quote));
        }

        [Fact]
        public async Task GetTechFactAsync_ShouldReturnNonEmptyFact()
        {
            var engine = new HeuristicAiEngine();

            var fact = await engine.GetTechFactAsync();

            Assert.False(string.IsNullOrWhiteSpace(fact));
        }
    }
}
