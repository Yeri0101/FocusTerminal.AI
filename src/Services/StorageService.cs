using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Interfaces;
using FocusTerminal.AI.Core.Models;

namespace FocusTerminal.AI.Services
{
    public class StorageService : IStorageService
    {
        private readonly string _storageDir;
        private readonly string _taskFilePath;
        private readonly string _historyFilePath;
        private const string LegacyTaskFile = "task_config.txt";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public StorageService(string? customDir = null)
        {
            if (!string.IsNullOrWhiteSpace(customDir))
            {
                _storageDir = customDir;
            }
            else
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                _storageDir = Path.Combine(home, ".focusterminal");
            }

            try
            {
                if (!Directory.Exists(_storageDir))
                {
                    Directory.CreateDirectory(_storageDir);
                }
            }
            catch
            {
                _storageDir = AppContext.BaseDirectory;
            }

            _taskFilePath = Path.Combine(_storageDir, "current_task.json");
            _historyFilePath = Path.Combine(_storageDir, "history.json");
        }

        public bool HasSavedTask()
        {
            return File.Exists(_taskFilePath) || File.Exists(LegacyTaskFile);
        }

        public async Task<TaskDetails?> LoadSavedTaskAsync()
        {
            try
            {
                if (File.Exists(_taskFilePath))
                {
                    var json = await File.ReadAllTextAsync(_taskFilePath);
                    return JsonSerializer.Deserialize<TaskDetails>(json, JsonOptions);
                }

                // Compatibilidad con archivo legado task_config.txt
                if (File.Exists(LegacyTaskFile))
                {
                    var lines = await File.ReadAllLinesAsync(LegacyTaskFile);
                    if (lines.Length > 0)
                    {
                        return new TaskDetails
                        {
                            Name = lines[0],
                            Description = lines.Length > 1 ? lines[1] : "",
                            IntervalDurationMinutes = lines.Length > 2 && int.TryParse(lines[2], out int m) ? m : 30,
                            Mode = lines.Length > 3 ? lines[3] : "work"
                        };
                    }
                }
            }
            catch
            {
                // Silencioso
            }

            return null;
        }

        public async Task SaveTaskAsync(TaskDetails task)
        {
            try
            {
                var json = JsonSerializer.Serialize(task, JsonOptions);
                await File.WriteAllTextAsync(_taskFilePath, json);

                // También actualizamos el archivo legado para mantener sincronía
                string[] legacyLines = { task.Name, task.Description, task.IntervalDurationMinutes.ToString(), task.Mode };
                await File.WriteAllLinesAsync(LegacyTaskFile, legacyLines);
            }
            catch
            {
                // Silencioso
            }
        }

        public Task DeleteSavedTaskAsync()
        {
            try
            {
                if (File.Exists(_taskFilePath)) File.Delete(_taskFilePath);
                if (File.Exists(LegacyTaskFile)) File.Delete(LegacyTaskFile);
            }
            catch
            {
                // Silencioso
            }

            return Task.CompletedTask;
        }

        public async Task SaveSessionHistoryAsync(SessionHistory session)
        {
            try
            {
                var history = (await GetSessionHistoryAsync(500)).ToList();
                history.Insert(0, session);

                // Conservar máximo 200 sesiones
                if (history.Count > 200) history = history.Take(200).ToList();

                var json = JsonSerializer.Serialize(history, JsonOptions);
                await File.WriteAllTextAsync(_historyFilePath, json);
            }
            catch
            {
                // Silencioso
            }
        }

        public async Task<IReadOnlyList<SessionHistory>> GetSessionHistoryAsync(int limit = 50)
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var json = await File.ReadAllTextAsync(_historyFilePath);
                    var list = JsonSerializer.Deserialize<List<SessionHistory>>(json, JsonOptions);
                    if (list != null)
                    {
                        return list.Take(limit).ToList();
                    }
                }
            }
            catch
            {
                // Silencioso
            }

            return Array.Empty<SessionHistory>();
        }

        public async Task<double> GetTotalFocusHoursAsync()
        {
            var history = await GetSessionHistoryAsync(500);
            double totalMinutes = history.Sum(h => h.CompletedMinutes);
            return Math.Round(totalMinutes / 60.0, 1);
        }
    }
}
