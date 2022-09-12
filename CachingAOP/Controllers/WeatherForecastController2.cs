using Microsoft.AspNetCore.Mvc;

namespace CachingAOP.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController2 : ControllerBase
    {
        private readonly ILogger<WeatherForecastController2> _logger;
        private readonly IWeatherForecastService _weatherForecastService;

        public WeatherForecastController2(ILogger<WeatherForecastController2> logger, IWeatherForecastService weatherForecastService)
        {
            _logger = logger;
            _weatherForecastService = weatherForecastService;
        }

        [HttpGet("CachedGetWeatherForecastAsync")]
        public async Task<IEnumerable<WeatherForecast>> GetAsync()
        {
            return await _weatherForecastService.GetAsync();
        }

        [HttpGet("NoCacheGetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            return _weatherForecastService.Get();
        }
    }
}