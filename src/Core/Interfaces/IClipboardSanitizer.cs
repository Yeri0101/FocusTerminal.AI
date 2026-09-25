using System.Collections.Generic;

namespace FocusTerminal.AI.Core.Interfaces
{
    public interface IClipboardSanitizer
    {
        string Sanitize(string rawText, int maxLength = 150);
        IReadOnlyList<string> SanitizeBatch(IEnumerable<string> snippets, int maxLength = 150);
        bool ContainsSensitiveData(string text);
    }
}
