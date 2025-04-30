using ScraperAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScraperAPI.Services
{
    public interface IOfferRepository
    {
        Task<IEnumerable<Offer>> GetAllOffersAsync();
        Task<Offer?> GetOfferByIdAsync(int? offerId);
        Task AddOfferAsync(Offer offer);
        Task UpdateOfferAsync(Offer offer);
        Task SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
