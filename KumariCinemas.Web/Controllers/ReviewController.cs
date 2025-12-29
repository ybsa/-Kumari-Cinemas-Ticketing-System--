using Microsoft.AspNetCore.Mvc;
using KumariCinemas.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace KumariCinemas.Web.Controllers
{
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly IConfiguration _configuration;

        public ReviewController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int movieId, int rating, string comment)
        {
            // SECURE: Get UserId from Session
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null) return RedirectToAction("Login", "User");
            int userId = int.Parse(userIdClaim.Value);

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = @"
                    INSERT INTO T_Reviews (MovieId, UserId, Rating, CommentText) 
                    VALUES (@movieId, @userId, @rating, @commentText)";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    command.Parameters.AddWithValue("@movieId", movieId);
                    command.Parameters.AddWithValue("@userId", userId);
                    command.Parameters.AddWithValue("@rating", rating);
                    command.Parameters.AddWithValue("@commentText", comment ?? (object)DBNull.Value);
                    await command.ExecuteNonQueryAsync();
                }
            }

            TempData["Success"] = "Thanks for your review! 🌟";
            return RedirectToAction("Index", "Ticket"); 
        }
    }
}
