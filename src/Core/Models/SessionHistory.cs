using System;

namespace FocusTerminal.AI.Core.Models
{
    public class SessionHistory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string TaskName { get; set; } = string.Empty;
        public string Mode { get; set; } = "work";
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime EndTime { get; set; } = DateTime.UtcNow;
        public double PlannedMinutes { get; set; }
        public double CompletedMinutes { get; set; }
        public int TotalChecks { get; set; }
        public int FocusedChecks { get; set; }
        public int DistractionChecks { get; set; }
        public double FocusScorePercentage => TotalChecks > 0 ? Math.Round((double)FocusedChecks / TotalChecks * 100, 1) : 100.0;
        public string Notes { get; set; } = string.Empty;
    }
}
