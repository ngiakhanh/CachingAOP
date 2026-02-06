using Microsoft.AspNetCore.Mvc;

namespace CachingAOP.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private readonly ILogger<WeatherForecastController> _logger;
    private readonly WeatherForecastService _weatherForecastService;

    public WeatherForecastController(ILogger<WeatherForecastController> logger, WeatherForecastService weatherForecastService)
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