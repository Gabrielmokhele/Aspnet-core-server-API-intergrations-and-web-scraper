using ScraperAPI.Services;

public class OfferSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OfferSyncBackgroundService> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromHours(6); 

    public OfferSyncBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OfferSyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Offer Sync Background Service is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Starting scheduled offer sync");

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                     var scraperService = scope.ServiceProvider.GetRequiredService<ScraperService>();
                    await scraperService.SyncOffersAsync();
                }
                _logger.LogInformation("Completed scheduled offer sync");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during scheduled offer sync");
            }

            await Task.Delay(_syncInterval, stoppingToken);
        }
    }
}