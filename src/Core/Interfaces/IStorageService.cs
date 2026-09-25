using System.Collections.Generic;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Models;

namespace FocusTerminal.AI.Core.Interfaces
{
    public interface IStorageService
    {
        Task<TaskDetails?> LoadSavedTaskAsync();
        Task SaveTaskAsync(TaskDetails task);
        Task DeleteSavedTaskAsync();
        bool HasSavedTask();

        Task SaveSessionHistoryAsync(SessionHistory session);
        Task<IReadOnlyList<SessionHistory>> GetSessionHistoryAsync(int limit = 50);
        Task<double> GetTotalFocusHoursAsync();
    }
}
