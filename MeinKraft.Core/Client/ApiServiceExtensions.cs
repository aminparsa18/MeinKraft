using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http;

public static class ApiServiceExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        string serverBaseUrl,
        string? apiKey = null)
    {
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, attempt =>
                TimeSpan.FromSeconds(Math.Pow(2, attempt))); // 2s, 4s, 8s

        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(10);

        void ConfigureClient(HttpClient client)
        {
            client.BaseAddress = new Uri(serverBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            if (!string.IsNullOrEmpty(apiKey))
                client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        services.AddHttpClient<IPlayerApiService, PlayerApiService>(ConfigureClient)
          .AddStandardResilienceHandler(options =>
          {
              options.Retry.MaxRetryAttempts = 3;
              options.Retry.Delay = TimeSpan.FromSeconds(2);
              options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
              options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
          });

        services.AddHttpClient<ISessionApiService, SessionApiService>(ConfigureClient)
                .AddStandardResilienceHandler(options =>
                {
                    options.Retry.MaxRetryAttempts = 3;
                    options.Retry.Delay = TimeSpan.FromSeconds(2);
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                });

        services.AddHttpClient<IWorldApiService, WorldApiService>(ConfigureClient)
                .AddStandardResilienceHandler(options =>
                {
                    options.Retry.MaxRetryAttempts = 3;
                    options.Retry.Delay = TimeSpan.FromSeconds(2);
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                });

        return services;
    }
}