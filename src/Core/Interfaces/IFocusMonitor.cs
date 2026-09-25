using System.Collections.Generic;
using System.Threading;
using FocusTerminal.AI.Core.Models;

namespace FocusTerminal.AI.Core.Interfaces
{
    public interface IFocusMonitor
    {
        void StartMonitoring(CancellationToken token);
        ActivitySnapshot GetAndClearActivity();
        List<string> GetAndClearClipboardHistory();
        string GetCurrentActiveWindow();
        int CapturedItemsCount { get; }
    }
}
