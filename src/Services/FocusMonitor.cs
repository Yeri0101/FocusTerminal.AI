using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Interfaces;
using FocusTerminal.AI.Core.Models;
using TextCopy;

namespace FocusTerminal.AI.Services
{
    public class FocusMonitor : IFocusMonitor
    {
        private readonly IClipboardSanitizer _sanitizer;
        private readonly List<string> _clipboardHistory = new();
        private readonly HashSet<string> _activeWindowsHistory = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        private string _lastClipboardRaw = string.Empty;
        private string _lastActiveWindow = string.Empty;

        public int CapturedItemsCount
        {
            get
            {
                lock (_lock) return _clipboardHistory.Count + _activeWindowsHistory.Count;
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
                    CollectActiveWindowData();

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
                // Silencioso ante fallos del clipboard del SO (ej. falta xclip en Linux)
            }
        }

        private void CollectActiveWindowData()
        {
            try
            {
                string windowTitle = GetCurrentActiveWindow();
                if (!string.IsNullOrWhiteSpace(windowTitle) && windowTitle != _lastActiveWindow)
                {
                    _lastActiveWindow = windowTitle;

                    // Ignorar la propia ventana de la terminal si no aporta información
                    if (!windowTitle.StartsWith("Terminal - FocusTerminal", StringComparison.OrdinalIgnoreCase))
                    {
                        lock (_lock)
                        {
                            _activeWindowsHistory.Add(windowTitle);
                        }
                    }
                }
            }
            catch
            {
                // Silencioso
            }
        }

        public string GetCurrentActiveWindow()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxActiveWindow();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetWindowsActiveWindow();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetMacActiveWindow();
            }

            return string.Empty;
        }

        private static string GetLinuxActiveWindow()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "xdotool",
                    Arguments = "getactivewindow getwindowname",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(300);
                    if (!string.IsNullOrWhiteSpace(output))
                        return output;
                }
            }
            catch
            {
                // Fallback a wmctrl
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wmctrl",
                    Arguments = "-l",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(300);
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var active = lines.LastOrDefault();
                    if (!string.IsNullOrWhiteSpace(active))
                    {
                        var parts = active.Split(new[] { ' ' }, 4, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4) return parts[3].Trim();
                    }
                }
            }
            catch
            {
                // Silencioso
            }

            return string.Empty;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private static string GetWindowsActiveWindow()
        {
            try
            {
                var handle = GetForegroundWindow();
                if (handle != IntPtr.Zero)
                {
                    var sb = new StringBuilder(256);
                    if (GetWindowText(handle, sb, sb.Capacity) > 0)
                    {
                        return sb.ToString().Trim();
                    }
                }
            }
            catch
            {
                // Silencioso
            }

            return string.Empty;
        }

        private static string GetMacActiveWindow()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments = "-e 'tell application \"System Events\" to get name of first application process whose frontmost is true'",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(300);
                    return output;
                }
            }
            catch
            {
                // Silencioso
            }

            return string.Empty;
        }

        public ActivitySnapshot GetAndClearActivity()
        {
            lock (_lock)
            {
                var snapshot = new ActivitySnapshot
                {
                    ClipboardHistory = new List<string>(_clipboardHistory),
                    ActiveWindows = _activeWindowsHistory.ToList(),
                    CurrentWindow = _lastActiveWindow
                };

                _clipboardHistory.Clear();
                _activeWindowsHistory.Clear();
                return snapshot;
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
