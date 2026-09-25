using System;
using System.IO;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Models;
using FocusTerminal.AI.Services;
using Xunit;

namespace FocusTerminal.AI.Tests
{
    public class StorageServiceTests : IDisposable
    {
        private readonly string _testDir;
        private readonly StorageService _storageService;

        public StorageServiceTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "FocusTerminalTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _storageService = new StorageService(_testDir);
        }

        [Fact]
        public async Task SaveAndLoadTask_ShouldPersistTaskCorrectly()
        {
            var task = new TaskDetails
            {
                Name = "Refactor Authentication",
                Description = "Implement JWT validation",
                IntervalDurationMinutes = 45,
                Mode = "work"
            };

            await _storageService.SaveTaskAsync(task);
            var loaded = await _storageService.LoadSavedTaskAsync();

            Assert.NotNull(loaded);
            Assert.Equal("Refactor Authentication", loaded.Name);
            Assert.Equal("Implement JWT validation", loaded.Description);
            Assert.Equal(45, loaded.IntervalDurationMinutes);
            Assert.Equal("work", loaded.Mode);
        }

        [Fact]
        public async Task SaveSessionHistory_ShouldCalculateFocusScore()
        {
            var session = new SessionHistory
            {
                TaskName = "Testing Phase",
                Mode = "study",
                PlannedMinutes = 30,
                CompletedMinutes = 30,
                TotalChecks = 4,
                FocusedChecks = 3,
                DistractionChecks = 1
            };

            await _storageService.SaveSessionHistoryAsync(session);
            var history = await _storageService.GetSessionHistoryAsync();

            Assert.NotEmpty(history);
            Assert.Equal(75.0, history[0].FocusScorePercentage);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, recursive: true);
                }
            }
            catch
            {
                // Silencioso
            }
        }
    }
}
