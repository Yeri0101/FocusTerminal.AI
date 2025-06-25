using System;
using System.Collections.Generic;
using System.Text;

namespace FocusTerminal.AI.Helpers
{
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
            var bar = new StringBuilder("[");
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
