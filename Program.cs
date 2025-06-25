using System;
using System.Threading.Tasks;

namespace FocusTerminal.AI
{
    /// <summary>
    /// Punto de entrada principal de la aplicación.
    /// </summary>
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
}
