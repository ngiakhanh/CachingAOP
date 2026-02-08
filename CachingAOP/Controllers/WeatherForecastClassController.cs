using Microsoft.AspNetCore.Mvc;

namespace CachingAOP.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastClassController : ControllerBase
{
    private readonly ILogger<WeatherForecastClassController> _logger;
    private readonly WeatherForecastService _weatherForecastService;

    public WeatherForecastClassController(ILogger<WeatherForecastClassController> logger, WeatherForecastService weatherForecastService)
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

    [HttpGet("CacheThrowAsyncGetWeatherForecast")]
    public async Task<IEnumerable<WeatherForecast>> ThrowAsync()
    {
        return await _weatherForecastService.ThrowAsync();
    }

    [HttpGet("CacheThrowGetWeatherForecast")]
    public IEnumerable<WeatherForecast> Throw()
    {
        return _weatherForecastService.Throw();
    }
}