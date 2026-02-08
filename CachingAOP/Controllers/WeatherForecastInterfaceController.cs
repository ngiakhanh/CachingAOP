using Microsoft.AspNetCore.Mvc;

namespace CachingAOP.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastInterfaceController : ControllerBase
{
    private readonly ILogger<WeatherForecastInterfaceController> _logger;
    private readonly IWeatherForecastService _weatherForecastService;

    public WeatherForecastInterfaceController(ILogger<WeatherForecastInterfaceController> logger, IWeatherForecastService weatherForecastService)
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