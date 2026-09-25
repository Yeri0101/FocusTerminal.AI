using System.Threading;
using System.Threading.Tasks;
using FocusTerminal.AI.Core.Models;

namespace FocusTerminal.AI.Core.Interfaces
{
    public interface IWeatherService
    {
        Task<WeatherInfo> GetWeatherAsync(CancellationToken ct = default);
        Task<WeatherInfo> GetWeatherForCityAsync(string city, double lat, double lon, CancellationToken ct = default);
    }
}
