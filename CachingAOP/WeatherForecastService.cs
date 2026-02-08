namespace CachingAOP;

public interface IWeatherForecastService
{
    public List<WeatherForecast> Get();

    public Task<List<WeatherForecast>> GetAsync();

    [Cache(Seconds = 30)]
    public List<WeatherForecast> Throw();

    [Cache(Seconds = 30)]
    public Task<List<WeatherForecast>> ThrowAsync();
}
public class WeatherForecastService : IWeatherForecastService
{
    private readonly string[] Summaries = [ "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" ];

    public virtual List<WeatherForecast> Get()
    {
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToList();
    }

    [Cache(Seconds = 30)]
    public async virtual Task<List<WeatherForecast>> GetAsync()
    {
        return await Task.FromResult(Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToList());
    }

    public virtual List<WeatherForecast> Throw()
    {
        throw new NotImplementedException();
    }

    public virtual Task<List<WeatherForecast>> ThrowAsync()
    {
        throw new NotImplementedException();
    }
}