using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Interfaces;
using FocusTerminal.AI.Core.Models;

namespace FocusTerminal.AI.AI
{
    public class GeminiAiEngine : IAiFocusEngine
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiConfig _config;

        public string ProviderName => $"Google Gemini ({_config.Model})";

        public GeminiAiEngine(GeminiConfig config, HttpClient? httpClient = null)
        {
            _config = config;
            _httpClient = httpClient ?? new HttpClient();
        }

        public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        {
            bool hasKey = !string.IsNullOrWhiteSpace(_config.ApiKey) &&
                          !_config.ApiKey.Contains("AQUÍ_VA_TU_API_KEY", StringComparison.OrdinalIgnoreCase) &&
                          !_config.ApiKey.Contains("YOUR_GEMINI_API_KEY", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(hasKey);
        }

        public async Task<FocusResult> AnalyzeFocusAsync(IReadOnlyList<string> clipboardHistory, string taskDescription, CancellationToken ct = default)
        {
            if (clipboardHistory == null || clipboardHistory.Count == 0)
            {
                return new FocusResult
                {
                    IsFocused = true,
                    Message = "Portapapeles limpio, sin distracciones registradas.",
                    ProviderName = ProviderName
                };
            }

            if (!await IsAvailableAsync(ct))
            {
                return new FocusResult
                {
                    IsFocused = true,
                    Message = "Gemini API Key no configurada, asumiendo enfoque.",
                    ProviderName = ProviderName
                };
            }

            string snippets = string.Join(", ", clipboardHistory.Select(s => $"'{s}'"));
            string prompt = $$"""
                Un usuario está trabajando en una tarea descrita como: '{{taskDescription}}'.
                Historial de textos copiados recientemente: [{{snippets}}].
                Basado en esto, ¿parece estar enfocado en su tarea o distraído?
                Responde ÚNICAMENTE con un objeto JSON: {"is_focused": true, "message": "mensaje muy corto y amigable en español"}.
                """;

            var responseText = await GenerateContentAsync(prompt, ct);
            if (!string.IsNullOrWhiteSpace(responseText))
            {
                try
                {
                    var clean = responseText.Replace("```json", "").Replace("```", "").Trim();
                    var result = JsonSerializer.Deserialize<FocusResult>(clean, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (result != null)
                    {
                        result.ProviderName = ProviderName;
                        return result;
                    }
                }
                catch
                {
                    // Fallback
                }
            }

            return new FocusResult
            {
                IsFocused = true,
                Message = "Enfoque validado correctamente.",
                ProviderName = ProviderName
            };
        }

        public async Task<Playlist> GetPlaylistRecommendationAsync(string mode, string taskDescription, CancellationToken ct = default)
        {
            string prompt = $"Basado en la siguiente tarea ('{taskDescription}', Modo: {mode}), recomienda solo el GÉNERO o TIPO de música ideal para concentrarse (ejemplo: 'Música Clásica', 'Lofi Beats', 'Sonidos de Lluvia'). Responde solo el género sin URLs.";
            var response = await GenerateContentAsync(prompt, ct);
            var genre = string.IsNullOrWhiteSpace(response) ? "Lofi Chill" : response.Trim();

            return new Playlist
            {
                Name = $"Gemini ({_config.Model}): {genre}",
                Url = "Búscalo en tu plataforma favorita.",
                Genre = genre
            };
        }

        public async Task<string> GetMotivationalQuoteAsync(CancellationToken ct = default)
        {
            string prompt = "Dame una frase de motivación para enfoque de trabajo de máximo 15 palabras en español. Responde solo la frase.";
            var response = await GenerateContentAsync(prompt, ct);
            return string.IsNullOrWhiteSpace(response) ? "El éxito llega a quienes no se rinden jamás." : response.Trim();
        }

        public async Task<string> GetTechFactAsync(CancellationToken ct = default)
        {
            string prompt = "Dame un dato curioso sobre informática de máximo 20 palabras en español. Responde solo el dato.";
            var response = await GenerateContentAsync(prompt, ct);
            return string.IsNullOrWhiteSpace(response) ? "El primer dominio de internet registrado fue Symbolics.com en 1985." : response.Trim();
        }

        private async Task<string> GenerateContentAsync(string prompt, CancellationToken ct)
        {
            try
            {
                var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{_config.Model}:generateContent?key={_config.ApiKey}";
                var payload = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(apiUrl, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(jsonResponse);
                    var candidates = doc.RootElement.GetProperty("candidates");
                    if (candidates.GetArrayLength() > 0)
                    {
                        var text = candidates[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString();
                        return text ?? "";
                    }
                }
            }
            catch
            {
                // Fallback silencioso
            }

            return "";
        }
    }
}
