using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Interfaces;
using FocusTerminal.AI.Core.Models;

namespace FocusTerminal.AI.AI
{
    public class HeuristicAiEngine : IAiFocusEngine
    {
        public string ProviderName => "Heurístico Offline";

        private static readonly HashSet<string> DistractionKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "youtube", "tiktok", "instagram", "facebook", "twitter", "x.com",
            "netflix", "twitch", "reddit", "steam", "discord", "aliexpress",
            "amazon", "shein", "mercadolibre", "meme", "game", "juego", "series", "pelicula",
            "redes", "sociales", "whatsapp", "telegram", "chismes", "noticias", "musica",
            "spotify", "shorts", "reels", "anime"
        };

        private static readonly HashSet<string> FocusKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "github", "gitlab", "stackoverflow", "learn.microsoft", "docs",
            "c#", "dotnet", "python", "typescript", "javascript", "react", "docker", "api",
            "class", "function", "async", "await", "public", "private", "return", "import",
            "select", "from", "where", "test", "commit", "push", "pull", "merge", "debug",
            "universidad", "estudio", "carrera", "curso", "investigacion", "academic", "scholar"
        };

        private static readonly Dictionary<string, List<Playlist>> DefaultPlaylists = new(StringComparer.OrdinalIgnoreCase)
        {
            ["study"] = new()
            {
                new() { Name = "LoFi Girl (Focus Beats)", Url = "https://www.youtube.com/watch?v=jfKfPfyJRdk", Genre = "Lo-Fi" },
                new() { Name = "Deep Focus (Instrumental)", Url = "https://open.spotify.com/playlist/37i9dQZF1DWZeKCadgRdKQ", Genre = "Ambient" },
                new() { Name = "Piano Concentration", Url = "https://open.spotify.com/playlist/37i9dQZF1DX8Uebhn9wzrS", Genre = "Classical Piano" }
            },
            ["work"] = new()
            {
                new() { Name = "Electronic Focus", Url = "https://open.spotify.com/playlist/37i9dQZF1DX8NTLI2TtZa6", Genre = "Synth & Electronic" },
                new() { Name = "Ambient Music for Work", Url = "https://www.youtube.com/watch?v=1fueZCTYkpA", Genre = "Ambient" },
                new() { Name = "Coding Mode", Url = "https://open.spotify.com/playlist/4U4N3rOcvNQWlJdUBwWQdU", Genre = "Techno/Focus" }
            },
            ["creative"] = new()
            {
                new() { Name = "Creative Flow", Url = "https://open.spotify.com/playlist/37i9dQZF1DWU6RkLn4WNxq", Genre = "Neo-Classical" },
                new() { Name = "Dreamy Beats", Url = "https://www.youtube.com/watch?v=DWcJFNfaw9c", Genre = "Chillhop" },
                new() { Name = "Brain Food", Url = "https://open.spotify.com/playlist/37i9dQZF1DX8Uebhn9wzrS", Genre = "Acoustic Focus" }
            }
        };

        private static readonly string[] Quotes =
        {
            "El secreto para salir adelante es simplemente empezar.",
            "La disciplina es el puente entre las metas y los logros.",
            "La concentración no es decir sí a lo que debes hacer, sino no a las otras 100 cosas.",
            "Una hora de trabajo profundo vale por cuatro horas de trabajo distraído.",
            "Hazlo simple, hazlo memorable, hazlo agradable a la vista y divertido de leer."
        };

        private static readonly string[] TechFacts =
        {
            "El primer 'bug' informático fue literalmente una polilla atrapada en un relé del Mark II en 1947.",
            "El primer disco duro comercial de IBM (1956) pesaba más de una tonelada y almacenaba 5 MB.",
            "El término 'Spam' para correo no deseado proviene de un sketch de Monty Python.",
            "C# fue creado originalmente bajo el nombre clave 'Cool' (C-like Object Oriented Language).",
            "La primera cámara web del mundo se inventó en Cambridge para vigilar una cafetera y no bajar en vano."
        };

        public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task<FocusResult> AnalyzeFocusAsync(
            IReadOnlyList<string> clipboardHistory,
            IReadOnlyList<string> activeWindows,
            string taskDescription,
            CancellationToken ct = default)
        {
            int distractionHits = 0;
            int focusHits = 0;
            string? firstDistraction = null;

            // 1. Evaluar ventanas activas visitadas
            if (activeWindows != null)
            {
                foreach (var win in activeWindows)
                {
                    var lower = win.ToLowerInvariant();
                    foreach (var kw in DistractionKeywords)
                    {
                        if (lower.Contains(kw))
                        {
                            distractionHits += 2; // Las ventanas tienen mayor peso
                            firstDistraction ??= win;
                        }
                    }

                    foreach (var kw in FocusKeywords)
                    {
                        if (lower.Contains(kw))
                        {
                            focusHits++;
                        }
                    }
                }
            }

            // 2. Evaluar portapapeles
            if (clipboardHistory != null)
            {
                foreach (var snippet in clipboardHistory)
                {
                    var lower = snippet.ToLowerInvariant();
                    foreach (var kw in DistractionKeywords)
                    {
                        if (lower.Contains(kw))
                        {
                            distractionHits++;
                            firstDistraction ??= snippet;
                        }
                    }

                    foreach (var kw in FocusKeywords)
                    {
                        if (lower.Contains(kw))
                        {
                            focusHits++;
                        }
                    }
                }
            }

            bool isFocused = distractionHits <= focusHits;
            string message;

            if (isFocused)
            {
                message = focusHits > 0
                    ? "Patrones de trabajo y estudio activos detectados. ¡Excelente ritmo!"
                    : "Sesión en curso sin distracciones detectadas. Sigue enfocado.";
            }
            else
            {
                string context = !string.IsNullOrWhiteSpace(firstDistraction)
                    ? $" ('{Truncate(firstDistraction, 35)}')"
                    : "";
                message = $"⚠️ Posible distracción detectada{context}. Reenfócate en tu objetivo.";
            }

            return Task.FromResult(new FocusResult
            {
                IsFocused = isFocused,
                Message = message,
                Confidence = 0.85,
                ProviderName = ProviderName
            });
        }

        public Task<Playlist> GetPlaylistRecommendationAsync(string mode, string taskDescription, CancellationToken ct = default)
        {
            string key = mode?.Trim().ToLowerInvariant() ?? "work";
            if (!DefaultPlaylists.TryGetValue(key, out var list))
            {
                list = DefaultPlaylists["work"];
            }

            var selected = list[Random.Shared.Next(list.Count)];
            return Task.FromResult(selected);
        }

        public Task<string> GetMotivationalQuoteAsync(CancellationToken ct = default) =>
            Task.FromResult(Quotes[Random.Shared.Next(Quotes.Length)]);

        public Task<string> GetTechFactAsync(CancellationToken ct = default) =>
            Task.FromResult(TechFacts[Random.Shared.Next(TechFacts.Length)]);

        private static string Truncate(string text, int max)
        {
            if (text.Length <= max) return text;
            return text.Substring(0, max) + "...";
        }
    }
}
