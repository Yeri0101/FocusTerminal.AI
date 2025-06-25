using SharpHook;
using SharpHook.Data;
using SharpHook.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TextCopy;

namespace FocusTerminal.AI
{
    public class FocusMonitor
    {
        private readonly List<string> _clipboardHistory = new List<string>();
        private readonly HashSet<string> _processedClipboardTexts = new HashSet<string>();
        private readonly TaskPoolGlobalHook _hook;
        private readonly List<int> _kpmHistory = new List<int>();
        private int _keystrokeCount = 0;
        private Timer _kpmTimer;
        private bool _isPaused = false;
        private readonly StringBuilder _currentWord = new StringBuilder();
        private readonly List<string> _wordBuffer = new List<string>();
        private readonly List<string> _sampledWordsHistory = new List<string>();
        private const int WORD_BUFFER_SIZE = 30;
        private const int SAMPLE_SIZE = 6;

        public FocusMonitor()
        {
            _hook = new TaskPoolGlobalHook();
        }

        public void StartMonitoring(CancellationToken token)
        {
            _hook.KeyPressed += OnKeyPressed;
            _kpmTimer = new Timer(RecordKpm, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            Task.Run(() => _hook.RunAsync(), token);
            Task.Run(async () => {
                while (!token.IsCancellationRequested) { await CollectClipboardData(); await Task.Delay(2000, token); }
            }, token);
        }

        private void OnKeyPressed(object sender, KeyboardHookEventArgs e)
        {
            if (_isPaused) return;
            _keystrokeCount++;
            char keyChar = (char)e.Data.KeyChar;
            if (char.IsLetterOrDigit(keyChar))
            {
                _currentWord.Append(keyChar);
            }
            else if ((e.Data.KeyCode == KeyCode.VcSpace || e.Data.KeyCode == KeyCode.VcEnter) && _currentWord.Length > 0)
            {
                ProcessWord(_currentWord.ToString().ToLower());
                _currentWord.Clear();
            }
        }

        private void ProcessWord(string word)
        {
            _wordBuffer.Add(word);
            if (_wordBuffer.Count >= WORD_BUFFER_SIZE)
            {
                var random = new Random();
                var sample = _wordBuffer.OrderBy(x => random.Next()).Take(SAMPLE_SIZE).ToList();
                _sampledWordsHistory.AddRange(sample);
                _wordBuffer.Clear();
            }
        }

        private void RecordKpm(object state)
        {
            if (!_isPaused)
            {
                _kpmHistory.Add(_keystrokeCount);
                _keystrokeCount = 0;
                if (_kpmHistory.Count > 10) _kpmHistory.RemoveAt(0);
            }
        }

        public void StopMonitoring()
        {
            _hook.KeyPressed -= OnKeyPressed;
            _kpmTimer?.Dispose();
            _hook.Dispose();
        }

        public void PauseMonitoring() => _isPaused = true;
        public void ResumeMonitoring() => _isPaused = false;

        private async Task CollectClipboardData()
        {
            if (_isPaused) return;
            try
            {
                var clipboard = new Clipboard();
                string currentClipboardText = await clipboard.GetTextAsync();

                if (!string.IsNullOrEmpty(currentClipboardText) && !_processedClipboardTexts.Contains(currentClipboardText))
                {
                    string truncatedText = currentClipboardText.Substring(0, Math.Min(currentClipboardText.Length, 100));
                    _clipboardHistory.Add(truncatedText);
                    _processedClipboardTexts.Add(currentClipboardText);
                }
            }
            catch { /* Ignorar errores */ }
        }

        public List<string> GetAndClearClipboardHistory()
        {
            var history = new List<string>(_clipboardHistory);
            _clipboardHistory.Clear();
            return history;
        }

        public List<int> GetKpmHistory() => new List<int>(_kpmHistory);

        public List<string> GetAndClearSampledWordsHistory()
        {
            var history = new List<string>(_sampledWordsHistory);
            _sampledWordsHistory.Clear();
            return history;
        }

        public void ClearAllHistory()
        {
            _clipboardHistory.Clear();
            _processedClipboardTexts.Clear();
            _kpmHistory.Clear();
            _sampledWordsHistory.Clear();
            _wordBuffer.Clear();
            _currentWord.Clear();
            _keystrokeCount = 0;
        }
    }
}
