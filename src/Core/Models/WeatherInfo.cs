namespace FocusTerminal.AI.Core.Models
{
    public class WeatherInfo
    {
        public string City { get; set; } = "Lausanne";
        public double Temperature { get; set; }
        public int WeatherCode { get; set; }
        public string ConditionDescription { get; set; } = "Despejado";
        public bool IsAvailable { get; set; } = false;

        public override string ToString()
        {
            if (!IsAvailable) return "Clima no disponible";
            return $"{Temperature:F1}°C — {ConditionDescription} ({City})";
        }
    }
}
