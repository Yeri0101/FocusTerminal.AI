using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Interfaces;
using TextCopy;

namespace FocusTerminal.AI.Services
{
    public class FocusMonitor : IFocusMonitor
    {
        private readonly IClipboardSanitizer _sanitizer;
        private readonly List<string> _clipboardHistory = new();
        private readonly object _lock = new();
        private string _lastClipboardRaw = string.Empty;

        public int CapturedItemsCount
        {
            get
            {
                lock (_lock) return _clipboardHistory.Count;
            }
        }

        public FocusMonitor(IClipboardSanitizer sanitizer)
        {
            _sanitizer = sanitizer;
        }

        public void StartMonitoring(CancellationToken token)
        {
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await CollectClipboardDataAsync();
                    try
                    {
                        await Task.Delay(2000, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, token);
        }

        private async Task CollectClipboardDataAsync()
        {
            try
            {
                var clipboard = new Clipboard();
                string? currentText = await clipboard.GetTextAsync();

                if (!string.IsNullOrWhiteSpace(currentText) && currentText != _lastClipboardRaw)
                {
                    _lastClipboardRaw = currentText;

                    // Sanitizar inmediatamente antes de almacenar en memoria
                    string sanitized = _sanitizer.Sanitize(currentText);

                    if (!string.IsNullOrWhiteSpace(sanitized))
                    {
                        lock (_lock)
                        {
                            _clipboardHistory.Add(sanitized);
                        }
                    }
                }
            }
            catch
            {
                // Silencioso ante fallos del clipboard del SO (ej. entorno sin GUI o restringido)
            }
        }

        public List<string> GetAndClearClipboardHistory()
        {
            lock (_lock)
            {
                var copy = new List<string>(_clipboardHistory);
                _clipboardHistory.Clear();
                return copy;
            }
        }
    }
}
