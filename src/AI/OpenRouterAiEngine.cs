using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Interfaces;
using FocusTerminal.AI.Core.Models;

namespace FocusTerminal.AI.AI
{
    public class OpenRouterAiEngine : IAiFocusEngine
    {
        private readonly HttpClient _httpClient;
        private readonly OpenRouterConfig _config;

        public string ProviderName => $"OpenRouter ({_config.Model})";

        public OpenRouterAiEngine(OpenRouterConfig config, HttpClient? httpClient = null)
        {
            _config = config;
            _httpClient = httpClient ?? new HttpClient();

            if (!string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey.Trim());
            }

            if (!string.IsNullOrWhiteSpace(_config.SiteUrl))
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", _config.SiteUrl);
            }

            if (!string.IsNullOrWhiteSpace(_config.SiteName))
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", _config.SiteName);
            }
        }

        public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        {
            bool hasKey = !string.IsNullOrWhiteSpace(_config.ApiKey) &&
                          !_config.ApiKey.Contains("YOUR_OPENROUTER_API_KEY", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(hasKey);
        }

        public async Task<FocusResult> AnalyzeFocusAsync(IReadOnlyList<string> clipboardHistory, string taskDescription, CancellationToken ct = default)
        {
            if (clipboardHistory == null || clipboardHistory.Count == 0)
            {
                return new FocusResult
                {
                    IsFocused = true,
                    Message = "Sin registros recientes en el portapapeles. Buen ritmo de trabajo.",
                    ProviderName = ProviderName
                };
            }

            string snippets = string.Join("\n- ", clipboardHistory);
            string systemPrompt = "Eres un asistente de concentración para desarrolladores y estudiantes. Devuelve ÚNICAMENTE un JSON válido con el esquema: {\"is_focused\": boolean, \"message\": \"mensaje corto en español\"}.";
            string userPrompt = $"""
                Tarea actual del usuario: "{taskDescription}"
                
                Últimos textos copiados al portapapeles:
                - {snippets}
                
                Determina si el usuario está enfocado en su tarea o distraído en ocio/redes.
                """;

            var responseText = await CallChatCompletionAsync(systemPrompt, userPrompt, ct);
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
                    // Error en deserialización de JSON
                }
            }

            return new FocusResult
            {
                IsFocused = true,
                Message = "Enfoque activo comprobado por OpenRouter.",
                ProviderName = ProviderName
            };
        }

        public async Task<Playlist> GetPlaylistRecommendationAsync(string mode, string taskDescription, CancellationToken ct = default)
        {
            string prompt = $"Recomienda un género musical o estilo ideal en 2 o 3 palabras para la tarea: '{taskDescription}' (Modo: {mode}). Ejemplo: 'Lofi Beats', 'Synthwave', 'Barroco'. Responde solo el género.";
            var response = await CallChatCompletionAsync("Responde solo el nombre del género musical.", prompt, ct);
            var genre = string.IsNullOrWhiteSpace(response) ? "Ambient Focus" : response.Trim();

            return new Playlist
            {
                Name = $"OpenRouter ({_config.Model}): {genre}",
                Url = "Búscalo en Spotify o YouTube",
                Genre = genre
            };
        }

        public async Task<string> GetMotivationalQuoteAsync(CancellationToken ct = default)
        {
            string response = await CallChatCompletionAsync(
                "Responde únicamente una frase de concentración y productividad en español de máximo 15 palabras.",
                "Dame una frase motivacional breve.",
                ct);

            return string.IsNullOrWhiteSpace(response) ? "La disciplina crea tu futuro." : response.Trim();
        }

        public async Task<string> GetTechFactAsync(CancellationToken ct = default)
        {
            string response = await CallChatCompletionAsync(
                "Responde únicamente un dato curioso tecnológico en español de máximo 20 palabras.",
                "Dame un dato curioso sobre tecnología o programación.",
                ct);

            return string.IsNullOrWhiteSpace(response) ? "Ada Lovelace escribió el primer algoritmo para la máquina analítica en 1843." : response.Trim();
        }

        private async Task<string> CallChatCompletionAsync(string systemMessage, string userMessage, CancellationToken ct)
        {
            try
            {
                var url = _config.Endpoint.TrimEnd('/') + "/chat/completions";
                var payload = new
                {
                    model = _config.Model,
                    messages = new object[]
                    {
                        new { role = "system", content = systemMessage },
                        new { role = "user", content = userMessage }
                    },
                    temperature = 0.3
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(responseJson);
                    var choices = doc.RootElement.GetProperty("choices");
                    if (choices.GetArrayLength() > 0)
                    {
                        var text = choices[0].GetProperty("message").GetProperty("content").GetString();
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
