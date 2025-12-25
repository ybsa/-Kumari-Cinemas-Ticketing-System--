using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KumariCinemas.Web.Models;
using Oracle.ManagedDataAccess.Client;

namespace KumariCinemas.Web.Controllers
{
    [Authorize]
    public class WatchlistController : Controller
    {
        private readonly IConfiguration _configuration;

        public WatchlistController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null) return RedirectToAction("Login", "User");
            int userId = int.Parse(userIdClaim.Value);

            var movies = new List<Movie>();
            string connectionString = _configuration.GetConnectionString("OracleDb");

            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = @"
                    SELECT m.MovieId, m.Title, m.Genre, m.Duration, m.ImageUrl, m.ReleaseDate
                    FROM T_Watchlist w
                    JOIN M_Movies m ON w.MovieId = m.MovieId
                    WHERE w.UserId = :userId
                    ORDER BY w.AddedDate DESC";

                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.Parameters.Add("userId", userId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            movies.Add(new Movie
                            {
                                MovieId = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                Genre = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Duration = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                ImageUrl = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                ReleaseDate = reader.GetDateTime(5)
                            });
                        }
                    }
                }
            }

            return View(movies);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int movieId, string returnUrl)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null) return RedirectToAction("Login", "User");
            int userId = int.Parse(userIdClaim.Value);

            try
            {
                string connectionString = _configuration.GetConnectionString("OracleDb");
                using (var connection = new OracleConnection(connectionString))
                {
                    await connection.OpenAsync();
                    // Use MERGE or IGNORE to handle duplicates easily, or just catch exception
                    // Simple INSERT with exception handling for Unique Constraint
                    string sql = "INSERT INTO T_Watchlist (UserId, MovieId) VALUES (:userId, :movieId)";
                    using (var cmd = new OracleCommand(sql, connection))
                    {
                        cmd.Parameters.Add("userId", userId);
                        cmd.Parameters.Add("movieId", movieId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                TempData["Success"] = "Added to Watchlist";
            }
            catch (OracleException ex)
            {
                if (ex.Number == 1) // Unique constraint violation
                {
                    TempData["Info"] = "Already in Watchlist";
                }
                else
                {
                    TempData["Error"] = "Error adding to watchlist";
                }
            }

            if (!string.IsNullOrEmpty(returnUrl)) return LocalRedirect(returnUrl);
            return RedirectToAction("Index", "Ticket");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int movieId, string returnUrl)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null) return RedirectToAction("Login", "User");
            int userId = int.Parse(userIdClaim.Value);

            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = "DELETE FROM T_Watchlist WHERE UserId = :userId AND MovieId = :movieId";
                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.Parameters.Add("userId", userId);
                    cmd.Parameters.Add("movieId", movieId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            TempData["Success"] = "Removed from Watchlist";

            if (!string.IsNullOrEmpty(returnUrl)) return LocalRedirect(returnUrl);
            return RedirectToAction("Index");
        }
    }
}
