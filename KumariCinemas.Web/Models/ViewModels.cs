namespace KumariCinemas.Web.Models
{
    public class Movie
    {
        public int MovieId { get; set; }
        public required string Title { get; set; }
        public string? Duration { get; set; }
        public string? Language { get; set; }
        public string? Genre { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class Show
    {
        public int ShowId { get; set; }
        public int MovieId { get; set; }
        public int HallId { get; set; }
        public DateTime ShowDateTime { get; set; }
        public decimal BasePrice { get; set; }
        
        // Navigation properties/Helper properties
        public required Movie Movie { get; set; }
        public decimal CalculatedPrice { get; set; }
    }

    public class MovieDetailsViewModel
    {
        public Movie Movie { get; set; }
        public List<Show> UpcomingShows { get; set; }
        public List<ReviewViewModel> Reviews { get; set; }
        public double AverageRating { get; set; }
        public bool IsInWatchlist { get; set; }
    }

    public class ReviewViewModel
    {
        public int ReviewId { get; set; }
        public string Username { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public DateTime ReviewDate { get; set; }
    }

    public class Booking
    {
        public int BookingId { get; set; }
        public int UserId { get; set; }
        public int ShowId { get; set; }
        public DateTime BookingTime { get; set; }
        public string? Status { get; set; }
        public decimal FinalPrice { get; set; }
        public int TotalTickets { get; set; }

        // Helper properties for display
        public string? MovieTitle { get; set; }
        public DateTime ShowDateTime { get; set; }
    }
}
