using Microsoft.AspNetCore.Mvc;
using KumariCinemas.Web.Models;
using KumariCinemas.Web.Services;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using Microsoft.AspNetCore.Authorization;

namespace KumariCinemas.Web.Controllers
{
    [Authorize] // Enforce Login
    public class TicketController : Controller
    {
        private readonly IConfiguration _configuration;

        public TicketController(IConfiguration configuration)
        {
            _configuration = configuration;
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

                        // Use Static Helper for Price Calculation
                        bool isNewRelease = (show.Movie.ReleaseDate >= DateTime.Now.AddDays(-7));
                        show.CalculatedPrice = PricingHelper.CalculatePrice(show.BasePrice, show.ShowDateTime, isNewRelease);
                        
                        shows.Add(show);
                    }
                }
            }

            return View(shows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(int showId, int quantity)
        {
            // SECURE: Get UserId from Session (Claim), never trust user input
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null) return RedirectToAction("Login", "User");
            int userId = int.Parse(userIdClaim.Value);

            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();

                // Start Transaction for Atomicity
                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try 
                    {
                        // 1. SECURE: Recalculate Price Server-Side (Prevent Tampering)
                        // Fetch Show Details
                        decimal basePrice = 0;
                        DateTime showDateTime = DateTime.MinValue;
                        bool isNewRelease = false;

                        string priceSql = @"
                            SELECT s.BasePrice, s.ShowDateTime,
                                   CASE WHEN m.ReleaseDate >= SYSDATE - 7 THEN 1 ELSE 0 END as IsNewRelease
                            FROM M_Shows s
                            JOIN M_Movies m ON s.MovieId = m.MovieId
                            WHERE s.ShowId = :showId";

                        using (var priceCmd = new OracleCommand(priceSql, connection))
                        {
                            priceCmd.Transaction = transaction;
                            priceCmd.Parameters.Add("showId", showId);
                            using (var reader = await priceCmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    basePrice = reader.GetDecimal(0);
                                    showDateTime = reader.GetDateTime(1);
                                    isNewRelease = reader.GetInt32(2) == 1;
                                }
                                else 
                                {
                                    throw new Exception("Show not found");
                                }
                            }
                        }

                        decimal finalPrice = PricingHelper.CalculatePrice(basePrice, showDateTime, isNewRelease) * quantity;

                        // 2. SECURE: Check Capacity with LOCKING (Prevent Race Conditions)
                        // Note: Oracle's 'FOR UPDATE' locks the rows returned.
                        string capacitySql = @"
                            SELECT h.Capacity, 
                                   (SELECT COALESCE(SUM(b.TotalTickets), 0) FROM T_Bookings b WHERE b.ShowId = s.ShowId AND b.Status != 'CANCELLED') as BookedCount
                            FROM M_Shows s
                            JOIN M_Halls h ON s.HallId = h.HallId
                            WHERE s.ShowId = :showId
                            FOR UPDATE"; 

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

                        // 3. Proceed with Booking and get BookingId
                        int bookingId = 0;
                        string sql = @"
                            INSERT INTO T_Bookings (UserId, ShowId, Status, FinalPrice, TotalTickets) 
                            VALUES (:userId, :showId, 'BOOKED', :finalPrice, :quantity)
                            RETURNING BookingId INTO :bookingId";

                        using (var command = new OracleCommand(sql, connection))
                        {
                            command.Transaction = transaction;
                            command.Parameters.Add(new OracleParameter("userId", OracleDbType.Int32) { Value = userId });
                            command.Parameters.Add(new OracleParameter("showId", OracleDbType.Int32) { Value = showId });
                            command.Parameters.Add(new OracleParameter("finalPrice", OracleDbType.Decimal) { Value = finalPrice });
                            command.Parameters.Add(new OracleParameter("quantity", OracleDbType.Int32) { Value = quantity });
                            command.Parameters.Add(new OracleParameter("bookingId", OracleDbType.Int32) { Direction = System.Data.ParameterDirection.Output });
                            await command.ExecuteNonQueryAsync();
                            bookingId = Convert.ToInt32(command.Parameters["bookingId"].Value.ToString());
                        }

                        transaction.Commit();
                        
                        // Redirect to confirmation page
                        return RedirectToAction("BookingConfirmation", new { id = bookingId });
                    }
                    catch 
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> MyBookings()
        {
            // SECURE: Get UserId from Session
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null) return RedirectToAction("Login", "User");
            int userId = int.Parse(userIdClaim.Value);

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

        public async Task<IActionResult> BookingConfirmation(int id)
        {
            var booking = new Booking();
            string connectionString = _configuration.GetConnectionString("OracleDb");

            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = @"
                    SELECT b.BookingId, b.BookingTime, b.Status, b.FinalPrice, b.TotalTickets,
                           s.ShowDateTime, m.Title, h.HallName, br.BranchName
                    FROM T_Bookings b
                    JOIN M_Shows s ON b.ShowId = s.ShowId
                    JOIN M_Movies m ON s.MovieId = m.MovieId
                    JOIN M_Halls h ON s.HallId = h.HallId
                    JOIN M_Branches br ON h.BranchId = br.BranchId
                    WHERE b.BookingId = :bookingId";

                using (var command = new OracleCommand(sql, connection))
                {
                    command.Parameters.Add(new OracleParameter("bookingId", OracleDbType.Int32) { Value = id });
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            booking.BookingId = reader.GetInt32(0);
                            booking.BookingTime = reader.GetDateTime(1);
                            booking.Status = reader.GetString(2);
                            booking.FinalPrice = reader.GetDecimal(3);
                            booking.TotalTickets = reader.GetInt32(4);
                            booking.ShowDateTime = reader.GetDateTime(5);
                            booking.MovieTitle = reader.GetString(6);
                            // Store extra info in ViewBag
                            ViewBag.HallName = reader.GetString(7);
                            ViewBag.BranchName = reader.GetString(8);
                        }
                    }
                }
            }

            return View(booking);
        }
    }
}
