using System;

namespace KumariCinemas.Web.Models
{
    public class Review
    {
        public int ReviewId { get; set; }
        public int MovieId { get; set; }
        public int UserId { get; set; }
        public double Rating { get; set; } // 1.0 to 5.0
        public string CommentText { get; set; }
        public DateTime ReviewDate { get; set; }

        public string Username { get; set; } // Helper for display
    }
}
