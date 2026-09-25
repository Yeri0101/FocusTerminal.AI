using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Interfaces;
using FocusTerminal.AI.Core.Models;

namespace FocusTerminal.AI.Services
{
    public class WeatherService : IWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly WeatherConfig _config;

        public WeatherService(WeatherConfig? config = null, HttpClient? httpClient = null)
        {
            _config = config ?? new WeatherConfig();
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        }

        public async Task<WeatherInfo> GetWeatherAsync(CancellationToken ct = default)
        {
            return await GetWeatherForCityAsync(_config.City, _config.Latitude, _config.Longitude, ct);
        }

        public async Task<WeatherInfo> GetWeatherForCityAsync(string city, double lat, double lon, CancellationToken ct = default)
        {
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current_weather=true";
            try
            {
                var response = await _httpClient.GetStringAsync(url, ct);
                using var jsonDoc = JsonDocument.Parse(response);
                var currentWeather = jsonDoc.RootElement.GetProperty("current_weather");
                var temp = currentWeather.GetProperty("temperature").GetDouble();
                var code = currentWeather.GetProperty("weathercode").GetInt32();

                return new WeatherInfo
                {
                    City = city,
                    Temperature = temp,
                    WeatherCode = code,
                    ConditionDescription = InterpretWeatherCode(code),
                    IsAvailable = true
                };
            }
            catch
            {
                return new WeatherInfo
                {
                    City = city,
                    IsAvailable = false
                };
            }
        }

        private static string InterpretWeatherCode(int code)
        {
            return code switch
            {
                0 => "☀️ Despejado",
                1 => "🌤️ Mayormente despejado",
                2 => "⛅ Parcialmente nublado",
                3 => "☁️ Nublado",
                45 or 48 => "🌫️ Niebla",
                51 or 53 or 55 => "🌦️ Llovizna ligera",
                61 or 63 or 65 => "🌧️ Lluvia",
                71 or 73 or 75 => "❄️ Nieve",
                80 or 81 or 82 => "🌧️ Chubascos",
                95 or 96 or 99 => "⛈️ Tormenta eléctrica",
                _ => "🌡️ Clima estándar"
            };
        }
    }
}
