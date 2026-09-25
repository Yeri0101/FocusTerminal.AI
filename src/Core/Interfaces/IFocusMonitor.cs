using System.Collections.Generic;
using System.Threading;

namespace FocusTerminal.AI.Core.Interfaces
{
    public interface IFocusMonitor
    {
        void StartMonitoring(CancellationToken token);
        List<string> GetAndClearClipboardHistory();
        int CapturedItemsCount { get; }
    }
}
