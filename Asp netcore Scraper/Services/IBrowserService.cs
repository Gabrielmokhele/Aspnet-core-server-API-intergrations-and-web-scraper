namespace ScraperAPI.Services
{
    public interface IBrowserService
    {
        Task<string> GetPageContentAsync(string url);
    }
}
