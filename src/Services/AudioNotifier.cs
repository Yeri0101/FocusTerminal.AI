using System;
using FocusTerminal.AI.Core.Models;

namespace FocusTerminal.AI.Services
{
    public class AudioNotifier
    {
        private readonly AudioConfig _config;

        public AudioNotifier(AudioConfig? config = null)
        {
            _config = config ?? new AudioConfig();
        }

        public void PlayIntervalFinishedAlert()
        {
            if (!_config.EnableSoundAlerts) return;

            try
            {
                // Terminal bell estándar
                Console.Write("\a");
            }
            catch
            {
                // Silencioso
            }
        }

        public void PlayFocusWarningAlert()
        {
            if (!_config.EnableSoundAlerts) return;

            try
            {
                Console.Write("\a");
            }
            catch
            {
                // Silencioso
            }
        }
    }
}
