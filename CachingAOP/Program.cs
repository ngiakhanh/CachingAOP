using CachingAOP;

var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddProxiedScoped<IWeatherForecastService, WeatherForecastService>();
builder.Services.AddScoped<IWeatherForecastService, WeatherForecastService>();
//builder.Services.AddProxiedScoped<WeatherForecastService>();
builder.Services.AddScoped<WeatherForecastService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.BuildWithCache();
//var app = builder.BuildWithProxyCache();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
