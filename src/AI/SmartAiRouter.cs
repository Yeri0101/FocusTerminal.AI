using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Interfaces;
using FocusTerminal.AI.Core.Models;

namespace FocusTerminal.AI.AI
{
    public class SmartAiRouter : IAiFocusEngine
    {
        private readonly AppSettings _settings;
        private readonly OllamaAiEngine _ollama;
        private readonly OpenRouterAiEngine _openRouter;
        private readonly CustomOpenAiEngine _customApi;
        private readonly GeminiAiEngine _gemini;
        private readonly HeuristicAiEngine _heuristic;

        private IAiFocusEngine? _activeEngine;

        public string ProviderName => _activeEngine?.ProviderName ?? "Seleccionando proveedor...";

        public SmartAiRouter(AppSettings settings)
        {
            _settings = settings;

            // Compatibilidad hacia atrás si la ApiKey está en la raíz
            if (!string.IsNullOrWhiteSpace(_settings.ApiKey) && string.IsNullOrWhiteSpace(_settings.Gemini.ApiKey))
            {
                _settings.Gemini.ApiKey = _settings.ApiKey;
            }

            _ollama = new OllamaAiEngine(_settings.Ollama);
            _openRouter = new OpenRouterAiEngine(_settings.OpenRouter);
            _customApi = new CustomOpenAiEngine(_settings.CustomApi);
            _gemini = new GeminiAiEngine(_settings.Gemini);
            _heuristic = new HeuristicAiEngine();
        }

        public async Task<IAiFocusEngine> ResolveActiveEngineAsync(CancellationToken ct = default)
        {
            if (_activeEngine != null) return _activeEngine;

            string choice = _settings.AiProvider?.Trim().ToLowerInvariant() ?? "auto";

            switch (choice)
            {
                case "ollama":
                    if (await _ollama.IsAvailableAsync(ct))
                        return _activeEngine = _ollama;
                    break;

                case "openrouter":
                    if (await _openRouter.IsAvailableAsync(ct))
                        return _activeEngine = _openRouter;
                    break;

                case "custom":
                    if (await _customApi.IsAvailableAsync(ct))
                        return _activeEngine = _customApi;
                    break;

                case "gemini":
                    if (await _gemini.IsAvailableAsync(ct))
                        return _activeEngine = _gemini;
                    break;

                case "heuristic":
                    return _activeEngine = _heuristic;
            }

            // Modo "Auto": Prioridad Local -> OpenRouter -> Gemini -> Custom -> Heurístico
            if (await _ollama.IsAvailableAsync(ct))
            {
                return _activeEngine = _ollama;
            }

            if (await _openRouter.IsAvailableAsync(ct))
            {
                return _activeEngine = _openRouter;
            }

            if (await _gemini.IsAvailableAsync(ct))
            {
                return _activeEngine = _gemini;
            }

            if (await _customApi.IsAvailableAsync(ct))
            {
                return _activeEngine = _customApi;
            }

            return _activeEngine = _heuristic;
        }

        public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
        {
            var engine = await ResolveActiveEngineAsync(ct);
            return await engine.IsAvailableAsync(ct);
        }

        public async Task<FocusResult> AnalyzeFocusAsync(IReadOnlyList<string> clipboardHistory, string taskDescription, CancellationToken ct = default)
        {
            var engine = await ResolveActiveEngineAsync(ct);
            try
            {
                var result = await engine.AnalyzeFocusAsync(clipboardHistory, taskDescription, ct);
                if (result != null) return result;
            }
            catch
            {
                // Fallback automático al motor heurístico
            }

            return await _heuristic.AnalyzeFocusAsync(clipboardHistory, taskDescription, ct);
        }

        public async Task<Playlist> GetPlaylistRecommendationAsync(string mode, string taskDescription, CancellationToken ct = default)
        {
            var engine = await ResolveActiveEngineAsync(ct);
            try
            {
                var playlist = await engine.GetPlaylistRecommendationAsync(mode, taskDescription, ct);
                if (playlist != null) return playlist;
            }
            catch
            {
                // Fallback
            }

            return await _heuristic.GetPlaylistRecommendationAsync(mode, taskDescription, ct);
        }

        public async Task<string> GetMotivationalQuoteAsync(CancellationToken ct = default)
        {
            var engine = await ResolveActiveEngineAsync(ct);
            try
            {
                var quote = await engine.GetMotivationalQuoteAsync(ct);
                if (!string.IsNullOrWhiteSpace(quote)) return quote;
            }
            catch
            {
                // Fallback
            }

            return await _heuristic.GetMotivationalQuoteAsync(ct);
        }

        public async Task<string> GetTechFactAsync(CancellationToken ct = default)
        {
            var engine = await ResolveActiveEngineAsync(ct);
            try
            {
                var fact = await engine.GetTechFactAsync(ct);
                if (!string.IsNullOrWhiteSpace(fact)) return fact;
            }
            catch
            {
                // Fallback
            }

            return await _heuristic.GetTechFactAsync(ct);
        }
    }
}
