using System;
using System.Net.Http;
using System.Threading.Tasks;
using FocusTerminal.AI.AI;
using FocusTerminal.AI.Core.Models;
using FocusTerminal.AI.Services;
using Spectre.Console;

namespace FocusTerminal.AI.UI
{
    public class AiSetupWizard
    {
        private readonly ConfigService _configService;

        public AiSetupWizard(ConfigService configService)
        {
            _configService = configService;
        }

        public async Task RunSetupWizardAsync(AppSettings settings, SmartAiRouter router)
        {
            AnsiConsole.Clear();
            var rule = new Rule("[bold cyan]Configuración de Proveedor de Inteligencia Artificial[/]")
            {
                Style = Style.Parse("cyan dim")
            };
            AnsiConsole.Write(rule);

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Selecciona cómo deseas que FocusTerminal analice tu concentración:")
                    .PageSize(6)
                    .AddChoices(new[]
                    {
                        "☁️  Google Gemini (Recomendado - Gratuito en Google AI Studio)",
                        "🌐 OpenRouter (Modelos gratuitos y comerciales: Llama 3.2, etc.)",
                        "💻 Ollama Local (100% privado en tu PC, sin internet)",
                        "🔌 API Custom / LM Studio (Compatible con OpenAI)",
                        "🛡️  Motor Heurístico Offline (Sin IA externa, 0 configuración)",
                        "🔙 Volver al menú principal"
                    }));

            if (choice.StartsWith("☁️"))
            {
                await ConfigureGeminiAsync(settings, router);
            }
            else if (choice.StartsWith("🌐"))
            {
                await ConfigureOpenRouterAsync(settings, router);
            }
            else if (choice.StartsWith("💻"))
            {
                await ConfigureOllamaAsync(settings, router);
            }
            else if (choice.StartsWith("🔌"))
            {
                await ConfigureCustomApiAsync(settings, router);
            }
            else if (choice.StartsWith("🛡️"))
            {
                settings.AiProvider = "Heuristic";
                await _configService.SaveSettingsAsync(settings);
                AnsiConsole.MarkupLine("[green]✓ Modo Heurístico Offline configurado como predeterminado.[/]");
                await Task.Delay(1200);
            }
        }

        public async Task<bool> PromptIfNoAiConfiguredAsync(AppSettings settings, SmartAiRouter router)
        {
            // Si el usuario ya eligió explícitamente "Heuristic", respetar su decisión
            if (string.Equals(settings.AiProvider, "Heuristic", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 1. Probar si Ollama está corriendo en local
            var ollama = new OllamaAiEngine(settings.Ollama);
            if (await ollama.IsAvailableAsync())
            {
                return true;
            }

            // 2. Probar si Gemini está configurado
            var gemini = new GeminiAiEngine(settings.Gemini);
            if (await gemini.IsAvailableAsync())
            {
                return true;
            }

            // 3. Probar si OpenRouter está configurado
            var openRouter = new OpenRouterAiEngine(settings.OpenRouter);
            if (await openRouter.IsAvailableAsync())
            {
                return true;
            }

            // 4. Probar si Custom API está configurada
            var custom = new CustomOpenAiEngine(settings.CustomApi);
            if (await custom.IsAvailableAsync())
            {
                return true;
            }

            // Si ninguno está disponible, alertar y dar la opción de configurar
            AnsiConsole.WriteLine();
            var panel = new Panel(
                new Markup("[bold yellow]⚠️ No se detectó ningún modelo de IA activo.[/]\n\n" +
                           "[grey]Opciones detectadas:[/]\n" +
                           "  • [dim]Ollama Local:[/] No está iniciado en http://localhost:11434\n" +
                           "  • [dim]Google Gemini / OpenRouter:[/] Sin API Key configurada\n\n" +
                           "[white]FocusTerminal puede usar una IA en la nube, un modelo local o su motor heurístico offline.[/]"))
            {
                Header = new PanelHeader("[bold yellow] Asistente de IA Inicial [/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = Style.Parse("yellow"),
                Padding = new Padding(2, 1, 2, 1)
            };

            AnsiConsole.Write(panel);

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("¿Qué te gustaría hacer?")
                    .AddChoices(new[]
                    {
                        "⚡ Configurar una IA ahora (Gemini, OpenRouter, Ollama)",
                        "🛡️  Continuar con Motor Heurístico Offline (Sin configurar nada)",
                    }));

            if (action.StartsWith("⚡"))
            {
                await RunSetupWizardAsync(settings, router);
                return true;
            }
            else
            {
                settings.AiProvider = "Heuristic";
                await _configService.SaveSettingsAsync(settings);
                AnsiConsole.MarkupLine("[grey]Continuando en modo Heurístico Offline...[/]");
                await Task.Delay(800);
                return true;
            }
        }

        private async Task ConfigureGeminiAsync(AppSettings settings, SmartAiRouter router)
        {
            AnsiConsole.MarkupLine("\n[cyan]Obtén una clave gratis en: [link=https://aistudio.google.com/app/apikey]https://aistudio.google.com/app/apikey[/][/]");

            var key = AnsiConsole.Prompt(
                new TextPrompt<string>("Ingresa tu [bold]Gemini API Key:[/] ")
                    .PromptStyle("green")
                    .Secret()
                    .ValidationErrorMessage("[red]La clave no puede estar vacía[/]")
                    .Validate(k => !string.IsNullOrWhiteSpace(k)));

            settings.Gemini.ApiKey = key.Trim();
            settings.AiProvider = "Gemini";
            await _configService.SaveSettingsAsync(settings);

            AnsiConsole.MarkupLine("[bold green]✅ Clave de Gemini guardada en appsettings.json[/]");
            AnsiConsole.MarkupLine("[grey]Motor activo actualizado a Google Gemini.[/]");
            await Task.Delay(1500);
        }

        private async Task ConfigureOpenRouterAsync(AppSettings settings, SmartAiRouter router)
        {
            AnsiConsole.MarkupLine("\n[cyan]Obtén tu clave en: [link=https://openrouter.ai/keys]https://openrouter.ai/keys[/][/]");

            var key = AnsiConsole.Prompt(
                new TextPrompt<string>("Ingresa tu [bold]OpenRouter API Key:[/] ")
                    .PromptStyle("green")
                    .Secret()
                    .Validate(k => !string.IsNullOrWhiteSpace(k)));

            settings.OpenRouter.ApiKey = key.Trim();
            settings.AiProvider = "OpenRouter";
            await _configService.SaveSettingsAsync(settings);

            AnsiConsole.MarkupLine("[bold green]✅ Clave de OpenRouter guardada en appsettings.json[/]");
            await Task.Delay(1500);
        }

        private async Task ConfigureOllamaAsync(AppSettings settings, SmartAiRouter router)
        {
            var ollama = new OllamaAiEngine(settings.Ollama);

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Buscando servicio Ollama en localhost:11434...", async ctx =>
                {
                    await Task.Delay(500);
                });

            bool available = await ollama.IsAvailableAsync();

            if (available)
            {
                settings.AiProvider = "Ollama";
                await _configService.SaveSettingsAsync(settings);
                AnsiConsole.MarkupLine($"[bold green]✅ ¡Ollama detectado y funcionando en {settings.Ollama.Endpoint}![/]");
                AnsiConsole.MarkupLine($"[grey]Modelo configurado:[/] [cyan]{settings.Ollama.Model}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠️ No se pudo conectar a Ollama en http://localhost:11434[/]");
                AnsiConsole.MarkupLine("[grey]Para iniciarlo en tu Linux, ejecuta en otra terminal:[/]\n" +
                                       "[bold white]  ollama serve[/]  o  [bold white]ollama run llama3.2:3b[/]");

                var retry = AnsiConsole.Confirm("¿Deseas guardar Ollama de todos modos para cuando lo inicies?", defaultValue: true);
                if (retry)
                {
                    settings.AiProvider = "Ollama";
                    await _configService.SaveSettingsAsync(settings);
                    AnsiConsole.MarkupLine("[green]Ollama configurado como proveedor principal.[/]");
                }
            }

            await Task.Delay(1800);
        }

        private async Task ConfigureCustomApiAsync(AppSettings settings, SmartAiRouter router)
        {
            var endpoint = AnsiConsole.Prompt(
                new TextPrompt<string>("Endpoint base (ej: http://localhost:1234/v1): ")
                    .DefaultValue("http://localhost:1234/v1"));

            var model = AnsiConsole.Prompt(
                new TextPrompt<string>("Nombre del modelo: ")
                    .DefaultValue("local-model"));

            var apiKey = AnsiConsole.Prompt(
                new TextPrompt<string>("API Key (dejar vacío si es servidor local): ")
                    .AllowEmpty());

            settings.CustomApi.Endpoint = endpoint.Trim();
            settings.CustomApi.Model = model.Trim();
            settings.CustomApi.ApiKey = apiKey.Trim();
            settings.AiProvider = "Custom";

            await _configService.SaveSettingsAsync(settings);
            AnsiConsole.MarkupLine("[bold green]✅ Configuración de API Custom guardada con éxito.[/]");
            await Task.Delay(1500);
        }
    }
}
