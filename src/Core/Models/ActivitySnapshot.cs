using System.Collections.Generic;

namespace FocusTerminal.AI.Core.Models
{
    public class ActivitySnapshot
    {
        public List<string> ClipboardHistory { get; set; } = new();
        public List<string> ActiveWindows { get; set; } = new();
        public string CurrentWindow { get; set; } = string.Empty;
        public bool HasData => ClipboardHistory.Count > 0 || ActiveWindows.Count > 0;
    }
}
