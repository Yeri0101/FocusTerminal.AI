using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration; // <-- Paquete necesario
using TextCopy; // <-- Necesario para el portapapeles

namespace FocusTerminal.AI
{
    public class Program
    {
        [STAThread]
        public static async Task Main(string[] args)
        {
            var sessionManager = new SessionManager();
            await sessionManager.Start();
        }
    }

    public class SessionManager
    {
        private TaskDetails _currentTask;
        private readonly GeminiService _geminiService;
        private readonly FocusMonitor _focusMonitor;
        private readonly WeatherService _weatherService;
        private Playlist _recommendedPlaylist;
        private bool _isSessionActive;
        private int _nextCheckPointPercentage;

        private const string TASK_CONFIG_FILE = "task_config.txt";

        public SessionManager()
        {
            // --- INICIO DE LA MODIFICACIÓN ---
            // Construir la configuración para leer el archivo appsettings.json
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Obtener la API Key desde la configuración
            string apiKey = config["ApiKey"];

            if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("AQUÍ_VA_TU_API_KEY"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: La API Key de Gemini no se ha configurado en el archivo appsettings.json.");
                Console.ResetColor();
            }

            // Inyectar la API key en el servicio de Gemini
            _geminiService = new GeminiService(apiKey);
            // --- FIN DE LA MODIFICACIÓN ---

            _focusMonitor = new FocusMonitor();
            _weatherService = new WeatherService();
            _isSessionActive = false;
        }

        public async Task Start()
        {
            var clipboard = new Clipboard();
            clipboard.SetText(string.Empty);
            PrintWelcomeMessage();

            if (!SetupTask())
            {
                PrintGoodbyeMessage();
                return;
            }

            Console.WriteLine("\n--- Sesión Iniciada ---");

            string weather = await _weatherService.GetWeatherAsync("Lausanne");
            Console.WriteLine($"🌤️  Clima actual en Lausanne: {weather}");

            Console.WriteLine($"Tarea: {_currentTask.Name} | Modo: {_currentTask.Mode} | Intervalo: {_currentTask.IntervalDurationMinutes} min");

            _recommendedPlaylist = await _geminiService.GetPlaylistRecommendation(_currentTask.Mode, _currentTask.Description);
            await ProvidePlaylistRecommendation();

            _isSessionActive = true;

            var monitorCts = new CancellationTokenSource();
            _focusMonitor.StartMonitoring(monitorCts.Token);

            await RunRealtimeSession();

            monitorCts.Cancel();
            PrintGoodbyeMessage();
        }

        private async Task RunRealtimeSession()
        {
            while (_isSessionActive)
            {
                DateTime intervalStartTime = DateTime.Now;
                DateTime endTime = intervalStartTime.AddMinutes(_currentTask.IntervalDurationMinutes);
                _nextCheckPointPercentage = 15;
                double totalDurationSeconds = _currentTask.IntervalDurationMinutes * 60;

                while (DateTime.Now < endTime && _isSessionActive)
                {
                    TimeSpan remainingTime = endTime - DateTime.Now;
                    Console.Write($"\rTiempo restante: {remainingTime:mm\\:ss} | Escribe 'stop' o 'pause'...");

                    double elapsedSeconds = (DateTime.Now - intervalStartTime).TotalSeconds;
                    int currentPercentage = (int)((elapsedSeconds / totalDurationSeconds) * 100);

                    if (currentPercentage >= _nextCheckPointPercentage)
                    {
                        await PerformFocusCheck();
                        _nextCheckPointPercentage += 15;
                    }

                    if (Console.KeyAvailable)
                    {
                        string command = Console.ReadLine()?.ToLower().Trim() ?? "";
                        switch (command)
                        {
                            case "stop":
                                Console.Write("\n¿Deseas eliminar la tarea guardada al salir? (s/n, 'n' por defecto la guardará): ");
                                string deleteChoice = Console.ReadLine()?.ToLower().Trim();
                                if (deleteChoice == "s")
                                {
                                    if (File.Exists(TASK_CONFIG_FILE))
                                    {
                                        File.Delete(TASK_CONFIG_FILE);
                                        Console.WriteLine("Tarea eliminada.");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Tarea guardada para la próxima vez.");
                                }
                                _isSessionActive = false;
                                ShowSessionSummary();
                                break;
                            case "pause":
                                TimeSpan activeTime = DateTime.Now - intervalStartTime;
                                await ShowPauseSummary(activeTime, remainingTime);
                                endTime = await HandlePause(endTime);
                                Console.WriteLine("Reanudando sesión...");
                                break;
                        }
                    }
                    await Task.Delay(1000);
                }

                if (_isSessionActive)
                {
                    await OnIntervalElapsed();
                    Console.Write("¿Iniciar otro intervalo? (s/n): ");
                    if (Console.ReadLine()?.ToLower() != "s")
                    {
                        _isSessionActive = false;
                        ShowSessionSummary();
                    }
                }
            }
        }

        private async Task PerformFocusCheck()
        {
            var clipboardHistory = _focusMonitor.GetAndClearClipboardHistory();
            if (clipboardHistory.Count == 0) return;

            Console.WriteLine($"\n[Chequeo de enfoque al {_nextCheckPointPercentage - 15}%...]");

            FocusResult result = await _geminiService.AnalyzeFocusAsync(clipboardHistory, _currentTask.Description);

            Console.ForegroundColor = result.IsFocused ? ConsoleColor.Green : ConsoleColor.Yellow;
            string icon = result.IsFocused ? "✅" : "⚠️";
            Console.WriteLine($"{icon} {result.Message}");
            Console.ResetColor();
        }


        private async Task<DateTime> HandlePause(DateTime currentEndTime)
        {
            DateTime pauseStartTime = DateTime.Now;
            while (true)
            {
                if (Console.KeyAvailable && Console.ReadLine()?.ToLower().Trim() == "resume")
                {
                    DateTime pauseEndTime = DateTime.Now;
                    TimeSpan pauseDuration = pauseEndTime - pauseStartTime;
                    return currentEndTime.Add(pauseDuration);
                }
                await Task.Delay(100);
            }
        }

        private async Task ShowPauseSummary(TimeSpan activeTime, TimeSpan remainingTime)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("--- SESIÓN EN PAUSA ---");

            string weather = await _weatherService.GetWeatherAsync("Lausanne");

            Console.WriteLine($"⏱️  Tiempo activo: {activeTime:mm} min");
            Console.WriteLine($"📍  Modo: {_currentTask.Mode} ({_currentTask.Description})");
            Console.WriteLine($"⏳  Tiempo restante del intervalo: {remainingTime:mm\\:ss}");
            Console.WriteLine($"🌤️  Clima actual en Lausanne: {weather}");
            Console.WriteLine($"🎵  Música sugerida: {_recommendedPlaylist.Name} ({_recommendedPlaylist.Url})");

            Console.WriteLine("\nEscribe 'resume' para continuar.");
            Console.ResetColor();
        }

        private bool SetupTask()
        {
            if (File.Exists(TASK_CONFIG_FILE))
            {
                var lines = File.ReadAllLines(TASK_CONFIG_FILE);
                string taskName = lines.Length > 0 ? lines[0] : "tarea guardada";

                Console.WriteLine($"\nSe encontró una tarea guardada: '{taskName}'");
                Console.WriteLine("¿Qué deseas hacer?");
                Console.WriteLine("  [1] Continuar con esta tarea");
                Console.WriteLine("  [2] Iniciar una nueva tarea (la anterior será eliminada)");
                Console.WriteLine("  [3] Salir");
                Console.Write("Elige una opción: ");

                while (true)
                {
                    string choice = Console.ReadLine()?.Trim();
                    switch (choice)
                    {
                        case "1":
                            LoadTask();
                            Console.WriteLine("Tarea anterior cargada.");
                            return true;
                        case "2":
                            CreateNewTask();
                            return true;
                        case "3":
                            return false; // El usuario elige salir
                        default:
                            Console.Write("Opción no válida. Por favor, elige 1, 2 o 3: ");
                            break;
                    }
                }
            }
            else
            {
                CreateNewTask();
                return true;
            }
        }

        private async Task ProvidePlaylistRecommendation()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\nBuscando una playlist para tu sesión...");
            Console.WriteLine($"💡 Sugerencia: Para tu tarea, te recomiendo: '{_recommendedPlaylist.Name}'");
            Console.WriteLine($"   Enlace: {_recommendedPlaylist.Url}");
            Console.ResetColor();
        }

        private void CreateNewTask()
        {
            Console.WriteLine("\n--- Creando Nueva Tarea ---");
            Console.Write("Nombre de la tarea: ");
            string name = Console.ReadLine();
            Console.Write("Descripción breve de la tarea: ");
            string description = Console.ReadLine();
            Console.Write("Duración del intervalo de trabajo (minutos): ");
            int interval = int.TryParse(Console.ReadLine(), out int i) ? i : 30;
            Console.Write("Modo (ej: 'study', 'work', 'creative' o uno personalizado): ");
            string mode = Console.ReadLine();

            _currentTask = new TaskDetails
            {
                Name = name,
                Description = description,
                IntervalDurationMinutes = interval,
                Mode = mode
            };
            SaveTask();
        }

        private void SaveTask()
        {
            string[] lines = { _currentTask.Name, _currentTask.Description, _currentTask.IntervalDurationMinutes.ToString(), _currentTask.Mode };
            File.WriteAllLines(TASK_CONFIG_FILE, lines);
        }

        private void LoadTask()
        {
            var lines = File.ReadAllLines(TASK_CONFIG_FILE);
            _currentTask = new TaskDetails
            {
                Name = lines.Length > 0 ? lines[0] : "Tarea sin nombre",
                Description = lines.Length > 1 ? lines[1] : "",
                IntervalDurationMinutes = lines.Length > 2 && int.TryParse(lines[2], out int i) ? i : 30,
                Mode = lines.Length > 3 ? lines[3] : "work"
            };
        }

        private async Task OnIntervalElapsed()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n--- Fin del Intervalo ---");

            string message = new Random().Next(0, 2) == 0
                ? $"💪 {await _geminiService.GetMotivationalQuote()}"
                : $"💡 ¿Sabías que? {await _geminiService.GetTechFact()}";
            Console.WriteLine(message);

            Console.WriteLine("\nTómate un descanso. Estira, mira a lo lejos, hidrátate.");
            Console.ResetColor();

            if (new Random().Next(0, 3) == 0) AskForSubjectiveFeedback();
        }

        private void AskForSubjectiveFeedback()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nEvaluación rápida (opcional):");
            Console.Write("¿Cómo te sientes de energía (1-5)?: ");
            string energy = Console.ReadLine();
            Console.Write("Describe tu estado (ej: 'concentrado', 'cansado'): ");
            string status = Console.ReadLine();
            Console.WriteLine("Gracias. Ajustaré las notificaciones.");
            Console.ResetColor();
        }

        private void ShowSessionSummary()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n\n--- Resumen de la Sesión ---");
            Console.WriteLine($"¡Buen trabajo en la tarea '{_currentTask.Name}'!");
            Console.ResetColor();
        }

        private void PrintWelcomeMessage()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================");
            Console.WriteLine("     Bienvenido a FocusTerminal.AI");
            Console.WriteLine("===================================");
            Console.ResetColor();
            Console.WriteLine();
        }

        private void PrintGoodbyeMessage()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n¡Sesión finalizada. Hasta la próxima!");
            Console.ResetColor();
        }
    }

    public class Playlist { public string Name { get; set; } public string Url { get; set; } }
    public class TaskDetails { public string Name { get; set; } public string Description { get; set; } public int IntervalDurationMinutes { get; set; } public string Mode { get; set; } }
    public class FocusResult { [JsonPropertyName("is_focused")] public bool IsFocused { get; set; } [JsonPropertyName("message")] public string Message { get; set; } }


    public class FocusMonitor
    {
        private readonly List<string> _clipboardHistory = new List<string>();
        private string _lastClipboardText = "";

        public void StartMonitoring(CancellationToken token)
        {
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await CollectClipboardData();
                    await Task.Delay(2000, token);
                }
            }, token);
        }

        private async Task CollectClipboardData()
        {
            try
            {
                var clipboard = new Clipboard();
                string currentClipboardText = await clipboard.GetTextAsync();

                if (!string.IsNullOrEmpty(currentClipboardText) && currentClipboardText != _lastClipboardText)
                {
                    _lastClipboardText = currentClipboardText;
                    string truncatedText = currentClipboardText.Substring(0, Math.Min(currentClipboardText.Length, 100));
                    _clipboardHistory.Add(truncatedText);
                }
            }
            catch (Exception) { /* Ignorar errores silenciosamente */ }
        }

        public List<string> GetAndClearClipboardHistory()
        {
            var history = new List<string>(_clipboardHistory);
            _clipboardHistory.Clear();
            return history;
        }
    }

    public class WeatherService
    {
        private readonly HttpClient _httpClient = new HttpClient();

        public async Task<string> GetWeatherAsync(string city)
        {
            var url = "https://api.open-meteo.com/v1/forecast?latitude=46.52&longitude=6.63&current_weather=true";
            try
            {
                var response = await _httpClient.GetStringAsync(url);
                using (var jsonDoc = JsonDocument.Parse(response))
                {
                    var currentWeather = jsonDoc.RootElement.GetProperty("current_weather");
                    var temp = currentWeather.GetProperty("temperature").GetDouble();
                    var code = currentWeather.GetProperty("weathercode").GetInt32();
                    return $"{temp}°C — Código de clima: {code}";
                }
            }
            catch { return "No disponible"; }
        }
    }

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
                ["study"] = new List<Playlist> { new Playlist { Name = "LoFi Girl (YouTube)", Url = "https://www.youtube.com/watch?v=jfKfPfyJRdk" }, new Playlist { Name = "Deep Focus (Spotify)", Url = "https://open.spotify.com/playlist/37i9dQZF1DWZeKCadgRdKQ" }, new Playlist { Name = "Piano Concentration (Spotify)", Url = "https://open.spotify.com/playlist/37i9dQZF1DX8Uebhn9wzrS" } },
                ["work"] = new List<Playlist> { new Playlist { Name = "Electronic Focus (Spotify)", Url = "https://open.spotify.com/playlist/37i9dQZF1DX8NTLI2TtZa6" }, new Playlist { Name = "Ambient Music for Work (YouTube)", Url = "https://www.youtube.com/watch?v=1fueZCTYkpA" }, new Playlist { Name = "Coding Mode (Spotify)", Url = "https://open.spotify.com/playlist/4U4N3rOcvNQWlJdUBwWQdU" } },
                ["creative"] = new List<Playlist> { new Playlist { Name = "Creative Flow (Spotify)", Url = "https://open.spotify.com/playlist/37i9dQZF1DWU6RkLn4WNxq" }, new Playlist { Name = "Dreamy & Soft Beats (YouTube)", Url = "https://www.youtube.com/watch?v=DWcJFNfaw9c" }, new Playlist { Name = "Brain Food (Spotify)", Url = "https://open.spotify.com/playlist/37i9dQZF1DX8Uebhn9wzrS" } }
            };
        }

        public async Task<FocusResult> AnalyzeFocusAsync(List<string> clipboardHistory, string taskDescription)
        {
            if (string.IsNullOrEmpty(_apiKey) || _apiKey.Contains("AQUÍ_VA_TU_API_KEY"))
                return new FocusResult { IsFocused = true, Message = "API Key no configurada, se asume enfoque." };

            string history = string.Join(", ", clipboardHistory.Select(item => $"'{item}'"));
            string prompt = $"Un usuario está trabajando en una tarea descrita como '{taskDescription}'. Este es un historial de textos que ha copiado recientemente: [{history}]. Basado en esto, ¿parece estar enfocado en su tarea o distraído? Responde únicamente con un objeto JSON con el formato: {{\"is_focused\": boolean, \"message\": \"un mensaje muy corto y amigable\"}}. Si está enfocado, el mensaje debe ser motivacional. Si está distraído, el mensaje debe animarlo a reenfocarse.";

            var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key={_apiKey}";
            var payload = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
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
            catch (Exception ex)
            {
                return new FocusResult { IsFocused = true, Message = $"Error de análisis: {ex.Message}" };
            }

            return new FocusResult { IsFocused = true, Message = "No se pudo analizar, se asume enfoque." };
        }

        public async Task<Playlist> GetPlaylistRecommendation(string mode, string taskDescription)
        {
            string normalizedMode = mode.ToLower().Trim();
            if (_predefinedPlaylists.TryGetValue(normalizedMode, out var playlists))
            {
                return playlists[new Random().Next(playlists.Count)];
            }
            string prompt = $"Basado en la siguiente descripción de tarea, recomienda solo el GÉNERO o TIPO de música ideal para la concentración (ejemplo: 'Música Clásica', 'Lofi Beats', 'Sonidos de la Naturaleza'). No des URLs ni nombres de playlists específicas. Tarea: '{taskDescription}'";
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
                var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(apiUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                }
                return "Rock Instrumental (Error de API)";
            }
            catch { return "Sonidos de Lluvia (Excepción)"; }
        }

        public Task<string> GetMotivationalQuote() => Task.FromResult(new[] { "El secreto para salir adelante es empezar.", "La disciplina es el puente entre las metas y los logros." }[new Random().Next(2)]);
        public Task<string> GetTechFact() => Task.FromResult(new[] { "El primer 'bug' informático fue literalmente una polilla.", "El primer disco duro de 1GB pesaba más de 227 kg." }[new Random().Next(2)]);
    }
}
