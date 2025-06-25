using System.Text.Json.Serialization;

namespace FocusTerminal.AI
{
    public class FocusResult
    {
        [JsonPropertyName("is_focused")]
        public bool IsFocused { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
