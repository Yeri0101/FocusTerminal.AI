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
            "youtube.com", "tiktok", "instagram", "facebook", "twitter.com", "x.com",
            "netflix", "twitch.tv", "reddit.com", "steam", "discord.com", "aliexpress",
            "amazon", "shein", "mercadolibre", "meme", "game", "juego", "series", "pelicula"
        };

        private static readonly HashSet<string> FocusKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "github.com", "gitlab.com", "stackoverflow.com", "learn.microsoft.com", "docs.",
            "c#", "dotnet", "python", "typescript", "javascript", "react", "docker", "api",
            "class", "function", "async", "await", "public", "private", "return", "import",
            "select", "from", "where", "test", "commit", "push", "pull", "merge", "debug"
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

        public Task<FocusResult> AnalyzeFocusAsync(IReadOnlyList<string> clipboardHistory, string taskDescription, CancellationToken ct = default)
        {
            if (clipboardHistory == null || clipboardHistory.Count == 0)
            {
                return Task.FromResult(new FocusResult
                {
                    IsFocused = true,
                    Message = "No se detectó actividad distractora. Buen ritmo.",
                    Confidence = 0.9,
                    ProviderName = ProviderName
                });
            }

            int distractionHits = 0;
            int focusHits = 0;

            foreach (var snippet in clipboardHistory)
            {
                var lower = snippet.ToLowerInvariant();
                if (DistractionKeywords.Any(k => lower.Contains(k)))
                {
                    distractionHits++;
                }
                if (FocusKeywords.Any(k => lower.Contains(k)))
                {
                    focusHits++;
                }
            }

            bool isFocused = distractionHits <= focusHits;
            string message = isFocused
                ? "Patrones de trabajo activos detectados. ¡Sigue así!"
                : "Se detectaron posibles fuentes de distracción en el portapapeles. Vuelve a tu objetivo principal.";

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
    }
}
