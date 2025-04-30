using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using ScraperAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;


namespace ScraperAPI.Services
{
    public class ScraperService
    {
        private readonly IBrowserService _browserService;
        private readonly IOfferRepository _offerRepository;
        private readonly HttpClient _httpClient;
        private readonly string? _externalApiUrl;
        private readonly string? _apiKey;
        private readonly ILogger<ScraperService> _logger;

        public ScraperService(
            IBrowserService browserService,
            IOfferRepository offerRepository,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<ScraperService> logger)
        {
            _browserService = browserService;
            _offerRepository = offerRepository;
            _httpClient = httpClient;
            _externalApiUrl = configuration["ExternalApi:Url"];
            _apiKey = configuration["ExternalApi:ApiKey"];
            _logger = logger;

            _logger.LogInformation($"ScraperService initialized with API URL: {_externalApiUrl}");
        }

        public async Task SyncOffersAsync()
        {
            try
            {
                _logger.LogInformation("Starting offer synchronization");
                await _offerRepository.BeginTransactionAsync();
                var offers = await FetchAllOffersFromExternalApi();
                _logger.LogInformation($"Fetched {offers.Count()} offers from external API");

                foreach (var offer in offers)
                {
                    var existingOffer = await _offerRepository.GetOfferByIdAsync(offer.OfferId);

                    if (existingOffer == null)
                    {
                        // If offer doesn't exist, add competitor price and save
                        if (!string.IsNullOrEmpty(offer.OfferUrl))
                        {
                            try
                            {
                                var priceResult = await FetchPriceFromUrl(offer.OfferUrl);
                                if (priceResult.IsSuccess)
                                {
                                    offer.CompetitorPrice = priceResult.Price;
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log error but continue processing
                                _logger.LogError(ex, $"Error fetching price for offer {offer.OfferId}");
                            }
                        }

                        offer.LastUpdated = DateTime.UtcNow;
                        await _offerRepository.AddOfferAsync(offer);
                    }
                    else
                    {
                        // If offer exists, update it and check if we need to refresh competitor price
                        if (!string.IsNullOrEmpty(offer.OfferUrl))
                        {
                            try
                            {
                                var priceResult = await FetchPriceFromUrl(offer.OfferUrl);
                                if (priceResult.IsSuccess)
                                {
                                    existingOffer.CompetitorPrice = priceResult.Price;
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log error but continue processing
                                _logger.LogError(ex, $"Error fetching price for offer {offer.OfferId}");
                            }
                        }

                        // Update other offer properties
                        existingOffer.TsinId = offer.TsinId;
                        existingOffer.ImageUrl = offer.ImageUrl;
                        existingOffer.Sku = offer.Sku;
                        existingOffer.Barcode = offer.Barcode;
                        existingOffer.Title = offer.Title;
                        existingOffer.SellingPrice = offer.SellingPrice;
                        existingOffer.Status = offer.Status;
                        existingOffer.OfferUrl = offer.OfferUrl;
                        existingOffer.LastUpdated = DateTime.UtcNow;

                        await _offerRepository.UpdateOfferAsync(existingOffer);
                    }
                    if (offers.TakeWhile(o => o != offer).Count() % 100 == 0)
                    {
                        await _offerRepository.SaveChangesAsync();
                    }
                }
                await _offerRepository.SaveChangesAsync();
                await _offerRepository.CommitTransactionAsync();
                _logger.LogInformation("Offer synchronization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during offer synchronization");
                await _offerRepository.RollbackTransactionAsync();
                throw;
            }
        }

        private async Task<IEnumerable<Offer>> FetchAllOffersFromExternalApi()
        {
            var allOffers = new List<Offer>();
            int pageNumber = 1;
            bool hasMorePages = true;

            try
            {

                while (hasMorePages)
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"https://seller-api.takealot.com/v2/offers?page_number={pageNumber}");
                    request.Headers.Add("Accept", "*/*");
                    request.Headers.Add("User-Agent", "Thunder Client (https://www.thunderclient.com)");
                    request.Headers.Add("Authorization", $"Key {_apiKey}");

                    var response = await _httpClient.SendAsync(request);
                    Console.WriteLine($"Status Code: {response.StatusCode}");

                    var rawContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("RAW JSON:\n" + rawContent);

                    response.EnsureSuccessStatusCode();

                    OfferResponse? responseContent;

                    try
                    {
                        responseContent = JsonSerializer.Deserialize<OfferResponse>(rawContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Deserialization FAILED: " + ex.Message);
                        _logger.LogError(ex, "Error deserializing OfferResponse from external API.");
                        await _offerRepository.RollbackTransactionAsync();
                        return new List<Offer>();
                    }

                    if (responseContent == null)
                    {
                        _logger.LogWarning("Deserialized OfferResponse is null.");
                        await _offerRepository.RollbackTransactionAsync();
                        return new List<Offer>();
                    }

                    if (responseContent.Offers != null && responseContent.Offers.Any())
                    {
                        allOffers.AddRange(responseContent.Offers);


                    }
                    else
                    {
                        hasMorePages = false;
                    }

                    pageNumber++;
                }

                return allOffers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FetchAllOffersFromExternalApi");
                await _offerRepository.RollbackTransactionAsync();
                throw;
            }
        }



        private async Task<(bool IsSuccess, decimal? Price)> FetchPriceFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return (false, null);
            }

            var content = await _browserService.GetPageContentAsync(url);
           

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(content);

            var priceNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@data-ref='price']//span[contains(@class, 'currency') and contains(@class, 'currency-module_currency')]");

            if (priceNode == null)
            {
                return (false, null);
            }

            string priceText = priceNode.InnerText.Trim().Replace("R", "").Replace("€", "").Replace(",", "");
            _logger.LogInformation("Extracted price text: {PriceText}", priceText);
            if (decimal.TryParse(priceText, out decimal price))
            {
                return (true, price);
            }

            return (false, null);
        }
    }

    public class OfferResponse
    {
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public int TotalResults { get; set; }
        public List<Offer> Offers { get; set; } = new List<Offer>();
    }
}