using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;


namespace ScraperAPI.Models
{
    [Index(nameof(OfferId), IsUnique = true)]
    public class Offer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [JsonPropertyName("offer_id")]

        public int OfferId { get; set; }

        [JsonPropertyName("tsin_id")]
        public int TsinId { get; set; }

        [JsonPropertyName("image_url")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("sku")]
        public string? Sku { get; set; }

        [JsonPropertyName("barcode")]
        public string? Barcode { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("selling_price")]
        public decimal? SellingPrice { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("offer_url")]
        public string? OfferUrl { get; set; }

        public decimal? CompetitorPrice { get; set; }

        [JsonPropertyName("date_created")]
        public string? DateCreated { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}