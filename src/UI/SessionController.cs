using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Interfaces;
using FocusTerminal.AI.Core.Models;
using FocusTerminal.AI.Services;
using Spectre.Console;

namespace FocusTerminal.AI.UI
{
    public class SessionController
    {
        private readonly IAiFocusEngine _aiEngine;
        private readonly IFocusMonitor _focusMonitor;
        private readonly IWeatherService _weatherService;
        private readonly IStorageService _storageService;
        private readonly AudioNotifier _audioNotifier;
        private readonly TerminalDashboard _dashboard;

        private TaskDetails _task = new();
        private bool _isActive;
        private int _totalChecks;
        private int _focusedChecks;
        private int _distractionChecks;

        public SessionController(
            IAiFocusEngine aiEngine,
            IFocusMonitor focusMonitor,
            IWeatherService weatherService,
            IStorageService storageService,
            AudioNotifier audioNotifier,
            TerminalDashboard dashboard)
        {
            _aiEngine = aiEngine;
            _focusMonitor = focusMonitor;
            _weatherService = weatherService;
            _storageService = storageService;
            _audioNotifier = audioNotifier;
            _dashboard = dashboard;
        }

        public async Task RunAsync()
        {
            _dashboard.RenderWelcomeBanner(_aiEngine.ProviderName);

            while (true)
            {
                var savedTask = await _storageService.LoadSavedTaskAsync();
                var taskDetails = _dashboard.PromptTaskDetails(savedTask);

                if (taskDetails.Name == "__EXIT__")
                {
                    AnsiConsole.MarkupLine("[cyan]¡Hasta la próxima sesión de enfoque![/]");
                    return;
                }

                if (taskDetails.Name == "__SHOW_STATS__")
                {
                    var history = await _storageService.GetSessionHistoryAsync(20);
                    var totalHours = await _storageService.GetTotalFocusHoursAsync();
                    _dashboard.RenderHistoricalStats(history, totalHours);
                    _dashboard.RenderWelcomeBanner(_aiEngine.ProviderName);
                    continue;
                }

                _task = taskDetails;
                await _storageService.SaveTaskAsync(_task);
                break;
            }

            await StartFocusSessionAsync();
        }

        private async Task StartFocusSessionAsync()
        {
            var weather = await _weatherService.GetWeatherAsync();
            var playlist = await _aiEngine.GetPlaylistRecommendationAsync(_task.Mode, _task.Description);

            _dashboard.RenderSessionHeader(_task, weather, playlist, _aiEngine.ProviderName);

            var monitorCts = new CancellationTokenSource();
            _focusMonitor.StartMonitoring(monitorCts.Token);
            _isActive = true;

            var sessionStart = DateTime.UtcNow;
            double accumulatedMinutes = 0;

            try
            {
                while (_isActive)
                {
                    var intervalStart = DateTime.Now;
                    var intervalMinutes = _task.IntervalDurationMinutes;
                    var targetEndTime = intervalStart.AddMinutes(intervalMinutes);
                    int nextCheckPercentage = 15;
                    double totalSeconds = intervalMinutes * 60.0;

                    while (DateTime.Now < targetEndTime && _isActive)
                    {
                        var remaining = targetEndTime - DateTime.Now;
                        var elapsed = DateTime.Now - intervalStart;
                        int progress = Math.Clamp((int)((elapsed.TotalSeconds / totalSeconds) * 100), 0, 100);

                        // Renderizado de línea limpia con barra de progreso
                        string progressBar = CreateProgressBar(progress, 20);
                        Console.Write($"\r⏱️  [ {remaining:mm\\:ss} ] {progressBar} {progress}%  | [Espacio]: Pausa  [Q]: Salir   ");

                        if (progress >= nextCheckPercentage)
                        {
                            Console.WriteLine();
                            await TriggerFocusCheckAsync(nextCheckPercentage);
                            nextCheckPercentage += 15;
                        }

                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(intercept: true);
                            if (key.Key == ConsoleKey.Spacebar)
                            {
                                targetEndTime = await HandlePauseAsync(intervalStart, targetEndTime, weather, playlist);
                            }
                            else if (key.Key == ConsoleKey.Q)
                            {
                                Console.WriteLine();
                                _isActive = false;
                                break;
                            }
                            else if (key.Key == ConsoleKey.S)
                            {
                                Console.WriteLine();
                                AnsiConsole.MarkupLine($"[grey dim]Chequeos actuales: {_focusedChecks}/{_totalChecks} enfocados.[/]");
                            }
                        }

                        await Task.Delay(500);
                    }

                    accumulatedMinutes += (DateTime.Now - intervalStart).TotalMinutes;

                    if (_isActive)
                    {
                        Console.WriteLine();
                        _audioNotifier.PlayIntervalFinishedAlert();
                        AnsiConsole.MarkupLine("[bold magenta]🎉 ¡Intervalo completado! Tómate un respiro, estira y bebe agua.[/]");

                        var quote = await _aiEngine.GetMotivationalQuoteAsync();
                        var fact = await _aiEngine.GetTechFactAsync();
                        AnsiConsole.MarkupLine($"[italic cyan]\"{Markup.Escape(quote)}\"[/]");
                        AnsiConsole.MarkupLine($"[grey]💡 {Markup.Escape(fact)}[/]");

                        var repeat = AnsiConsole.Confirm("¿Deseas iniciar otro intervalo de enfoque?", defaultValue: true);
                        if (!repeat)
                        {
                            _isActive = false;
                        }
                    }
                }
            }
            finally
            {
                monitorCts.Cancel();
            }

            var sessionEnd = DateTime.UtcNow;
            var historyRecord = new SessionHistory
            {
                TaskName = _task.Name,
                Mode = _task.Mode,
                StartTime = sessionStart,
                EndTime = sessionEnd,
                PlannedMinutes = _task.IntervalDurationMinutes,
                CompletedMinutes = Math.Round(accumulatedMinutes, 1),
                TotalChecks = _totalChecks,
                FocusedChecks = _focusedChecks,
                DistractionChecks = _distractionChecks
            };

            await _storageService.SaveSessionHistoryAsync(historyRecord);

            var finishQuote = await _aiEngine.GetMotivationalQuoteAsync();
            var finishFact = await _aiEngine.GetTechFactAsync();
            _dashboard.RenderSessionSummary(historyRecord, finishQuote, finishFact);

            var deleteChoice = AnsiConsole.Confirm("¿Deseas eliminar la tarea guardada ahora que finalizaste?", defaultValue: false);
            if (deleteChoice)
            {
                await _storageService.DeleteSavedTaskAsync();
                AnsiConsole.MarkupLine("[grey]Tarea anterior removida del almacenamiento.[/]");
            }
        }

        private async Task TriggerFocusCheckAsync(int percentage)
        {
            var history = _focusMonitor.GetAndClearClipboardHistory();
            if (history.Count == 0) return;

            var result = await _aiEngine.AnalyzeFocusAsync(history, _task.Description);

            _totalChecks++;
            if (result.IsFocused)
            {
                _focusedChecks++;
            }
            else
            {
                _distractionChecks++;
                _audioNotifier.PlayFocusWarningAlert();
            }

            _dashboard.RenderFocusNotification(result, percentage);
        }

        private async Task<DateTime> HandlePauseAsync(DateTime intervalStart, DateTime currentEndTime, WeatherInfo weather, Playlist playlist)
        {
            Console.WriteLine();
            var pauseStart = DateTime.Now;
            var activeTime = pauseStart - intervalStart;
            var remainingTime = currentEndTime - pauseStart;

            _dashboard.RenderPauseScreen(activeTime, remainingTime, weather, playlist);

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key is ConsoleKey.Spacebar or ConsoleKey.Enter)
                    {
                        var pauseDuration = DateTime.Now - pauseStart;
                        AnsiConsole.MarkupLine("[green]▶️ Reanudando sesión de concentración...[/]");
                        return currentEndTime.Add(pauseDuration);
                    }
                }
                await Task.Delay(100);
            }
        }

        private static string CreateProgressBar(int percentage, int width)
        {
            int filled = (int)Math.Round((percentage / 100.0) * width);
            filled = Math.Clamp(filled, 0, width);
            int empty = width - filled;
            return $"{new string('■', filled)}{new string('░', empty)}";
        }
    }
}
