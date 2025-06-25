using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Linq;

namespace FocusTerminal.AI
{
    /// <summary>
    /// Gère la communication avec l'API Google Gemini.
    /// </summary>
    public class GeminiService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, List<Playlist>> _predefinedPlaylists;

        /// <summary>
        /// Initialise le service.
        /// </summary>
        public GeminiService(string key)
        {
            _apiKey = key; // Clé API.
            _httpClient = new HttpClient(); // Client HTTP.

            // Playlists pour différents modes.
            _predefinedPlaylists = new Dictionary<string, List<Playlist>>
            {
                ["study"] = new List<Playlist> { new Playlist { Name = "Musique LoFi pour étudier", Url = "https://youtube.com/results?search_query=lofi+hip+hop" }, new Playlist { Name = "Concentration Profonde", Url = "https://open.spotify.com/playlist/37i9dQZF1DWZeKCadgRdKQ" } },
                ["work"] = new List<Playlist> { new Playlist { Name = "Focus Électronique", Url = "https://open.spotify.com/playlist/37i9dQZF1DX5trt9i14X7j" }, new Playlist { Name = "Mode Programmation", Url = "https://open.spotify.com/playlist/37i9dQZF1E8Vz6GAa6bV1G" } },
                ["creative"] = new List<Playlist> { new Playlist { Name = "Flux Créatif", Url = "https://open.spotify.com/playlist/37i9dQZF1DXa2Y2alIbrb6" }, new Playlist { Name = "Musique pour le Cerveau", Url = "https://open.spotify.com/playlist/37i9dQZF1DWXLeA8AabcxS" } }
            };
        }

        /// <summary>
        /// Analyse l'activité de l'utilisateur pour vérifier sa concentration.
        /// </summary>
        public async Task<FocusResult> AnalyzeFocusAsync(List<string> clipboardHistory, List<int> kpmHistory, List<string> sampledWords, string taskDescription)
        {
            // 1. Vérifier la clé API.
            if (string.IsNullOrEmpty(_apiKey) || _apiKey.Contains("API_KEY_Gemeni"))
            {
                return new FocusResult { IsFocused = true, Message = "Clé API non configurée." };
            }

            // 2. Préparer les données pour le prompt.
            string clipboardText = clipboardHistory.Any() ? $"Historique du presse-papiers : [{string.Join(", ", clipboardHistory.Select(item => $"'{item}'"))}]." : "L'utilisateur n'a rien copié récemment.";
            string kpmText = kpmHistory.Any() ? $"Rythme de frappe récent (touches par minute) : [{string.Join(", ", kpmHistory)}]." : "Pas de données sur le rythme de frappe.";
            string sampledWordsText = sampledWords.Any() ? $"Un échantillon des mots qu'il a écrits est : [{string.Join(", ", sampledWords)}]." : "Pas d'échantillon de mots écrits.";

            // 3. Construire le prompt pour l'IA.
            string prompt = $"Vous êtes un assistant de concentration. Votre objectif principal est d'analyser si le contenu de ce qu'un utilisateur écrit et copie est lié à sa tâche. La vitesse de frappe est secondaire.\n" +
                            $"Tâche de l'utilisateur: '{taskDescription}'.\n" +
                            $"Données d'activité:\n" +
                            $"- Échantillon de mots écrits: {sampledWordsText}\n" +
                            $"- Historique du presse-papiers: {clipboardText}\n" +
                            $"- Rythme de frappe (KPM): {kpmText}\n" +
                            $"RÈGLES D'ANALYSE:\n" +
                            $"1. **PRIORITÉ MAXIMALE : LE CONTENU.** Si les 'mots écrits' ou le 'presse-papiers' contiennent des sujets clairement non liés à la tâche (ex: réseaux sociaux, actualités, divertissement), l'utilisateur est **DISTRAIT**, peu importe son rythme de frappe.\n" +
                            $"2. **INACTIVITÉ POSITIVE:** Si le rythme de frappe est ZÉRO, mais que le presse-papiers et les mots sont non pertinents ou vides, supposez que l'utilisateur lit ou réfléchit. Considérez cela comme **CONCENTRÉ** et donnez-lui un message sur l'importance de la réflexion.\n" +
                            $"3. **CONCENTRATION CLAIRE:** Si le rythme de frappe est élevé et que les mots écrits sont liés à la tâche, l'utilisateur est **CONCENTRÉ**.\n" +
                            $"4. **LANGUE :** Votre 'message' doit toujours être en français.\n" + // <-- NOUVELLE DIRECTIVE
                            $"Répondez uniquement avec un objet JSON au format : {{\"is_focused\": boolean, \"message\": \"un message très court, amical et spécifique pour l'utilisateur\"}}.";

            // 4. Appeler l'API Gemini.
            var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key={_apiKey}";
            var payload = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };

            try
            {
                // Envoyer la requête POST.
                var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(apiUrl, content);

                // Si la réponse est OK...
                if (response.IsSuccessStatusCode)
                {
                    // Lire le JSON de la réponse.
                    var jsonResponse = await response.Content.ReadAsStringAsync();

                    // Extraire le texte de la réponse.
                    using var doc = JsonDocument.Parse(jsonResponse);
                    var textResponse = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();

                    // Nettoyer le JSON (enlever les ```).
                    var cleanJson = textResponse.Replace("```json", "").Replace("```", "").Trim();

                    // Transformer le JSON en objet FocusResult.
                    return JsonSerializer.Deserialize<FocusResult>(cleanJson);
                }
            }
            catch
            {
                // Gérer les erreurs (ex: connexion), sans planter.
            }

            // 5. Réponse de secours si l'appel échoue.
            return new FocusResult { IsFocused = true, Message = "L'analyse de la concentration a échoué." };
        }

        /// <summary>
        /// Suggère une playlist musicale.
        /// </summary>
        public async Task<Playlist> GetPlaylistRecommendation(string mode, string taskDescription)
        {
            // Si le mode existe, retourner une playlist prédéfinie.
            if (_predefinedPlaylists.TryGetValue(mode.ToLower().Trim(), out var playlists))
            {
                return playlists[new Random().Next(playlists.Count)];
            }

            // Sinon, demander une suggestion à l'IA.
            string prompt = $"Pour une tâche de '{taskDescription}', recommandez un genre de musique pour la concentration (ex: Lofi Beats). Uniquement le nom du genre.";
            string generatedGenre = await GenerateTextWithGemini(prompt);
            return new Playlist { Name = $"Suggestion IA: {generatedGenre.Trim()}", Url = "Recherchez ce genre sur votre service de musique." };
        }

        /// <summary>
        /// Helper générique pour générer du texte avec l'IA.
        /// </summary>
        private async Task<string> GenerateTextWithGemini(string prompt)
        {
            if (string.IsNullOrEmpty(_apiKey) || _apiKey.Contains("API_KEY_Gemeni"))
            {
                return "Musique Ambiante (Clé API non configurée)";
            }

            var apiUrl = $"[https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key=](https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key=){_apiKey}";
            var payload = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(apiUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                }
            }
            catch { /* Ignorer les erreurs. */ }

            return "Rock Instrumental (Erreur API)";
        }

        /// <summary>
        /// Retourne une citation de motivation en français.
        /// </summary>
        public Task<string> GetMotivationalQuote() => Task.FromResult(new[]
        {
            "Le secret pour avancer, c'est de commencer.",
            "La discipline est le pont entre les objectifs et la réalisation."
        }[new Random().Next(2)]);

        /// <summary>
        /// Retourne un fait technique en français.
        /// </summary>
        public Task<string> GetTechFact() => Task.FromResult(new[]
        {
            "Le premier 'bug' informatique était littéralement une mite.",
            "Le premier disque dur de 1 Go pesait plus de 227 kg."
        }[new Random().Next(2)]);
    }
}