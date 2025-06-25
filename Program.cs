using Microsoft.Extensions.Configuration;
using SharpHook;
using SharpHook.Data;
using SharpHook.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TextCopy;

namespace FocusTerminal.AI
{
    public class Program
    {
        [STAThread]
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
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
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            string apiKey = config["ApiKey"];

            if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("AQUÍ_VA_TU_API_KEY"))
            {
                ConsoleHelper.ShowPopup("Error de Configuración", "La API Key de Gemini no está configurada en appsettings.json. Las funciones de IA estarán limitadas.", ConsoleColor.Red);
            }

            _geminiService = new GeminiService(apiKey);
            _focusMonitor = new FocusMonitor();
            _weatherService = new WeatherService();
            _isSessionActive = false;
        }

        public async Task Start()
        {
            new Clipboard().SetText(string.Empty);
            PrintWelcomeMessage();

            if (!SetupTask())
            {
                PrintGoodbyeMessage();
                return;
            }

            Console.WriteLine($"\nIniciando sección de '{_currentTask.Mode}'...");

            var weatherTask = _weatherService.GetWeatherAsync("Lausanne");
            var playlistTask = _geminiService.GetPlaylistRecommendation(_currentTask.Mode, _currentTask.Description);

            await Task.WhenAll(weatherTask, playlistTask);

            string weather = await weatherTask;
            _recommendedPlaylist = await playlistTask;

            if (Console.CursorTop > 0)
            {
                Console.SetCursorPosition(0, Console.CursorTop - 1);
            }
            Console.Write(new string(' ', Console.WindowWidth > 0 ? Console.WindowWidth - 1 : 0) + "\r");

            // --- INICIO DE MODIFICACIÓN DE COLORES ---
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"\n--- Sesión de '{_currentTask.Mode}' Iniciada ---");
            Console.Write($"🌤️  Clima actual en Lausanne: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{weather}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"Tarea: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{_currentTask.Name} | Modo: {_currentTask.Mode} | Intervalo: {_currentTask.IntervalDurationMinutes} min");

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("\nRecomendación musical lista:");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"💡 Sugerencia: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"Para tu tarea, te recomiendo: '{_recommendedPlaylist.Name}'");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("\n   Enlace: ");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(_recommendedPlaylist.Url);
            Console.ResetColor();
            // --- FIN DE MODIFICACIÓN DE COLORES ---

            _isSessionActive = true;

            var monitorCts = new CancellationTokenSource();
            _focusMonitor.StartMonitoring(monitorCts.Token);

            await RunRealtimeSession();

            monitorCts.Cancel();
            _focusMonitor.StopMonitoring();
            PrintGoodbyeMessage();
        }

        private async Task RunRealtimeSession()
        {
            while (_isSessionActive)
            {
                _focusMonitor.ClearAllHistory();
                DateTime intervalStartTime = DateTime.Now;
                DateTime endTime = intervalStartTime.AddMinutes(_currentTask.IntervalDurationMinutes);
                _nextCheckPointPercentage = 15;

                while (DateTime.Now < endTime && _isSessionActive)
                {
                    TimeSpan remainingTime = endTime - DateTime.Now;
                    double totalSeconds = (endTime - intervalStartTime).TotalSeconds;
                    double elapsedSeconds = (DateTime.Now - intervalStartTime).TotalSeconds;
                    double progressPercentage = (elapsedSeconds / totalSeconds) * 100;

                    string progressBar = ConsoleHelper.CreateProgressBar(progressPercentage);

                    Console.Write("\r");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"Progreso: {progressBar} {progressPercentage:F0}%");
                    Console.ResetColor();
                    Console.Write(" | ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("Tiempo restante: ");
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write($"{remainingTime:mm\\:ss}");
                    Console.ResetColor();
                    Console.Write(" | Comandos: ");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("[1] Stop");
                    Console.ResetColor();
                    Console.Write(" ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("[2] Pause ");
                    Console.ResetColor();

                    if (progressPercentage >= _nextCheckPointPercentage)
                    {
                        await PerformFocusCheck();
                        _nextCheckPointPercentage += 15;
                    }

                    if (Console.KeyAvailable)
                    {
                        string command = Console.ReadLine()?.ToLower().Trim() ?? "";
                        switch (command)
                        {
                            case "1":
                                StopSession();
                                break;
                            case "2":
                                TimeSpan activeTime = DateTime.Now - intervalStartTime;
                                _focusMonitor.PauseMonitoring();
                                await ShowPauseSummary(activeTime, remainingTime);

                                var pauseResult = await HandlePause(endTime);
                                if (!pauseResult.sessionShouldContinue)
                                {
                                    StopSession();
                                }
                                else
                                {
                                    endTime = pauseResult.newEndTime;
                                    _focusMonitor.ResumeMonitoring();
                                    Console.WriteLine("\nReanudando sesión...");
                                }
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
                        StopSession();
                    }
                }
            }
        }

        private void StopSession()
        {
            Console.Write("\n¿Deseas eliminar la tarea guardada al salir? (s/n, 'n' por defecto la guardará): ");
            string deleteChoice = Console.ReadLine()?.ToLower().Trim();
            if (deleteChoice == "s")
            {
                if (File.Exists(TASK_CONFIG_FILE)) { File.Delete(TASK_CONFIG_FILE); Console.WriteLine("Tarea eliminada."); }
            }
            else { Console.WriteLine("Tarea guardada para la próxima vez."); }
            _isSessionActive = false;
            ShowSessionSummary();
        }

        private async Task PerformFocusCheck()
        {
            var clipboardHistory = _focusMonitor.GetAndClearClipboardHistory();
            var kpmHistory = _focusMonitor.GetKpmHistory();
            var sampledWords = _focusMonitor.GetAndClearSampledWordsHistory();

            if (clipboardHistory.Count == 0 && kpmHistory.All(k => k == 0) && sampledWords.Count == 0) return;

            Console.WriteLine();
            FocusResult result = await _geminiService.AnalyzeFocusAsync(clipboardHistory, kpmHistory, sampledWords, _currentTask.Description);

            string title = result.IsFocused ? "¡Buen Trabajo!" : "Recordatorio de Enfoque";
            ConsoleColor color = result.IsFocused ? ConsoleColor.Green : ConsoleColor.Yellow;

            ConsoleHelper.ShowPopup(title, result.Message, color);
        }

        private async Task<(DateTime newEndTime, bool sessionShouldContinue)> HandlePause(DateTime currentEndTime)
        {
            DateTime pauseStartTime = DateTime.Now;
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    string command = Console.ReadLine()?.ToLower().Trim() ?? "";
                    switch (command)
                    {
                        case "2":
                            DateTime pauseEndTimeResume = DateTime.Now;
                            TimeSpan pauseDurationResume = pauseEndTimeResume - pauseStartTime;
                            return (currentEndTime.Add(pauseDurationResume), true);
                        case "1":
                            return (currentEndTime, false);
                    }
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

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("⏱️  Tiempo activo: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{activeTime:mm} min");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("📍  Modo: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{_currentTask.Mode} ({_currentTask.Description})");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("⏳  Tiempo restante del intervalo: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{remainingTime:mm\\:ss}");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("🌤️  Clima actual en Lausanne: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{weather}");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("🎵  Música sugerida: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"{_recommendedPlaylist.Name} (");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write($"{_recommendedPlaylist.Url}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(")");

            Console.ResetColor();
            Console.WriteLine("\n[2] para continuar.");
            Console.WriteLine("[1] para finalizar la sesión.");
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
                        case "1": LoadTask(); Console.WriteLine("Tarea anterior cargada."); return true;
                        case "2": CreateNewTask(); return true;
                        case "3": return false;
                        default: Console.Write("Opción no válida. Por favor, elige 1, 2 o 3: "); break;
                    }
                }
            }
            else { CreateNewTask(); return true; }
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
            _currentTask = new TaskDetails { Name = name, Description = description, IntervalDurationMinutes = interval, Mode = mode };
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
            string message = new Random().Next(0, 2) == 0 ? $"💪 {await _geminiService.GetMotivationalQuote()}" : $"💡 ¿Sabías que? {await _geminiService.GetTechFact()}";
            ConsoleHelper.ShowPopup("Fin del Intervalo", message + "\n\nTómate un descanso. Estira, mira a lo lejos, hidrátate.", ConsoleColor.Magenta);
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
        }

        private void PrintGoodbyeMessage() { Console.WriteLine("\n¡Sesión finalizada. Hasta la próxima!"); }
    }

    public class Playlist { public string Name { get; set; } public string Url { get; set; } }
    public class TaskDetails { public string Name { get; set; } public string Description { get; set; } public int IntervalDurationMinutes { get; set; } public string Mode { get; set; } }
    public class FocusResult { [JsonPropertyName("is_focused")] public bool IsFocused { get; set; } [JsonPropertyName("message")] public string Message { get; set; } }

    public class FocusMonitor
    {
        private readonly List<string> _clipboardHistory = new List<string>();
        private readonly HashSet<string> _processedClipboardTexts = new HashSet<string>();
        private readonly TaskPoolGlobalHook _hook;
        private readonly List<int> _kpmHistory = new List<int>();
        private int _keystrokeCount = 0;
        private Timer _kpmTimer;
        private bool _isPaused = false;
        private readonly StringBuilder _currentWord = new StringBuilder();
        private readonly List<string> _wordBuffer = new List<string>();
        private readonly List<string> _sampledWordsHistory = new List<string>();
        private const int WORD_BUFFER_SIZE = 30;
        private const int SAMPLE_SIZE = 6;

        public FocusMonitor()
        {
            _hook = new TaskPoolGlobalHook();
        }

        public void StartMonitoring(CancellationToken token)
        {
            _hook.KeyPressed += OnKeyPressed;
            _kpmTimer = new Timer(RecordKpm, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            Task.Run(() => _hook.RunAsync(), token);
            Task.Run(async () => {
                while (!token.IsCancellationRequested) { await CollectClipboardData(); await Task.Delay(2000, token); }
            }, token);
        }

        private void OnKeyPressed(object sender, KeyboardHookEventArgs e)
        {
            if (_isPaused) return;
            _keystrokeCount++;
            char keyChar = (char)e.Data.KeyChar;
            if (char.IsLetterOrDigit(keyChar))
            {
                _currentWord.Append(keyChar);
            }
            else if ((e.Data.KeyCode == KeyCode.VcSpace || e.Data.KeyCode == KeyCode.VcEnter) && _currentWord.Length > 0)
            {
                ProcessWord(_currentWord.ToString().ToLower());
                _currentWord.Clear();
            }
        }

        private void ProcessWord(string word)
        {
            _wordBuffer.Add(word);
            if (_wordBuffer.Count >= WORD_BUFFER_SIZE)
            {
                var random = new Random();
                var sample = _wordBuffer.OrderBy(x => random.Next()).Take(SAMPLE_SIZE).ToList();
                _sampledWordsHistory.AddRange(sample);
                _wordBuffer.Clear();
            }
        }

        private void RecordKpm(object state)
        {
            if (!_isPaused)
            {
                _kpmHistory.Add(_keystrokeCount);
                _keystrokeCount = 0;
                if (_kpmHistory.Count > 10) _kpmHistory.RemoveAt(0);
            }
        }

        public void StopMonitoring()
        {
            _hook.KeyPressed -= OnKeyPressed;
            _kpmTimer?.Dispose();
            _hook.Dispose();
        }

        public void PauseMonitoring() => _isPaused = true;
        public void ResumeMonitoring() => _isPaused = false;

        private async Task CollectClipboardData()
        {
            if (_isPaused) return;
            try
            {
                var clipboard = new Clipboard();
                string currentClipboardText = await clipboard.GetTextAsync();

                if (!string.IsNullOrEmpty(currentClipboardText) && !_processedClipboardTexts.Contains(currentClipboardText))
                {
                    string truncatedText = currentClipboardText.Substring(0, Math.Min(currentClipboardText.Length, 100));
                    _clipboardHistory.Add(truncatedText);
                    _processedClipboardTexts.Add(currentClipboardText);
                }
            }
            catch { /* Ignorar errores */ }
        }

        public List<string> GetAndClearClipboardHistory()
        {
            var history = new List<string>(_clipboardHistory);
            _clipboardHistory.Clear();
            return history;
        }

        public List<int> GetKpmHistory() => new List<int>(_kpmHistory);

        public List<string> GetAndClearSampledWordsHistory()
        {
            var history = new List<string>(_sampledWordsHistory);
            _sampledWordsHistory.Clear();
            return history;
        }

        public void ClearAllHistory()
        {
            _clipboardHistory.Clear();
            _processedClipboardTexts.Clear();
            _kpmHistory.Clear();
            _sampledWordsHistory.Clear();
            _wordBuffer.Clear();
            _currentWord.Clear();
            _keystrokeCount = 0;
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
        private readonly string _apiKey; private readonly HttpClient _httpClient; private readonly Dictionary<string, List<Playlist>> _predefinedPlaylists;
        public GeminiService(string key)
        {
            _apiKey = key; _httpClient = new HttpClient();
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
                var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
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

    public static class SoundHelper
    {
        [SupportedOSPlatform("windows")]
        public static void PlayNotificationSound()
        {
            try
            {
                using (var player = new SoundPlayer("notification.wav")) { player.Play(); }
            }
            catch (FileNotFoundException) { Console.Beep(); }
            catch (Exception) { /* Ignorar otros errores */ }
        }
    }

    public static class ConsoleHelper
    {
        public static void ShowPopup(string title, string message, ConsoleColor borderColor)
        {
            SoundHelper.PlayNotificationSound();
            Console.WriteLine();
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = borderColor;
            int width = 60;
            string titleBar = $" {title} ".PadRight(width - 2, '═');
            Console.WriteLine($"╔═{titleBar}╗");
            var messageLines = WordWrap(message, width - 4);
            foreach (var line in messageLines)
            {
                Console.WriteLine($"║ {line.PadRight(width - 4)} ║");
            }
            Console.WriteLine($"╚{"".PadRight(width - 2, '═')}╝");
            Console.ForegroundColor = originalColor;
            Console.WriteLine();
        }

        public static string CreateProgressBar(double percentage)
        {
            int width = 20;
            int filledBlocks = (int)(percentage / 100 * width);
            var bar = new System.Text.StringBuilder("[");
            bar.Append(new string('█', filledBlocks));
            bar.Append(new string('░', width - filledBlocks));
            bar.Append("]");
            return bar.ToString();
        }

        private static List<string> WordWrap(string text, int maxWidth)
        {
            var lines = new List<string>();
            var words = text.Replace("\n", " \n ").Split(' ');
            var currentLine = "";
            foreach (var word in words)
            {
                if (word == "\n") { lines.Add(currentLine); currentLine = ""; continue; }
                if ((currentLine + " " + word).Length > maxWidth) { lines.Add(currentLine); currentLine = word; }
                else { currentLine += (string.IsNullOrEmpty(currentLine) ? "" : " ") + word; }
            }
            if (!string.IsNullOrEmpty(currentLine)) lines.Add(currentLine);
            return lines;
        }
    }
}
