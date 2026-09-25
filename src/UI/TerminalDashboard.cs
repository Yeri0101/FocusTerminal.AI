using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Models;
using Spectre.Console;

namespace FocusTerminal.AI.UI
{
    public class TerminalDashboard
    {
        public void RenderWelcomeBanner(string providerName)
        {
            AnsiConsole.Clear();
            var rule = new Rule("[bold cyan]FocusTerminal.AI[/]")
            {
                Style = Style.Parse("cyan dim")
            };
            AnsiConsole.Write(rule);

            var figlet = new FigletText("FOCUS.AI")
                .Centered()
                .Color(Color.DeepSkyBlue1);
            AnsiConsole.Write(figlet);

            var panel = new Panel(
                new Markup($"[dim]Asistente inteligente de concentración y trabajo profundo[/]\n" +
                           $"[grey]Motor activo:[/] [bold green]{Markup.Escape(providerName)}[/] | [grey]Versión:[/] [bold]2.0 AAA[/]"))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("cyan dim"),
                Padding = new Padding(2, 0, 2, 0)
            };

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }

        public TaskDetails PromptTaskDetails(TaskDetails? existingTask)
        {
            if (existingTask != null)
            {
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"[yellow]Se encontró una tarea previa guardada:[/] [bold]{Markup.Escape(existingTask.Name)}[/]")
                        .PageSize(5)
                        .AddChoices(new[]
                        {
                            "▶️ Continuar con esta tarea",
                            "📝 Crear una nueva tarea",
                            "📊 Ver estadísticas históricas",
                            "⚙️ Configurar Proveedor de IA",
                            "❌ Salir"
                        }));

                if (choice.StartsWith("▶️"))
                {
                    return existingTask;
                }
                else if (choice.StartsWith("📊"))
                {
                    return new TaskDetails { Name = "__SHOW_STATS__" };
                }
                else if (choice.StartsWith("⚙️"))
                {
                    return new TaskDetails { Name = "__CONFIG_AI__" };
                }
                else if (choice.StartsWith("❌"))
                {
                    return new TaskDetails { Name = "__EXIT__" };
                }
            }
            else
            {
                var mainChoice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Selecciona una opción:")
                        .AddChoices(new[]
                        {
                            "📝 Iniciar una nueva tarea",
                            "📊 Ver estadísticas históricas",
                            "⚙️ Configurar Proveedor de IA",
                            "❌ Salir"
                        }));

                if (mainChoice.StartsWith("📊"))
                {
                    return new TaskDetails { Name = "__SHOW_STATS__" };
                }
                else if (mainChoice.StartsWith("⚙️"))
                {
                    return new TaskDetails { Name = "__CONFIG_AI__" };
                }
                else if (mainChoice.StartsWith("❌"))
                {
                    return new TaskDetails { Name = "__EXIT__" };
                }
            }

            AnsiConsole.MarkupLine("[bold cyan]— Nueva Tarea de Enfoque —[/]");

            var name = AnsiConsole.Prompt(
                new TextPrompt<string>("📌 [bold]Nombre de la tarea:[/] ")
                    .PromptStyle("green")
                    .ValidationErrorMessage("[red]El nombre no puede estar vacío[/]")
                    .Validate(n => !string.IsNullOrWhiteSpace(n)));

            var description = AnsiConsole.Prompt(
                new TextPrompt<string>("📝 [bold]Descripción breve o meta:[/] ")
                    .PromptStyle("green")
                    .AllowEmpty());

            var interval = AnsiConsole.Prompt(
                new TextPrompt<int>("⏱️ [bold]Minutos de intervalo:[/] ")
                    .PromptStyle("cyan")
                    .DefaultValue(30)
                    .Validate(val => val is > 0 and <= 180
                        ? ValidationResult.Success()
                        : ValidationResult.Error("[red]El intervalo debe ser entre 1 y 180 minutos[/]")));

            var mode = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("🎯 [bold]Elige el modo de trabajo:[/] ")
                    .PageSize(5)
                    .AddChoices(new[] { "work", "study", "creative", "custom" }));

            if (mode == "custom")
            {
                mode = AnsiConsole.Prompt(
                    new TextPrompt<string>("Escribe tu modo personalizado: ")
                        .DefaultValue("deepwork"));
            }

            return new TaskDetails
            {
                Name = name.Trim(),
                Description = description.Trim(),
                IntervalDurationMinutes = interval,
                Mode = mode.Trim()
            };
        }

        public void RenderSessionHeader(TaskDetails task, WeatherInfo weather, Playlist playlist, string provider)
        {
            var grid = new Grid();
            grid.AddColumn(new GridColumn().PadRight(2));
            grid.AddColumn(new GridColumn());

            grid.AddRow("[bold grey]Tarea:[/] " + Markup.Escape(task.Name),
                        "[bold grey]Modo:[/] [yellow]" + Markup.Escape(task.Mode) + "[/]");
            grid.AddRow("[bold grey]Intervalo:[/] " + task.IntervalDurationMinutes + " min",
                        "[bold grey]Clima:[/] " + Markup.Escape(weather.ToString()));
            grid.AddRow("[bold grey]Música:[/] [cyan]" + Markup.Escape(playlist.Name) + "[/]",
                        "[bold grey]Motor IA:[/] [green]" + Markup.Escape(provider) + "[/]");

            var panel = new Panel(grid)
            {
                Header = new PanelHeader("[bold cyan] Sesión Activa [/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("cyan"),
                Padding = new Padding(1, 1, 1, 1)
            };

            AnsiConsole.Write(panel);
            AnsiConsole.MarkupLine("[dim grey]Controles: [bold white][[Espacio]][/] Pausar/Reanudar  |  [bold white][[S]][/] Estadísticas  |  [bold white][[Q]][/] Finalizar[/]");
            AnsiConsole.WriteLine();
        }

        public void RenderFocusNotification(FocusResult result, int checkpointPercentage)
        {
            var color = result.IsFocused ? "green" : "yellow";
            var icon = result.IsFocused ? "✅" : "⚠️";
            var title = result.IsFocused ? "Enfoque Confirmado" : "Alerta de Distracción";

            var panel = new Panel(new Markup($"[{color}]{icon} {Markup.Escape(result.Message)}[/]\n[grey dim]Chequeo al {checkpointPercentage}% vía {Markup.Escape(result.ProviderName)}[/]"))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse(color),
                Padding = new Padding(1, 0, 1, 0)
            };

            AnsiConsole.Write(panel);
        }

        public void RenderPauseScreen(TimeSpan active, TimeSpan remaining, WeatherInfo weather, Playlist playlist)
        {
            AnsiConsole.WriteLine();
            var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Yellow);
            table.AddColumn("[bold yellow]Estado de la Pausa[/]");
            table.AddColumn("[bold yellow]Detalle[/]");

            table.AddRow("⏱️ Tiempo activo", $"{active.Minutes} min {active.Seconds} seg");
            table.AddRow("⏳ Tiempo restante", $"{remaining.Minutes} min {remaining.Seconds} seg");
            table.AddRow("🌤️ Clima actual", Markup.Escape(weather.ToString()));
            table.AddRow("🎵 Playlist sugerida", $"{Markup.Escape(playlist.Name)} ({Markup.Escape(playlist.Url)})");

            var panel = new Panel(table)
            {
                Header = new PanelHeader("[bold yellow] ⏸️ SESIÓN EN PAUSA [/]"),
                Padding = new Padding(1, 1, 1, 1)
            };

            AnsiConsole.Write(panel);
            AnsiConsole.MarkupLine("[bold white]Presiona [green][[Espacio]][/] para reanudar la sesión...[/]");
        }

        public void RenderSessionSummary(SessionHistory session, string quote, string techFact)
        {
            AnsiConsole.WriteLine();
            var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Green);
            table.AddColumn("[bold green]Métrica[/]");
            table.AddColumn("[bold green]Resultado[/]");

            table.AddRow("📌 Tarea", Markup.Escape(session.TaskName));
            table.AddRow("🎯 Modo", Markup.Escape(session.Mode));
            table.AddRow("⏱️ Tiempo Productivo", $"{session.CompletedMinutes:F1} minutos");
            table.AddRow("🔍 Chequeos de IA", $"{session.TotalChecks} ({session.FocusedChecks} enfocados, {session.DistractionChecks} distracciones)");

            string scoreColor = session.FocusScorePercentage >= 80 ? "green" : session.FocusScorePercentage >= 60 ? "yellow" : "red";
            table.AddRow("🏆 Focus Score", $"[{scoreColor} bold]{session.FocusScorePercentage}%[/]");

            var panel = new Panel(table)
            {
                Header = new PanelHeader("[bold green] 🏁 Resumen de la Sesión [/]"),
                Padding = new Padding(1, 1, 1, 1)
            };

            AnsiConsole.Write(panel);

            if (!string.IsNullOrWhiteSpace(quote))
            {
                AnsiConsole.MarkupLine($"[italic cyan]💬 \"{Markup.Escape(quote)}\"[/]");
            }
            if (!string.IsNullOrWhiteSpace(techFact))
            {
                AnsiConsole.MarkupLine($"[dim grey]💡 ¿Sabías que? {Markup.Escape(techFact)}[/]");
            }
            AnsiConsole.WriteLine();
        }

        public void RenderHistoricalStats(IReadOnlyList<SessionHistory> history, double totalHours)
        {
            AnsiConsole.Clear();
            var rule = new Rule("[bold cyan]Estadísticas de Productividad[/]");
            AnsiConsole.Write(rule);

            if (history.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]Aún no hay sesiones registradas en el historial.[/]");
                return;
            }

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Fecha");
            table.AddColumn("Tarea");
            table.AddColumn("Modo");
            table.AddColumn("Tiempo");
            table.AddColumn("Focus Score");

            foreach (var s in history.Take(10))
            {
                string scoreColor = s.FocusScorePercentage >= 80 ? "green" : s.FocusScorePercentage >= 60 ? "yellow" : "red";
                table.AddRow(
                    s.StartTime.ToLocalTime().ToString("dd/MM HH:mm"),
                    Markup.Escape(s.TaskName),
                    Markup.Escape(s.Mode),
                    $"{s.CompletedMinutes:F0} min",
                    $"[{scoreColor}]{s.FocusScorePercentage}%[/]"
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"[bold]Total acumulado de trabajo profundo:[/] [green bold]{totalHours} horas[/] en {history.Count} sesiones.");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Presiona cualquier tecla para continuar...[/]");
            Console.ReadKey(true);
        }
    }
}
