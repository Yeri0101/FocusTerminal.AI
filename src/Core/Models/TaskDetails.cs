using System;

namespace FocusTerminal.AI.Core.Models
{
    public class TaskDetails
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int IntervalDurationMinutes { get; set; } = 30;
        public string Mode { get; set; } = "work";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
