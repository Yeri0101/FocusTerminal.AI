using System;
using System.IO;
using System.Media;
using System.Runtime.Versioning;

namespace FocusTerminal.AI
{
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
}
