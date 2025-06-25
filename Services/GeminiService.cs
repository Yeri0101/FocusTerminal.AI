using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FocusTerminal.AI
{
    public class GeminiService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, List<Playlist>> _predefinedPlaylists;

        public GeminiService(string key)
        {
            _apiKey = key;
            _httpClient = new HttpClient();
            _predefinedPlaylists = new Dictionary<string, List<Playlist>>
            {
                ["study"] = new List<Playlist> { new Playlist { Name = "LoFi Girl", Url = "https://www.youtube.com/watch?v=jfKfPfyJRdk" }, new Playlist { Name = "Deep Focus", Url = "https://open.spotify.com/playlist/37i9dQZF1DWZeKCadgRdKQ" } },
                ["work"] = new List<Playlist> { new Playlist { Name = "Electronic Focus", Url = "https://open.spotify.com/playlist/37i9dQZF1DX8NTLI2TtZa6" }, new Playlist { Name = "Coding Mode", Url = "https://open.spotify.com/playlist/4U4N3rOcvNQWlJdUBwWQdU" } },
                ["creative"] = new List<Playlist> { new Playlist { Name = "Creative Flow", Url = "https://open.spotify.com/playlist/37i9dQZF1DWU6RkLn4WNxq" }, new Playlist { Name = "Brain Food", Url = "https://open.spotify.com/playlist/37i9dQZF1DX8Uebhn9wzrS" } }
            };
        }

        public async Task<FocusResult> AnalyzeFocusAsync(List<string> clipboardHistory, List<int> kpmHistory, List<string> sampledWords, string taskDescription)
        {
            if (string.IsNullOrEmpty(_apiKey) || _apiKey.Contains("AQUÍ_VA_TU_API_KEY")) return new FocusResult { IsFocused = true, Message = "API Key no configurada." };

            string clipboardText = clipboardHistory.Any() ? $"Historial del portapapeles: [{string.Join(", ", clipboardHistory.Select(item => $"'{item}'"))}]." : "El usuario no ha copiado nada recientemente.";
            string kpmText = kpmHistory.Any() ? $"Ritmo de escritura reciente (teclas por minuto): [{string.Join(", ", kpmHistory)}]." : "No hay datos de ritmo de escritura.";
            string sampledWordsText = sampledWords.Any() ? $"Una muestra de las palabras que ha escrito es: [{string.Join(", ", sampledWords)}]." : "No hay muestra de palabras escritas.";

            string prompt = $"Eres un asistente de concentración. Tu objetivo principal es analizar si el contenido de lo que escribe y copia un usuario está relacionado con su tarea. La velocidad de escritura es secundaria.\n" +
                            $"Tarea del usuario: '{taskDescription}'.\n" +
                            $"Datos de actividad:\n" +
                            $"- Muestra de palabras escritas: {sampledWordsText}\n" +
                            $"- Historial del portapapeles: {clipboardText}\n" +
                            $"- Ritmo de escritura (KPM): {kpmText}\n" +
                            $"REGLAS DE ANÁLISIS:\n" +
                            $"1. **PRIORIDAD MÁXIMA: EL CONTENIDO.** Si las 'palabras escritas' o el 'portapapeles' contienen temas claramente no relacionados con la tarea (ej: redes sociales, noticias, entretenimiento), el usuario está **DISTRAÍDO**, sin importar su ritmo de escritura.\n" +
                            $"2. **INACTIVIDAD POSITIVA:** Si el ritmo de escritura es CERO, pero el portapapeles y las palabras son irrelevantes o están vacíos, asume que el usuario está leyendo o pensando. Considera esto como **ENFOCADO** y dale un mensaje sobre la importancia de la reflexión.\n" +
                            $"3. **ENFOQUE CLARO:** Si el ritmo de escritura es alto y las palabras escritas están relacionadas con la tarea, el usuario está **ENFOCADO**.\n" +
                            $"Responde únicamente con un objeto JSON con el formato: {{\"is_focused\": boolean, \"message\": \"un mensaje muy corto, amigable y específico para el usuario\"}}.";

            var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key={_apiKey}";
            var payload = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
            try
            {
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(apiUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    var textResponse = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                    var cleanJson = textResponse.Replace("```json", "").Replace("```", "").Trim();
                    return JsonSerializer.Deserialize<FocusResult>(cleanJson);
                }
            }
            catch { /* Ignorar errores */ }
            return new FocusResult { IsFocused = true, Message = "No se pudo analizar el enfoque." };
        }

        public async Task<Playlist> GetPlaylistRecommendation(string mode, string taskDescription)
        {
            if (_predefinedPlaylists.TryGetValue(mode.ToLower().Trim(), out var playlists)) { return playlists[new Random().Next(playlists.Count)]; }
            string prompt = $"Para una tarea de '{taskDescription}', recomienda un género de música para concentrarse (ej: Lofi Beats). Solo el nombre del género.";
            string generatedGenre = await GenerateTextWithGemini(prompt);
            return new Playlist { Name = $"Sugerencia IA: {generatedGenre.Trim()}", Url = "Busca este género en tu servicio de música." };
        }
        private async Task<string> GenerateTextWithGemini(string prompt)
        {
            if (string.IsNullOrEmpty(_apiKey) || _apiKey.Contains("AQUÍ_VA_TU_API_KEY")) return "Música Ambiental (API Key no configurada)";
            var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key={_apiKey}";
            var payload = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
            try
            {
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(apiUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                }
            }
            catch { /* Ignorar errores */ }
            return "Rock Instrumental (Error de API)";
        }
        public Task<string> GetMotivationalQuote() => Task.FromResult(new[] { "El secreto para salir adelante es empezar.", "La disciplina es el puente entre las metas y los logros." }[new Random().Next(2)]);
        public Task<string> GetTechFact() => Task.FromResult(new[] { "El primer 'bug' informático fue literalmente una polilla.", "El primer disco duro de 1GB pesaba más de 227 kg." }[new Random().Next(2)]);
    }
}
