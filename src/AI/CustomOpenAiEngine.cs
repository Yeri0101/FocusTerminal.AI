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
    public class CustomOpenAiEngine : IAiFocusEngine
    {
        private readonly HttpClient _httpClient;
        private readonly CustomApiConfig _config;

        public string ProviderName => $"API Custom ({_config.Model})";

        public CustomOpenAiEngine(CustomApiConfig config, HttpClient? httpClient = null)
        {
            _config = config;
            _httpClient = httpClient ?? new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
            };

            if (!string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey.Trim());
            }
        }

        public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_config.Endpoint)) return false;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromMilliseconds(1500));

                var url = _config.Endpoint.TrimEnd('/') + "/models";
                var response = await _httpClient.GetAsync(url, cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<FocusResult> AnalyzeFocusAsync(
            IReadOnlyList<string> clipboardHistory,
            IReadOnlyList<string> activeWindows,
            string taskDescription,
            CancellationToken ct = default)
        {
            string snippets = (clipboardHistory != null && clipboardHistory.Count > 0)
                ? string.Join("\n- ", clipboardHistory)
                : "(Sin copias recientes)";

            string windows = (activeWindows != null && activeWindows.Count > 0)
                ? string.Join("\n- ", activeWindows)
                : "(Sin cambios de ventana registrados)";

            string systemPrompt = "Eres un evaluador de concentración. Responde únicamente con JSON en formato: {\"is_focused\": boolean, \"message\": \"mensaje corto en español\"}. Si el usuario navega en redes sociales, ocio o contenido no relacionado a su tarea, clasifícalo como distraído (is_focused: false).";
            string userPrompt = $"""
                Tarea del usuario: "{taskDescription}"
                
                Ventanas y aplicaciones utilizadas:
                - {windows}
                
                Contenidos recientes copiados:
                - {snippets}
                
                Determina si está enfocado en su tarea o distraído en ocio/redes.
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
                    // Fallback
                }
            }

            return new FocusResult
            {
                IsFocused = true,
                Message = "Enfoque detectado por API Custom.",
                ProviderName = ProviderName
            };
        }

        public async Task<Playlist> GetPlaylistRecommendationAsync(string mode, string taskDescription, CancellationToken ct = default)
        {
            string prompt = $"Recomienda un género musical ideal en 2 palabras para: '{taskDescription}' (Modo: {mode}).";
            var response = await CallChatCompletionAsync("Responde únicamente el nombre del género musical.", prompt, ct);
            var genre = string.IsNullOrWhiteSpace(response) ? "Lo-Fi Instrumental" : response.Trim();

            return new Playlist
            {
                Name = $"Custom API ({_config.Model}): {genre}",
                Url = "Búscalo en tu reproductor de música",
                Genre = genre
            };
        }

        public async Task<string> GetMotivationalQuoteAsync(CancellationToken ct = default)
        {
            var response = await CallChatCompletionAsync(
                "Responde con una frase de productividad corta (máx 15 palabras) en español.",
                "Frase motivacional para enfoque de trabajo.",
                ct);

            return string.IsNullOrWhiteSpace(response) ? "La persistencia vence a la resistencia." : response.Trim();
        }

        public async Task<string> GetTechFactAsync(CancellationToken ct = default)
        {
            var response = await CallChatCompletionAsync(
                "Responde con un dato curioso tecnológico en español (máx 20 palabras).",
                "Dato curioso de computación.",
                ct);

            return string.IsNullOrWhiteSpace(response) ? "Linux comenzó como un proyecto personal de Linus Torvalds en 1991." : response.Trim();
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
