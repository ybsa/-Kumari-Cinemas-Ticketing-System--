using Microsoft.AspNetCore.Mvc;
using KumariCinemas.Web.Models;
using KumariCinemas.Web.Services;
using Microsoft.Data.Sqlite;
using System.Data;
using Microsoft.AspNetCore.Authorization;

namespace KumariCinemas.Web.Controllers
{
    // [Authorize] removed from class level to allow browsing
    public class TicketController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TicketController> _logger;

        public TicketController(IConfiguration configuration, ILogger<TicketController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetSeatAvailability(int showId)
        {
            var bookedSeats = new List<string>();
            int capacity = 0;

            try 
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new SqliteConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // 1. Get Capacity
                    string capSql = @"
                        SELECT h.Capacity 
                        FROM M_Shows s
                        JOIN M_Halls h ON s.HallId = h.HallId
                        WHERE s.ShowId = @showId";
                    
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = capSql;
                        cmd.Parameters.AddWithValue("@showId", showId);
                        object result = await cmd.ExecuteScalarAsync();
                        if (result != null) capacity = Convert.ToInt32(result);
                    }

                    // 2. Get Booked Seats
                    string seatSql = @"
                        SELECT t.SeatNumber 
                        FROM T_Tickets t
                        JOIN T_Bookings b ON t.BookingId = b.BookingId
                        WHERE b.ShowId = @showId AND b.Status IN ('BOOKED', 'PAID')";

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = seatSql;
                        cmd.Parameters.AddWithValue("@showId", showId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                bookedSeats.Add(reader.GetString(0));
                            }
                        }
                    }
                }

                return Json(new { bookedSeats, capacity });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching seat availability for show {ShowId}", showId);
                return StatusCode(500, "Internal server error");
            }
        }

        public async Task<IActionResult> Index(string search, string genre, DateTime? date)
        {
            try
            {
                var shows = new List<Show>();
                string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                
                // Build dynamic SQL with filters
                // SQLite: ReleaseDate is text. To check 'RELEASED >= NOW-7', we use date(m.ReleaseDate) >= date('now', '-7 days')
                string sql = @"
                    SELECT s.ShowId, s.MovieId, s.HallId, s.ShowDateTime, s.BasePrice, 
                           m.Title, m.ReleaseDate, m.Genre, m.ImageUrl 
                    FROM M_Shows s 
                    JOIN M_Movies m ON s.MovieId = m.MovieId
                    WHERE 1=1";

                using (var command = connection.CreateCommand())
                {

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        sql += " AND UPPER(m.Title) LIKE UPPER(@search)";
                        command.Parameters.AddWithValue("@search", $"%{search}%");
                    }

                    if (!string.IsNullOrWhiteSpace(genre))
                    {
                        sql += " AND m.Genre = @genre";
                        command.Parameters.AddWithValue("@genre", genre);
                    }

                    if (date.HasValue)
                    {
                        // Compare usage of YYYY-MM-DD
                        sql += " AND date(s.ShowDateTime) = date(@showDate)";
                        command.Parameters.AddWithValue("@showDate", date.Value.ToString("yyyy-MM-dd"));
                    }

                    command.CommandText = sql;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var show = new Show
                            {
                                ShowId = reader.GetInt32(0),
                                MovieId = reader.GetInt32(1),
                                HallId = reader.GetInt32(2),
                                ShowDateTime = DateTime.Parse(reader.GetString(3)),
                                BasePrice = reader.GetDecimal(4),
                                Movie = new Movie 
                                { 
                                    Title = reader.GetString(5),
                                    ReleaseDate = DateTime.Parse(reader.GetString(6)),
                                    Genre = reader.GetString(7),
                                    ImageUrl = reader.IsDBNull(8) ? null : reader.GetString(8)
                                }
                            };

                            // Use Static Helper for Price Calculation
                            bool isNewRelease = (show.Movie.ReleaseDate >= DateTime.Now.AddDays(-7));
                            show.CalculatedPrice = PricingHelper.CalculatePrice(show.BasePrice, show.ShowDateTime, isNewRelease);
                            
                            shows.Add(show);
                        }
                    }
                }
            }
                return View(shows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading shows");
                TempData["Error"] = "Unable to load shows. Please try again later.";
                return View(new List<Show>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Book(int showId, int quantity, string seats = "")
        {
            // Input validation
            if (showId <= 0 || quantity <= 0 || quantity > 10)
            {
                TempData["Error"] = "Invalid booking parameters.";
                return RedirectToAction("Index");
            }

            // SECURE: Get UserId from Session (Claim), never trust user input
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null) return RedirectToAction("Login", "User");
            int userId = int.Parse(userIdClaim.Value);

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();

                // Start Transaction for Atomicity
                using (var transaction = connection.BeginTransaction())
                {
                    try 
                    {
                        // 1. SECURE: Recalculate Price Server-Side (Prevent Tampering)
                        // Fetch Show Details
                        decimal basePrice = 0;
                        DateTime showDateTime = DateTime.MinValue;
                        bool isNewRelease = false;

                        // SQLite: ReleaseDate is text. Use date(...) for comparisons.
                        // "julianday('now') - julianday(ReleaseDate) < 7" logic or simply check date string if consistent.
                        // Let's use ReleaseDate string parsing in C# logic if easier, but here we do it in SQL.
                        // We'll rely on our seed format YYYY-MM-DD HH:MM:SS
                        string priceSql = @"
                            SELECT s.BasePrice, s.ShowDateTime,
                                   CASE WHEN date(m.ReleaseDate) >= date('now', '-7 days') THEN 1 ELSE 0 END as IsNewRelease
                            FROM M_Shows s
                            JOIN M_Movies m ON s.MovieId = m.MovieId
                            WHERE s.ShowId = @showId";

                        using (var priceCmd = connection.CreateCommand())
                        {
                            priceCmd.Transaction = transaction;
                            priceCmd.CommandText = priceSql;
                            priceCmd.Parameters.AddWithValue("@showId", showId);
                            using (var reader = await priceCmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    basePrice = reader.GetDecimal(0);
                                    showDateTime = DateTime.Parse(reader.GetString(1));
                                    isNewRelease = reader.GetInt32(2) == 1;
                                }
                                else 
                                {
                                    throw new Exception("Show not found");
                                }
                            }
                        }

                        decimal finalPrice = PricingHelper.CalculatePrice(basePrice, showDateTime, isNewRelease) * quantity;

                        // 2. SECURE: Check Capacity (SQLite doesn't support FOR UPDATE in same way, 
                        // but default locking behavior usually prevents dirty reads during transaction).
                        string capacitySql = @"
                            SELECT h.Capacity, 
                                   (SELECT COALESCE(SUM(b.TotalTickets), 0) FROM T_Bookings b WHERE b.ShowId = s.ShowId AND b.Status != 'CANCELLED') as BookedCount
                            FROM M_Shows s
                            JOIN M_Halls h ON s.HallId = h.HallId
                            WHERE s.ShowId = @showId"; 

                        int capacity = 0;
                        int booked = 0;

                        using (var capCmd = connection.CreateCommand())
                        {
                            capCmd.Transaction = transaction;
                            capCmd.CommandText = capacitySql;
                            capCmd.Parameters.AddWithValue("@showId", showId);
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

                        // 3. Parse and validate seats if provided
                        var selectedSeats = new List<string>();
                        if (!string.IsNullOrWhiteSpace(seats))
                        {
                            selectedSeats = seats.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
                            
                            // Verify quantity matches selected seats
                            if (selectedSeats.Count != quantity)
                            {
                                TempData["Error"] = "Number of selected seats doesn't match quantity.";
                                return RedirectToAction("Index");
                            }

                            // Check if any selected seats are already booked
                            // Building dynamic IN clause
                            var paramNames = selectedSeats.Select((s, i) => $"@seat{i}").ToList();
                            string seatCheckSql = $@"
                                SELECT COUNT(*) 
                                FROM T_Tickets t
                                JOIN T_Bookings b ON t.BookingId = b.BookingId
                                WHERE b.ShowId = @showId 
                                  AND b.Status IN ('BOOKED', 'PAID')
                                  AND t.SeatNumber IN ({string.Join(",", paramNames)})";

                            using (var seatCheckCmd = connection.CreateCommand())
                            {
                                seatCheckCmd.Transaction = transaction;
                                seatCheckCmd.CommandText = seatCheckSql;
                                seatCheckCmd.Parameters.AddWithValue("@showId", showId);
                                for (int i = 0; i < selectedSeats.Count; i++)
                                {
                                    seatCheckCmd.Parameters.AddWithValue(paramNames[i], selectedSeats[i]);
                                }
                                
                                var seatsTaken = Convert.ToInt32(await seatCheckCmd.ExecuteScalarAsync());
                                if (seatsTaken > 0)
                                {
                                    TempData["Error"] = "Some selected seats are already booked. Please refresh and try again.";
                                    return RedirectToAction("Index");
                                }
                            }
                        }

                        // 4. Proceed with Booking and get BookingId
                        int bookingId = 0;
                        string sql = @"
                            INSERT INTO T_Bookings (UserId, ShowId, Status, FinalPrice, TotalTickets) 
                            VALUES (@userId, @showId, 'BOOKED', @finalPrice, @quantity);
                            SELECT last_insert_rowid();";

                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = sql;
                            command.Parameters.AddWithValue("@userId", userId);
                            command.Parameters.AddWithValue("@showId", showId);
                            command.Parameters.AddWithValue("@finalPrice", finalPrice);
                            command.Parameters.AddWithValue("@quantity", quantity);
                            
                            bookingId = Convert.ToInt32(await command.ExecuteScalarAsync());
                        }

                        // 5. Insert selected seats into T_Tickets
                        if (selectedSeats.Any())
                        {
                            foreach (var seat in selectedSeats)
                            {
                                string ticketSql = "INSERT INTO T_Tickets (BookingId, SeatNumber) VALUES (@bookingId, @seatNumber)";
                                using (var ticketCmd = connection.CreateCommand())
                                {
                                    ticketCmd.Transaction = transaction;
                                    ticketCmd.CommandText = ticketSql;
                                    ticketCmd.Parameters.AddWithValue("@bookingId", bookingId);
                                    ticketCmd.Parameters.AddWithValue("@seatNumber", seat);
                                    await ticketCmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        transaction.Commit();
                        
                        // Redirect to payment page instead of confirmation
                        return RedirectToAction("Index", "Payment", new { bookingId });
                    }
                    catch (SqliteException ex)
                    {
                        transaction.Rollback();
                        _logger.LogError(ex, "Database error during booking for show {ShowId}", showId);
                        TempData["Error"] = "Database error occurred. Please try again.";
                        return RedirectToAction("Index");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        _logger.LogError(ex, "Unexpected error during booking");
                        TempData["Error"] = "An unexpected error occurred. Please try again.";
                        return RedirectToAction("Index");
                    }
                }
            }

            return RedirectToAction("Index");
        }

        [Authorize]
        public async Task<IActionResult> MyBookings()
        {
            // SECURE: Get UserId from Session
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null) return RedirectToAction("Login", "User");
            int userId = int.Parse(userIdClaim.Value);

            var bookings = new List<Booking>();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = @"
                    SELECT b.BookingId, b.UserId, b.ShowId, b.BookingTime, b.Status, b.FinalPrice, b.TotalTickets,
                           s.ShowDateTime, m.Title 
                    FROM T_Bookings b
                    JOIN M_Shows s ON b.ShowId = s.ShowId
                    JOIN M_Movies m ON s.MovieId = m.MovieId
                    WHERE b.UserId = @userId";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    command.Parameters.AddWithValue("@userId", userId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            bookings.Add(new Booking
                            {
                                BookingId = reader.GetInt32(0),
                                BookingTime = DateTime.Parse(reader.GetString(3)),
                                Status = reader.GetString(4),
                                FinalPrice = reader.GetDecimal(5),
                                TotalTickets = reader.GetInt32(6),
                                ShowDateTime = DateTime.Parse(reader.GetString(7)),
                                MovieTitle = reader.GetString(8)
                            });
                        }
                    }
                }
            }
            return View(bookings);
        }

        // POST: Cancel a booking
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> CancelBooking(int bookingId)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null) return RedirectToAction("Login", "User");
            int userId = int.Parse(userIdClaim.Value);

            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new SqliteConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Check booking ownership and show time
                    string checkSql = @"
                        SELECT b.Status, s.ShowDateTime
                        FROM T_Bookings b
                        JOIN M_Shows s ON b.ShowId = s.ShowId
                        WHERE b.BookingId = @bookingId AND b.UserId = @userId";

                    using (var checkCmd = connection.CreateCommand())
                    {
                        checkCmd.CommandText = checkSql;
                        checkCmd.Parameters.AddWithValue("@bookingId", bookingId);
                        checkCmd.Parameters.AddWithValue("@userId", userId);

                        using (var reader = await checkCmd.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                            {
                                TempData["Error"] = "Booking not found.";
                                return RedirectToAction("MyBookings");
                            }

                            string status = reader.GetString(0);
                            DateTime showTime = DateTime.Parse(reader.GetString(1));

                            if (status == "CANCELLED")
                            {
                                TempData["Error"] = "Booking already cancelled.";
                                return RedirectToAction("MyBookings");
                            }

                            if ((showTime - DateTime.Now).TotalHours < 2)
                            {
                                TempData["Error"] = "Cannot cancel less than 2 hours before showtime.";
                                return RedirectToAction("MyBookings");
                            }
                        }
                    }

                    // Cancel booking
                    string updateSql = "UPDATE T_Bookings SET Status = 'CANCELLED' WHERE BookingId = @bookingId";
                    using (var updateCmd = connection.CreateCommand())
                    {
                        updateCmd.CommandText = updateSql;
                        updateCmd.Parameters.AddWithValue("@bookingId", bookingId);
                        await updateCmd.ExecuteNonQueryAsync();
                    }

                    TempData["Success"] = "Booking cancelled successfully!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling booking {BookingId}", bookingId);
                TempData["Error"] = "Error cancelling booking.";
            }

            return RedirectToAction("MyBookings");
        }

        [Authorize]
        public async Task<IActionResult> BookingConfirmation(int id)
        {
            var booking = new Booking();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (var connection = new SqliteConnection(connectionString))
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
                    WHERE b.BookingId = @bookingId";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    command.Parameters.AddWithValue("@bookingId", id);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            booking.BookingId = reader.GetInt32(0);
                            booking.BookingTime = DateTime.Parse(reader.GetString(1));
                            booking.Status = reader.GetString(2);
                            booking.FinalPrice = reader.GetDecimal(3);
                            booking.TotalTickets = reader.GetInt32(4);
                            booking.ShowDateTime = DateTime.Parse(reader.GetString(5));
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
