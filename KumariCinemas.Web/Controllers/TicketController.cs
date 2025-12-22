using Microsoft.AspNetCore.Mvc;
using KumariCinemas.Web.Models;
using KumariCinemas.Web.Services;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace KumariCinemas.Web.Controllers
{
    public class TicketController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IPricingService _pricingService;

        public TicketController(IConfiguration configuration, IPricingService pricingService)
        {
            _configuration = configuration;
            _pricingService = pricingService;
        }

        public async Task<IActionResult> Index()
        {
            var shows = new List<Show>();
            string connectionString = _configuration.GetConnectionString("OracleDb");

            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = @"
                    SELECT s.ShowId, s.MovieId, s.HallId, s.ShowDateTime, s.BasePrice, 
                           m.Title, m.ReleaseDate, m.Genre 
                    FROM M_Shows s 
                    JOIN M_Movies m ON s.MovieId = m.MovieId";

                using (var command = new OracleCommand(sql, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var show = new Show
                        {
                            ShowId = reader.GetInt32(0),
                            MovieId = reader.GetInt32(1),
                            HallId = reader.GetInt32(2),
                            ShowDateTime = reader.GetDateTime(3),
                            BasePrice = reader.GetDecimal(4),
                            Movie = new Movie 
                            { 
                                Title = reader.GetString(5),
                                ReleaseDate = reader.GetDateTime(6),
                                Genre = reader.GetString(7)
                            }
                        };

                        // Calculate dynamic price
                        bool isNewRelease = (show.Movie.ReleaseDate >= DateTime.Now.AddDays(-7));
                        show.CalculatedPrice = _pricingService.CalculatePrice(show.BasePrice, show.ShowDateTime, isNewRelease);
                        
                        shows.Add(show);
                    }
                }
            }

            return View(shows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(int showId, int userId, decimal finalPrice, int quantity)
        {
            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();

                // Start Transaction for Atomicity ("Seat Locking Lite")
                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try 
                    {
                        // 1. Check Capacity (Locked Context)
                        string capacitySql = @"
                            SELECT h.Capacity, 
                                   (SELECT COALESCE(SUM(b.TotalTickets), 0) FROM T_Bookings b WHERE b.ShowId = s.ShowId AND b.Status != 'CANCELLED') as BookedCount
                            FROM M_Shows s
                            JOIN M_Halls h ON s.HallId = h.HallId
                            WHERE s.ShowId = :showId"; // In a real app, add 'FOR UPDATE' to lock rows

                        int capacity = 0;
                        int booked = 0;

                        using (var capCmd = new OracleCommand(capacitySql, connection))
                        {
                            capCmd.Transaction = transaction;
                            capCmd.Parameters.Add("showId", showId);
                            using (var reader = await capCmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    capacity = reader.GetInt32(0);
                                    booked = reader.GetInt32(1);
                                }
                            }
                        }

                        if (booked + quantity > capacity)
                        {
                            TempData["Error"] = $"Booking Failed: Only {capacity - booked} seats remaining.";
                            return RedirectToAction("Index");
                        }

                        // 2. Proceed with Booking
                        string sql = @"
                            INSERT INTO T_Bookings (UserId, ShowId, Status, FinalPrice, TotalTickets) 
                            VALUES (:userId, :showId, 'BOOKED', :finalPrice, :quantity)";

                        using (var command = new OracleCommand(sql, connection))
                        {
                            command.Transaction = transaction;
                            command.Parameters.Add("userId", userId);
                            command.Parameters.Add("showId", showId);
                            command.Parameters.Add("finalPrice", finalPrice);
                            command.Parameters.Add("quantity", quantity);
                            await command.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                    }
                    catch 
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            return RedirectToAction("MyBookings", new { userId = userId });
        }

        public async Task<IActionResult> MyBookings(int userId)
        {
            var bookings = new List<Booking>();
            string connectionString = _configuration.GetConnectionString("OracleDb");

            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = @"
                    SELECT b.BookingId, b.UserId, b.ShowId, b.BookingTime, b.Status, b.FinalPrice, b.TotalTickets,
                           s.ShowDateTime, m.Title 
                    FROM T_Bookings b
                    JOIN M_Shows s ON b.ShowId = s.ShowId
                    JOIN M_Movies m ON s.MovieId = m.MovieId
                    WHERE b.UserId = :userId";

                using (var command = new OracleCommand(sql, connection))
                {
                    command.Parameters.Add("userId", userId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Logic placeholder for MyBookings display (can be enhanced to show date)
                            bookings.Add(new Booking
                            {
                                BookingId = reader.GetInt32(0),
                                BookingTime = reader.GetDateTime(3),
                                Status = reader.GetString(4),
                                FinalPrice = reader.GetDecimal(5),
                                TotalTickets = reader.GetInt32(6),
                                ShowDateTime = reader.GetDateTime(7),
                                MovieTitle = reader.GetString(8)
                            });
                        }
                    }
                }
            }
            return View(bookings);
        }
    }
}
