using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FocusTerminal.AI
{
    public class WeatherService
    {
        private readonly HttpClient _httpClient = new HttpClient();
        public async Task<string> GetWeatherAsync(string city)
        {
            var url = "https://api.open-meteo.com/v1/forecast?latitude=46.52&longitude=6.63&current_weather=true";
            try
            {
                var response = await _httpClient.GetStringAsync(url);
                using (var jsonDoc = JsonDocument.Parse(response))
                {
                    var currentWeather = jsonDoc.RootElement.GetProperty("current_weather");
                    var temp = currentWeather.GetProperty("temperature").GetDouble();
                    var code = currentWeather.GetProperty("weathercode").GetInt32();
                    return $"{temp}°C — Código de clima: {code}";
                }
            }
            catch { return "No disponible"; }
        }
    }
}
