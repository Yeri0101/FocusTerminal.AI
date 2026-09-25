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
    public class OllamaAiEngine : IAiFocusEngine
    {
        private readonly HttpClient _httpClient;
        private readonly OllamaConfig _config;

        public string ProviderName => $"Ollama Local ({_config.Model})";

        public OllamaAiEngine(OllamaConfig config, HttpClient? httpClient = null)
        {
            _config = config;
            if (httpClient != null)
            {
                _httpClient = httpClient;
            }
            else
            {
                var client = new HttpClient { Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds) };
                if (Uri.TryCreate(config.Endpoint?.TrimEnd('/'), UriKind.Absolute, out var baseUri))
                {
                    client.BaseAddress = baseUri;
                }
                _httpClient = client;
            }
        }

        public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
        {
            if (_httpClient.BaseAddress == null) return false;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromMilliseconds(1200));

                var response = await _httpClient.GetAsync("/api/tags", cts.Token);
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
            string historyText = (clipboardHistory != null && clipboardHistory.Count > 0)
                ? string.Join("; ", clipboardHistory.Select(s => $"\"{s}\""))
                : "Sin copias recientes";

            string windowsText = (activeWindows != null && activeWindows.Count > 0)
                ? string.Join("; ", activeWindows.Select(w => $"\"{w}\""))
                : "Ventanas estándar de trabajo";

            string prompt = $$"""
                Analiza si el usuario está enfocado en su tarea o distraído en ocio/redes.
                Tarea: "{{taskDescription}}"
                Ventanas y aplicaciones activas usadas: [{{windowsText}}]
                Historial reciente de portapapeles: [{{historyText}}]
                
                Responde EXCLUSIVAMENTE un objeto JSON válido con este esquema:
                {"is_focused": true, "message": "mensaje conciso y amigable en español (máx 12 palabras)"}
                """;

            var payload = new
            {
                model = _config.Model,
                prompt = prompt,
                stream = false,
                format = "json",
                options = new { temperature = 0.3 }
            };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/generate", content, ct);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(responseJson);

                    if (doc.RootElement.TryGetProperty("response", out var rawTextElement))
                    {
                        var rawText = rawTextElement.GetString() ?? "";
                        var clean = rawText.Replace("```json", "").Replace("```", "").Trim();
                        var result = JsonSerializer.Deserialize<FocusResult>(clean, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (result != null)
                        {
                            result.ProviderName = ProviderName;
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new FocusResult
                {
                    IsFocused = true,
                    Message = $"Aviso ({ProviderName}): {ex.Message}",
                    ProviderName = ProviderName
                };
            }

            return new FocusResult
            {
                IsFocused = true,
                Message = "Enfoque inferido por defecto.",
                ProviderName = ProviderName
            };
        }

        public async Task<Playlist> GetPlaylistRecommendationAsync(string mode, string taskDescription, CancellationToken ct = default)
        {
            string prompt = $"Recomienda un género musical o estilo ideal en 2 o 3 palabras para la tarea: '{taskDescription}' (Modo: {mode}). Ejemplo: 'Lofi Hip Hop', 'Dark Techno', 'Piano Clásico'. Responde solo el género.";
            var genre = await GenerateTextAsync(prompt, ct);
            return new Playlist
            {
                Name = $"IA Local ({_config.Model}): {genre.Trim()}",
                Url = "Búscalo en tu plataforma favorita.",
                Genre = genre.Trim()
            };
        }

        public async Task<string> GetMotivationalQuoteAsync(CancellationToken ct = default)
        {
            string prompt = "Dame una frase motivacional de trabajo profundo o productividad (máximo 15 palabras) en español. Responde solo la frase sin comillas ni intros.";
            var quote = await GenerateTextAsync(prompt, ct);
            return string.IsNullOrWhiteSpace(quote) ? "El éxito es la suma de pequeños esfuerzos repetidos." : quote.Trim();
        }

        public async Task<string> GetTechFactAsync(CancellationToken ct = default)
        {
            string prompt = "Dame un dato curioso tecnológico fascinante en español (máximo 20 palabras). Responde solo el dato.";
            var fact = await GenerateTextAsync(prompt, ct);
            return string.IsNullOrWhiteSpace(fact) ? "El primer virus informático se llamó Creeper y fue creado en 1971." : fact.Trim();
        }

        private async Task<string> GenerateTextAsync(string prompt, CancellationToken ct)
        {
            try
            {
                var payload = new
                {
                    model = _config.Model,
                    prompt = prompt,
                    stream = false,
                    options = new { temperature = 0.7 }
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/generate", content, ct);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(responseJson);
                    if (doc.RootElement.TryGetProperty("response", out var text))
                    {
                        return text.GetString() ?? "";
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
