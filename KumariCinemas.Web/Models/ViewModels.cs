namespace KumariCinemas.Web.Models
{
    public class Movie
    {
        public int MovieId { get; set; }
        public string Title { get; set; }
        public string Duration { get; set; }
        public string Language { get; set; }
        public string Genre { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
    }

    public class Show
    {
        public int ShowId { get; set; }
        public int MovieId { get; set; }
        public int HallId { get; set; }
        public DateTime ShowDateTime { get; set; }
        public decimal BasePrice { get; set; }
        
        // Navigation properties/Helper properties
        public Movie Movie { get; set; }
        public decimal CalculatedPrice { get; set; }
    }

    public class Booking
    {
        public int BookingId { get; set; }
        public int UserId { get; set; }
        public int ShowId { get; set; }
        public DateTime BookingTime { get; set; }
        public string Status { get; set; }
        public decimal FinalPrice { get; set; }
        public int TotalTickets { get; set; }

        // Helper properties for display
        public string MovieTitle { get; set; }
        public DateTime ShowDateTime { get; set; }
    }
}
