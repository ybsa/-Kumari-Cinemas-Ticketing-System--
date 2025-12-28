using Microsoft.AspNetCore.Mvc;
using KumariCinemas.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authorization;

namespace KumariCinemas.Web.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly IConfiguration _configuration;

        public AdminController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Check if user is admin
        private async Task<bool> IsAdmin()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null) return false;
            int userId = int.Parse(userIdClaim.Value);

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT Role FROM M_Users WHERE UserId = @userId";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@userId", userId);
                    var result = await cmd.ExecuteScalarAsync();
                    return result?.ToString() == "ADMIN";
                }
            }
        }

        public async Task<IActionResult> Index()
        {
            if (!await IsAdmin())
            {
                TempData["Error"] = "Access Denied. Admin privileges required.";
                return RedirectToAction("Index", "Ticket");
            }

            // Get dashboard stats
            var stats = new Dictionary<string, int>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();

                // Total Users
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM M_Users";
                    stats["TotalUsers"] = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // Total Movies
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM M_Movies";
                    stats["TotalMovies"] = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // Total Bookings
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM T_Bookings";
                    stats["TotalBookings"] = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                // Total Revenue
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COALESCE(SUM(FinalPrice), 0) FROM T_Bookings WHERE Status != 'CANCELLED'";
                    stats["TotalRevenue"] = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }

            return View(stats);
        }

        public async Task<IActionResult> Movies()
        {
            if (!await IsAdmin()) return RedirectToAction("Index", "Ticket");

            var movies = new List<Movie>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT MovieId, Title, Duration, Language, Genre, ReleaseDate FROM M_Movies";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            movies.Add(new Movie
                            {
                                MovieId = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                Duration = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Language = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Genre = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                ReleaseDate = DateTime.Parse(reader.GetString(5))
                            });
                        }
                    }
                }
            }

            return View(movies);
        }

        [HttpGet]
        public async Task<IActionResult> AddMovie()
        {
            if (!await IsAdmin()) return RedirectToAction("Index", "Ticket");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMovie(string title, string duration, string language, string genre, DateTime releaseDate)
        {
            if (!await IsAdmin()) return RedirectToAction("Index", "Ticket");

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = @"INSERT INTO M_Movies (Title, Duration, Language, Genre, ReleaseDate) 
                               VALUES (@title, @duration, @language, @genre, @releaseDate)";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@title", title);
                    cmd.Parameters.AddWithValue("@duration", duration ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@language", language ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@genre", genre ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@releaseDate", releaseDate.ToString("yyyy-MM-dd HH:mm:ss"));
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            TempData["Success"] = "Movie added successfully!";
            return RedirectToAction("Movies");
        }

        public async Task<IActionResult> Bookings()
        {
            if (!await IsAdmin()) return RedirectToAction("Index", "Ticket");

            var bookings = new List<Booking>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = @"
                    SELECT b.BookingId, b.BookingTime, b.Status, b.FinalPrice, b.TotalTickets,
                           s.ShowDateTime, m.Title, u.Username
                    FROM T_Bookings b
                    JOIN M_Shows s ON b.ShowId = s.ShowId
                    JOIN M_Movies m ON s.MovieId = m.MovieId
                    JOIN M_Users u ON b.UserId = u.UserId
                    ORDER BY b.BookingTime DESC";

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            bookings.Add(new Booking
                            {
                                BookingId = reader.GetInt32(0),
                                BookingTime = DateTime.Parse(reader.GetString(1)),
                                Status = reader.GetString(2),
                                FinalPrice = reader.GetDecimal(3),
                                TotalTickets = reader.GetInt32(4),
                                ShowDateTime = DateTime.Parse(reader.GetString(5)),
                                MovieTitle = reader.GetString(6)
                            });
                        }
                    }
                }
            }

            return View(bookings);
        }

        [HttpGet]
        public async Task<IActionResult> AddShow()
        {
            if (!await IsAdmin()) return RedirectToAction("Index", "Ticket");

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();

                // Get movies
                var movies = new List<Movie>();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT MovieId, Title FROM M_Movies";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            movies.Add(new Movie { MovieId = reader.GetInt32(0), Title = reader.GetString(1) });
                        }
                    }
                }

                // Get halls
                var halls = new List<dynamic>();
                string hallSql = @"SELECT h.HallId, h.HallName, h.Capacity, b.BranchName 
                                   FROM M_Halls h 
                                   JOIN M_Branches b ON h.BranchId = b.BranchId";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = hallSql;
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            halls.Add(new { HallId = reader.GetInt32(0), HallName = reader.GetString(1), 
                                          Capacity = reader.GetInt32(2), BranchName = reader.GetString(3) });
                        }
                    }
                }

                ViewBag.Movies = movies;
                ViewBag.Halls = halls;
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddShow(int movieId, int hallId, DateTime showDateTime, decimal basePrice)
        {
            if (!await IsAdmin()) return RedirectToAction("Index", "Ticket");

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = "INSERT INTO M_Shows (MovieId, HallId, ShowDateTime, BasePrice) VALUES (@movieId, @hallId, @showDateTime, @basePrice)";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@movieId", movieId);
                    cmd.Parameters.AddWithValue("@hallId", hallId);
                    // Use standard date format
                    cmd.Parameters.AddWithValue("@showDateTime", showDateTime.ToString("yyyy-MM-dd HH:mm:ss")); 
                    cmd.Parameters.AddWithValue("@basePrice", basePrice);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            TempData["Success"] = "Show added successfully!";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> EditMovie(int id)
        {
            if (!await IsAdmin()) return RedirectToAction("Index", "Ticket");

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT MovieId, Title, Duration, Language, Genre, ReleaseDate, ImageUrl FROM M_Movies WHERE MovieId = @id";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var movie = new Movie
                            {
                                MovieId = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                Duration = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Language = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Genre = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                ReleaseDate = DateTime.Parse(reader.GetString(5)),
                                ImageUrl = reader.IsDBNull(6) ? "" : reader.GetString(6)
                            };
                            return View(movie);
                        }
                    }
                }
            }

            return RedirectToAction("Movies");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMovie(int movieId, string title, string duration, string language, string genre, DateTime releaseDate, string imageUrl)
        {
            if (!await IsAdmin()) return RedirectToAction("Index", "Ticket");

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = @"UPDATE M_Movies SET Title = @title, Duration = @duration, Language = @language, 
                               Genre = @genre, ReleaseDate = @releaseDate, ImageUrl = @imageUrl WHERE MovieId = @movieId";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@title", title);
                    cmd.Parameters.AddWithValue("@duration", duration ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@language", language ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@genre", genre ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@releaseDate", releaseDate.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@imageUrl", imageUrl ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@movieId", movieId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            TempData["Success"] = "Movie updated successfully!";
            return RedirectToAction("Movies");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMovie(int movieId)
        {
            if (!await IsAdmin()) return RedirectToAction("Index", "Ticket");

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                
                // Check if movie has shows
                string checkSql = "SELECT COUNT(*) FROM M_Shows WHERE MovieId = @movieId";
                using (var checkCmd = connection.CreateCommand())
                {
                    checkCmd.CommandText = checkSql;
                    checkCmd.Parameters.AddWithValue("@movieId", movieId);
                    int showCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                    
                    if (showCount > 0)
                    {
                        TempData["Error"] = "Cannot delete movie with existing shows. Delete shows first.";
                        return RedirectToAction("Movies");
                    }
                }

                string sql = "DELETE FROM M_Movies WHERE MovieId = @movieId";
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@movieId", movieId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            TempData["Success"] = "Movie deleted successfully!";
            return RedirectToAction("Movies");
        }
    }
}
