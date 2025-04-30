
using PuppeteerSharp;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Polly;
using System.Net.Http;

namespace ScraperAPI.Services
{
    public class PuppeteerBrowserService : IBrowserService, IDisposable
    {
        private IBrowser? _browser;
        private bool _initialized = false;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly ILogger<PuppeteerBrowserService> _logger;
        private readonly TimeSpan _pageRequestTimeout = TimeSpan.FromSeconds(30);
        private readonly int _maxRetries = 3;

        public PuppeteerBrowserService(ILogger<PuppeteerBrowserService> logger)
        {
            _logger = logger;
        }

        private async Task InitializeBrowserAsync()
        {
            if (!_initialized)
            {
                await _semaphore.WaitAsync();
                try
                {
                    if (!_initialized)
                    {
                        _logger.LogInformation("Initializing Puppeteer browser");
                        var browserFetcherOptions = new BrowserFetcherOptions();
                        var browserFetcher = new BrowserFetcher(browserFetcherOptions);
                        await browserFetcher.DownloadAsync();

                        _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                        {
                            // Use true for production, false for debugging
                            Headless = true,
                            Args = new[] {
                                "--no-sandbox",
                                "--disable-setuid-sandbox",
                                "--disable-dev-shm-usage", // Helps with memory issues in containerized environments
                                "--disable-gpu"
                            }
                        });

                        _initialized = true;
                        _logger.LogInformation("Browser initialized successfully");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize browser");
                    throw;
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        public async Task<string> GetPageContentAsync(string url)
        {
            await InitializeBrowserAsync();

            if (_browser == null)
                throw new InvalidOperationException("Browser was not initialized.");

            // Create retry policy
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    _maxRetries,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception,
                            "Error getting content from {Url}. Retry {RetryCount} after {RetryTimeSpan}s",
                            url, retryCount, timeSpan.TotalSeconds);
                    }
                );

            return await retryPolicy.ExecuteAsync(async () =>
            {
                using var page = await _browser.NewPageAsync();

                // Set up cancellation for the request
                using var cts = new CancellationTokenSource(_pageRequestTimeout);

                try
                {
                    await page.SetViewportAsync(new ViewPortOptions
                    {
                        Width = 1366,
                        Height = 768
                    });

                    await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");

                    await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
                    {
                        ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8",
                        ["Accept-Language"] = "en-US,en;q=0.9",
                    });

                    // Register abort handler for the cancellation token
                    cts.Token.Register(() =>
                    {
                        _logger.LogWarning("Request to {Url} timed out after {Timeout}s", url, _pageRequestTimeout.TotalSeconds);
                    });

                    await page.GoToAsync(url, new NavigationOptions
                    {
                        WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded, WaitUntilNavigation.Load },
                        Timeout = (int)_pageRequestTimeout.TotalMilliseconds
                    });

                    await page.WaitForNetworkIdleAsync(new WaitForNetworkIdleOptions
                    {
                        Timeout = (int)_pageRequestTimeout.TotalMilliseconds,
                        IdleTime = 500  
                    });


                    // Wait a bit for any JavaScript to execute
                    await Task.Delay(5000, cts.Token);

                    _logger.LogInformation("Successfully loaded content from {Url}", url);
                    return await page.GetContentAsync();
                }
                catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
                {
                    _logger.LogError(ex, "Request timed out for {Url}", url);
                    throw new HttpRequestException($"Request timed out for {url}", ex);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching content from {Url}", url);
                    throw;
                }
                finally
                {
                    await page.CloseAsync();
                }
            });
        }

        public void Dispose()
        {
            try
            {
                _browser?.CloseAsync().GetAwaiter().GetResult();
                _browser?.Dispose();
                _logger.LogInformation("Browser disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing browser");
            }
        }
    }
}
