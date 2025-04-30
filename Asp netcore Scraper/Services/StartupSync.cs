using ScraperAPI.Services;

public class StartupSync : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StartupSync> _logger;

    public StartupSync(
        IServiceProvider serviceProvider,
        ILogger<StartupSync> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing startup sync");

        using (var scope = _serviceProvider.CreateScope())
        {
            try
            {
               var scraperService = scope.ServiceProvider.GetRequiredService<ScraperService>();
                await scraperService.SyncOffersAsync();  
                _logger.LogInformation("Startup sync completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during startup sync");
            }
        }

    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}