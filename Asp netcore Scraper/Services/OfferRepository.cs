using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScraperAPI.Data;
using ScraperAPI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScraperAPI.Services
{
    public class OfferRepository : IOfferRepository
    {
        private readonly ApplicationDbContext _context;
        private IDbContextTransaction _transaction = null!;

        public OfferRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Offer>> GetAllOffersAsync()
        {
            return await _context.Offers.ToListAsync();
        }

        public async Task<Offer?> GetOfferByIdAsync(int? offerId)
        {
            return await _context.Offers.FirstOrDefaultAsync(o => o.OfferId == offerId);
        }


        public async Task AddOfferAsync(Offer offer)
        {
            await _context.Offers.AddAsync(offer);
        }

        public Task UpdateOfferAsync(Offer offer)
        {
            _context.Offers.Update(offer);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            Console.WriteLine(">>> Saving to DB <<<");
            await _context.SaveChangesAsync();
        }

        public async Task BeginTransactionAsync()
        {
            if (_transaction == null)
            {
                _transaction = await _context.Database.BeginTransactionAsync();
            }
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                await _transaction.DisposeAsync();
                

            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();

            }
        }



    }
}