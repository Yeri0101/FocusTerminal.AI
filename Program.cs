using System;
using System.IO;
using System.Threading.Tasks;
using FocusTerminal.AI.AI;
using FocusTerminal.AI.Core.Models;
using FocusTerminal.AI.Security;
using FocusTerminal.AI.Services;
using FocusTerminal.AI.UI;
using Microsoft.Extensions.Configuration;

namespace FocusTerminal.AI
{
    public class Program
    {
        [STAThread]
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // 1. Cargar configuración con ConfigService
            var configService = new ConfigService();
            var settings = configService.LoadSettings();

            // 2. Componer servicios de infraestructura y seguridad
            var sanitizer = new ClipboardSanitizer(settings.Privacy);
            var aiRouter = new SmartAiRouter(settings);
            var focusMonitor = new FocusMonitor(sanitizer);
            var weatherService = new WeatherService(settings.Weather);
            var storageService = new StorageService();
            var audioNotifier = new AudioNotifier(settings.Audio);
            var dashboard = new TerminalDashboard();
            var aiWizard = new AiSetupWizard(configService);

            // 3. Orquestador de la sesión
            var controller = new SessionController(
                aiRouter,
                focusMonitor,
                weatherService,
                storageService,
                audioNotifier,
                dashboard,
                aiWizard,
                settings);

            await controller.RunAsync();
        }
    }
}
