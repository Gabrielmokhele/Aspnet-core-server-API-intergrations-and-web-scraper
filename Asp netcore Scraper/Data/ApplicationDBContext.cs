using Microsoft.EntityFrameworkCore;
using ScraperAPI.Models;

namespace ScraperAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Offer> Offers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Offer>()
                .HasIndex(o => o.OfferId)
                .IsUnique();

            modelBuilder.Entity<Offer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TsinId);
                entity.Property(e => e.OfferId).IsRequired();
                entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
                entity.Property(e => e.SellingPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CompetitorPrice).HasColumnType("decimal(18,2)");
            });
        }
    }
}