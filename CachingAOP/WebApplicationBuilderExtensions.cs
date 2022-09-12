namespace CachingAOP
{
    public static class WebApplicationBuilderExtensions
    {
        public static WebApplication BuildWithCache(this WebApplicationBuilder webApplicationBuilder)
        {
            webApplicationBuilder.Services.AddCaching();
            return webApplicationBuilder.Build();
        }
    }
}
