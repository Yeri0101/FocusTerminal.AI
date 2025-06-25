using System;
using System.IO;
using System.Linq;
using System.Threading;
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

        private int _totalFocusChecks = 0;
        private int _successfulFocusChecks = 0;
        private DateTime _sessionStartTime;

        public SessionManager()
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            string apiKey = config["ApiKey"];

            if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("AQUÍ_VA_TU_API_KEY"))
            {
                ConsoleHelper.ShowPopup("Erreur de Configuration", "La clé API Gemini n'est pas configurée dans appsettings.json. Les fonctions d'IA seront limitées.", ConsoleColor.Red);
            }

            _geminiService = new GeminiService(apiKey);
            _focusMonitor = new FocusMonitor();
            _weatherService = new WeatherService();
            _isSessionActive = false;
        }

        public async Task Start()
        {
            _sessionStartTime = DateTime.Now;
            new Clipboard().SetText(string.Empty);
            PrintWelcomeMessage();

            if (!SetupTask())
            {
                PrintGoodbyeMessage();
                return;
            }

            Console.WriteLine($"\nDémarrage de la session '{_currentTask.Mode}'...");

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
            Console.WriteLine($"\n--- Session '{_currentTask.Mode}' démarrée ---");
            Console.Write($"🌤️  Météo actuelle à Lausanne: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{weather}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"Tâche: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{_currentTask.Name} | Mode: {_currentTask.Mode} | Intervalle: {_currentTask.IntervalDurationMinutes} min");

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("\nRecommandation musicale prête:");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"💡 Suggestion: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"Pour votre tâche, je recommande : '{_recommendedPlaylist.Name}'");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("\n   Lien: ");
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
                    Console.Write($"Progression: {progressBar} {progressPercentage:F0}%");
                    Console.ResetColor();
                    Console.Write(" | ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("Temps restant: ");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write($"{remainingTime:mm\\:ss}");
                    Console.ResetColor();
                    Console.Write(" | Commandes: ");
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
                                await StopSession();
                                break;
                            case "2":
                                TimeSpan activeTime = DateTime.Now - intervalStartTime;
                                _focusMonitor.PauseMonitoring();
                                await ShowPauseSummary(activeTime, remainingTime);

                                var pauseResult = await HandlePause(endTime);
                                if (!pauseResult.sessionShouldContinue)
                                {
                                    await StopSession();
                                }
                                else
                                {
                                    endTime = pauseResult.newEndTime;
                                    _focusMonitor.ResumeMonitoring();
                                    Console.WriteLine("\nReprise de la session...");
                                }
                                break;
                        }
                    }
                    await Task.Delay(1000);
                }

                if (_isSessionActive)
                {
                    await OnIntervalElapsed();
                    Console.Write("Démarrer un autre intervalle ? (o/n): ");
                    if (Console.ReadLine()?.ToLower() != "o")
                    {
                        await StopSession();
                    }
                }
            }
        }

        private async Task StopSession()
        {
            Console.Write("\nVoulez-vous supprimer la tâche enregistrée en quittant ? (o/n, 'n' la sauvegardera par défaut): ");
            string deleteChoice = Console.ReadLine()?.ToLower().Trim();
            if (deleteChoice == "o")
            {
                if (File.Exists(TASK_CONFIG_FILE)) { File.Delete(TASK_CONFIG_FILE); Console.WriteLine("Tâche supprimée."); }
            }
            else { Console.WriteLine("Tâche sauvegardée pour la prochaine fois."); }
            _isSessionActive = false;
            await ShowSessionSummary();
        }

        private async Task PerformFocusCheck()
        {
            var clipboardHistory = _focusMonitor.GetAndClearClipboardHistory();
            var kpmHistory = _focusMonitor.GetKpmHistory();
            var sampledWords = _focusMonitor.GetAndClearSampledWordsHistory();

            if (clipboardHistory.Count == 0 && kpmHistory.All(k => k == 0) && sampledWords.Count == 0) return;

            Console.WriteLine();
            FocusResult result = await _geminiService.AnalyzeFocusAsync(clipboardHistory, kpmHistory, sampledWords, _currentTask.Description);

            _totalFocusChecks++;
            if (result.IsFocused)
            {
                _successfulFocusChecks++;
            }

            string title = result.IsFocused ? "Bon Travail !" : "Rappel de Concentration";
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
            Console.WriteLine("--- SESSION EN PAUSE ---");

            string weather = await _weatherService.GetWeatherAsync("Lausanne");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("⏱️  Temps actif: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{activeTime:mm} min");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("📍  Mode: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{_currentTask.Mode} ({_currentTask.Description})");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("⏳  Temps restant de l'intervalle: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{remainingTime:mm\\:ss}");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("🌤️  Météo actuelle à Lausanne: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{weather}");

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("🎵  Musique suggérée: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"{_recommendedPlaylist.Name} (");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write($"{_recommendedPlaylist.Url}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(")");

            Console.ResetColor();
            Console.WriteLine("\nAppuyez sur [2] pour continuer.");
            Console.WriteLine("Appuyez sur [1] pour terminer la session.");
        }

        private bool SetupTask()
        {
            if (File.Exists(TASK_CONFIG_FILE))
            {
                var lines = File.ReadAllLines(TASK_CONFIG_FILE);
                string taskName = lines.Length > 0 ? lines[0] : "tâche sauvegardée";
                Console.WriteLine($"\nUne tâche sauvegardée a été trouvée : '{taskName}'");
                Console.WriteLine("Que souhaitez-vous faire ?");
                Console.WriteLine("  [1] Continuer avec cette tâche");
                Console.WriteLine("  [2] Démarrer une nouvelle tâche (l'ancienne sera supprimée)");
                Console.WriteLine("  [3] Quitter");
                Console.Write("Choisissez une option: ");
                while (true)
                {
                    string choice = Console.ReadLine()?.Trim();
                    switch (choice)
                    {
                        case "1": LoadTask(); Console.WriteLine("Tâche précédente chargée."); return true;
                        case "2": CreateNewTask(); return true;
                        case "3": return false;
                        default: Console.Write("Option non valide. Veuillez choisir 1, 2 ou 3: "); break;
                    }
                }
            }
            else { CreateNewTask(); return true; }
        }

        private void CreateNewTask()
        {
            Console.WriteLine("\n--- Création d'une Nouvelle Tâche ---");

            string name = GetValidatedStringInput("Nom de la tâche: ");
            string description = GetValidatedStringInput("Brève description de la tâche: ", 5);
            int interval = GetPositiveIntegerInput("Durée de l'intervalle de travail (minutes, 30 par défaut): ", 30);
            string mode = GetValidatedStringInput("Mode (ex: 'study', 'work', 'creative' ou personnalisé): ");

            _currentTask = new TaskDetails { Name = name, Description = description, IntervalDurationMinutes = interval, Mode = mode };
            SaveTask();
        }

        private string GetValidatedStringInput(string prompt, int minLength = 1)
        {
            string input;
            while (true)
            {
                Console.Write(prompt);
                input = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("L'entrée не может быть пустой. Пожалуйста, попробуйте еще раз.");
                    Console.ResetColor();
                    continue;
                }

                if (input.Length < minLength)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"L'entrée doit contenir au moins {minLength} caractères. Veuillez réessayer.");
                    Console.ResetColor();
                    continue;
                }

                return input;
            }
        }

        private int GetPositiveIntegerInput(string prompt, int defaultValue)
        {
            while (true)
            {
                Console.Write(prompt);
                string input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    return defaultValue;
                }

                if (int.TryParse(input, out int result) && result > 0)
                {
                    return result;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Entrée non valide. Vous devez entrer un nombre entier positif.");
                    Console.ResetColor();
                }
            }
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
                Name = lines.Length > 0 ? lines[0] : "Tâche sans nom",
                Description = lines.Length > 1 ? lines[1] : "",
                IntervalDurationMinutes = lines.Length > 2 && int.TryParse(lines[2], out int i) ? i : 30,
                Mode = lines.Length > 3 ? lines[3] : "work"
            };
        }

        private async Task OnIntervalElapsed()
        {
            Console.WriteLine();
            string message = new Random().Next(0, 2) == 0 ? $"💪 {await _geminiService.GetMotivationalQuote()}" : $"💡 Le saviez-vous ? {await _geminiService.GetTechFact()}";
            ConsoleHelper.ShowPopup("Fin de l'Intervalle", message + "\n\nPrenez une pause. Étirez-vous, regardez au loin, hydratez-vous.", ConsoleColor.Magenta);
        }

        private async Task ShowSessionSummary()
        {
            TimeSpan totalSessionDuration = DateTime.Now - _sessionStartTime;
            double focusPercentage = _totalFocusChecks > 0 ? ((double)_successfulFocusChecks / _totalFocusChecks) * 100 : 100;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n\n--- Résumé de la Session ---");
            Console.ResetColor();

            Console.WriteLine($"Tâche: '{_currentTask.Name}'");
            Console.WriteLine($"Durée totale de la session: {totalSessionDuration:hh\\:mm\\:ss}");
            Console.WriteLine($"Vérifications de concentration effectuées: {_totalFocusChecks}");
            Console.WriteLine($"Taux de concentration: {focusPercentage:F1}%");

            Console.WriteLine("\nUne dernière pensée pour vous:");
            Console.ForegroundColor = ConsoleColor.Cyan;
            string quote = await _geminiService.GetMotivationalQuote();
            Console.WriteLine($"🧠 \"{quote}\"");
            Console.ResetColor();
        }

        private void PrintWelcomeMessage()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================");
            Console.WriteLine("    Bienvenue sur FocusTerminal.AI");
            Console.WriteLine("===================================");
            Console.ResetColor();
        }

        private void PrintGoodbyeMessage() { Console.WriteLine("\nSession terminée. À la prochaine !"); }
    }
}