using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FocusTerminal.AI.Helpers;
using Microsoft.Extensions.Configuration;
using TextCopy;

namespace FocusTerminal.AI
{
    /// <summary>
    /// Orquesta el flujo principal de la aplicación, gestionando la sesión del usuario.
    /// </summary>
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
}
