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
            var sessionManager = new SessionManager();
            await sessionManager.Start();
        }
    }
}
