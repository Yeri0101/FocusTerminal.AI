using System.Text.Json.Serialization;

namespace FocusTerminal.AI.Core.Models
{
    public class FocusResult
    {
        [JsonPropertyName("is_focused")]
        public bool IsFocused { get; set; } = true;

        [JsonPropertyName("message")]
        public string Message { get; set; } = "Manteniendo el enfoque.";

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; } = 1.0;

        [JsonIgnore]
        public string ProviderName { get; set; } = "Unknown";
    }
}
