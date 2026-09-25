using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Models;

namespace FocusTerminal.AI.Core.Interfaces
{
    public interface IAiFocusEngine
    {
        string ProviderName { get; }
        Task<bool> IsAvailableAsync(CancellationToken ct = default);
        Task<FocusResult> AnalyzeFocusAsync(IReadOnlyList<string> clipboardHistory, string taskDescription, CancellationToken ct = default);
        Task<Playlist> GetPlaylistRecommendationAsync(string mode, string taskDescription, CancellationToken ct = default);
        Task<string> GetMotivationalQuoteAsync(CancellationToken ct = default);
        Task<string> GetTechFactAsync(CancellationToken ct = default);
    }
}
