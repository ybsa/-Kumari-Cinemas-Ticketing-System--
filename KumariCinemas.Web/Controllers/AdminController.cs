using Microsoft.AspNetCore.Mvc;
using KumariCinemas.Web.Models;
using Oracle.ManagedDataAccess.Client;
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

            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT Role FROM M_Users WHERE UserId = :userId";
                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.Parameters.Add(new OracleParameter("userId", OracleDbType.Int32) { Value = userId });
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
            string connectionString = _configuration.GetConnectionString("OracleDb");

            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();

                // Total Users
                using (var cmd = new OracleCommand("SELECT COUNT(*) FROM M_Users", connection))
                    stats["TotalUsers"] = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                // Total Movies
                using (var cmd = new OracleCommand("SELECT COUNT(*) FROM M_Movies", connection))
                    stats["TotalMovies"] = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                // Total Bookings
                using (var cmd = new OracleCommand("SELECT COUNT(*) FROM T_Bookings", connection))
                    stats["TotalBookings"] = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                // Total Revenue
                using (var cmd = new OracleCommand("SELECT COALESCE(SUM(FinalPrice), 0) FROM T_Bookings WHERE Status != 'CANCELLED'", connection))
                    stats["TotalRevenue"] = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            return View(stats);
        }

        public async Task<IActionResult> Movies()
        {
            if (!await IsAdmin()) return RedirectToAction("Index", "Ticket");

            var movies = new List<Movie>();
            string connectionString = _configuration.GetConnectionString("OracleDb");

            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT MovieId, Title, Duration, Language, Genre, ReleaseDate FROM M_Movies";
                using (var cmd = new OracleCommand(sql, connection))
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
                            ReleaseDate = reader.GetDateTime(5)
                        });
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

            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = @"INSERT INTO M_Movies (Title, Duration, Language, Genre, ReleaseDate) 
                               VALUES (:title, :duration, :language, :genre, :releaseDate)";
                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.Parameters.Add(new OracleParameter("title", OracleDbType.Varchar2) { Value = title });
                    cmd.Parameters.Add(new OracleParameter("duration", OracleDbType.Varchar2) { Value = duration ?? "" });
                    cmd.Parameters.Add(new OracleParameter("language", OracleDbType.Varchar2) { Value = language ?? "" });
                    cmd.Parameters.Add(new OracleParameter("genre", OracleDbType.Varchar2) { Value = genre ?? "" });
                    cmd.Parameters.Add(new OracleParameter("releaseDate", OracleDbType.Date) { Value = releaseDate });
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
            string connectionString = _configuration.GetConnectionString("OracleDb");

            using (var connection = new OracleConnection(connectionString))
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

                using (var cmd = new OracleCommand(sql, connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        bookings.Add(new Booking
                        {
                            BookingId = reader.GetInt32(0),
                            BookingTime = reader.GetDateTime(1),
                            Status = reader.GetString(2),
                            FinalPrice = reader.GetDecimal(3),
                            TotalTickets = reader.GetInt32(4),
                            ShowDateTime = reader.GetDateTime(5),
                            MovieTitle = reader.GetString(6)
                        });
                    }
                }
            }

            return View(bookings);
        }

        [HttpGet]
        public async Task<IActionResult> AddShow()
        {
            if (!await IsAdmin()) return RedirectToAction("Index", "Ticket");

            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();

                // Get movies
                var movies = new List<Movie>();
                using (var cmd = new OracleCommand("SELECT MovieId, Title FROM M_Movies", connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        movies.Add(new Movie { MovieId = reader.GetInt32(0), Title = reader.GetString(1) });
                    }
                }

                // Get halls
                var halls = new List<dynamic>();
                string hallSql = @"SELECT h.HallId, h.HallName, h.Capacity, b.BranchName 
                                   FROM M_Halls h 
                                   JOIN M_Branches b ON h.BranchId = b.BranchId";
                using (var cmd = new OracleCommand(hallSql, connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        halls.Add(new { HallId = reader.GetInt32(0), HallName = reader.GetString(1), 
                                      Capacity = reader.GetInt32(2), BranchName = reader.GetString(3) });
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

            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = "INSERT INTO M_Shows (MovieId, HallId, ShowDateTime, BasePrice) VALUES (:movieId, :hallId, :showDateTime, :basePrice)";
                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.Parameters.Add(new OracleParameter("movieId", OracleDbType.Int32) { Value = movieId });
                    cmd.Parameters.Add(new OracleParameter("hallId", OracleDbType.Int32) { Value = hallId });
                    cmd.Parameters.Add(new OracleParameter("showDateTime", OracleDbType.TimeStamp) { Value = showDateTime });
                    cmd.Parameters.Add(new OracleParameter("basePrice", OracleDbType.Decimal) { Value = basePrice });
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

            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT MovieId, Title, Duration, Language, Genre, ReleaseDate, ImageUrl FROM M_Movies WHERE MovieId = :id";
                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int32) { Value = id });
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
                                ReleaseDate = reader.GetDateTime(5),
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

            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = @"UPDATE M_Movies SET Title = :title, Duration = :duration, Language = :language, 
                               Genre = :genre, ReleaseDate = :releaseDate, ImageUrl = :imageUrl WHERE MovieId = :movieId";
                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.Parameters.Add(new OracleParameter("title", OracleDbType.Varchar2) { Value = title });
                    cmd.Parameters.Add(new OracleParameter("duration", OracleDbType.Varchar2) { Value = duration ?? "" });
                    cmd.Parameters.Add(new OracleParameter("language", OracleDbType.Varchar2) { Value = language ?? "" });
                    cmd.Parameters.Add(new OracleParameter("genre", OracleDbType.Varchar2) { Value = genre ?? "" });
                    cmd.Parameters.Add(new OracleParameter("releaseDate", OracleDbType.Date) { Value = releaseDate });
                    cmd.Parameters.Add(new OracleParameter("imageUrl", OracleDbType.Varchar2) { Value = imageUrl ?? "" });
                    cmd.Parameters.Add(new OracleParameter("movieId", OracleDbType.Int32) { Value = movieId });
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

            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                
                // Check if movie has shows
                string checkSql = "SELECT COUNT(*) FROM M_Shows WHERE MovieId = :movieId";
                using (var checkCmd = new OracleCommand(checkSql, connection))
                {
                    checkCmd.Parameters.Add(new OracleParameter("movieId", OracleDbType.Int32) { Value = movieId });
                    int showCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                    
                    if (showCount > 0)
                    {
                        TempData["Error"] = "Cannot delete movie with existing shows. Delete shows first.";
                        return RedirectToAction("Movies");
                    }
                }

                string sql = "DELETE FROM M_Movies WHERE MovieId = :movieId";
                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.Parameters.Add(new OracleParameter("movieId", OracleDbType.Int32) { Value = movieId });
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            TempData["Success"] = "Movie deleted successfully!";
            return RedirectToAction("Movies");
        }
    }
}
