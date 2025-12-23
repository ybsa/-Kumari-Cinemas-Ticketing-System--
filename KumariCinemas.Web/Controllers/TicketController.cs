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
                string connectionString = _configuration.GetConnectionString("OracleDb");
                using (var connection = new OracleConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // 1. Get Capacity
                    string capSql = @"
                        SELECT h.Capacity 
                        FROM M_Shows s
                        JOIN M_Halls h ON s.HallId = h.HallId
                        WHERE s.ShowId = :showId";
                    
                    using (var cmd = new OracleCommand(capSql, connection))
                    {
                        cmd.Parameters.Add("showId", showId);
                        object result = await cmd.ExecuteScalarAsync();
                        if (result != null) capacity = Convert.ToInt32(result);
                    }

                    // 2. Get Booked Seats
                    string seatSql = @"
                        SELECT t.SeatNumber 
                        FROM T_Tickets t
                        JOIN T_Bookings b ON t.BookingId = b.BookingId
                        WHERE b.ShowId = :showId AND b.Status IN ('BOOKED', 'PAID')";

                    using (var cmd = new OracleCommand(seatSql, connection))
                    {
                        cmd.Parameters.Add("showId", showId);
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
                string connectionString = _configuration.GetConnectionString("OracleDb");

            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                
                // Build dynamic SQL with filters
                string sql = @"
                    SELECT s.ShowId, s.MovieId, s.HallId, s.ShowDateTime, s.BasePrice, 
                           m.Title, m.ReleaseDate, m.Genre, m.ImageUrl 
                    FROM M_Shows s 
                    JOIN M_Movies m ON s.MovieId = m.MovieId
                    WHERE 1=1";

                var parameters = new List<OracleParameter>();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    sql += " AND UPPER(m.Title) LIKE UPPER(:search)";
                    parameters.Add(new OracleParameter("search", OracleDbType.Varchar2) { Value = $"%{search}%" });
                }

                if (!string.IsNullOrWhiteSpace(genre))
                {
                    sql += " AND m.Genre = :genre";
                    parameters.Add(new OracleParameter("genre", OracleDbType.Varchar2) { Value = genre });
                }

                if (date.HasValue)
                {
                    sql += " AND TRUNC(s.ShowDateTime) = :showDate";
                    parameters.Add(new OracleParameter("showDate", OracleDbType.Date) { Value = date.Value.Date });
                }

                using (var command = new OracleCommand(sql, connection))
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.Add(param);
                    }
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
                            string seatCheckSql = @"
                                SELECT COUNT(*) 
                                FROM T_Tickets t
                                JOIN T_Bookings b ON t.BookingId = b.BookingId
                                WHERE b.ShowId = :showId 
                                  AND b.Status IN ('BOOKED', 'PAID')
                                  AND t.SeatNumber IN (" + string.Join(",", selectedSeats.Select((s, i) => $":seat{i}")) + @")";

                            using (var seatCheckCmd = new OracleCommand(seatCheckSql, connection))
                            {
                                seatCheckCmd.Transaction = transaction;
                                seatCheckCmd.Parameters.Add(new OracleParameter("showId", OracleDbType.Int32) { Value = showId });
                                for (int i = 0; i < selectedSeats.Count; i++)
                                {
                                    seatCheckCmd.Parameters.Add(new OracleParameter($"seat{i}", OracleDbType.Varchar2) { Value = selectedSeats[i] });
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

                        // 5. Insert selected seats into T_Tickets
                        if (selectedSeats.Any())
                        {
                            foreach (var seat in selectedSeats)
                            {
                                string ticketSql = "INSERT INTO T_Tickets (BookingId, SeatNumber) VALUES (:bookingId, :seatNumber)";
                                using (var ticketCmd = new OracleCommand(ticketSql, connection))
                                {
                                    ticketCmd.Transaction = transaction;
                                    ticketCmd.Parameters.Add(new OracleParameter("bookingId", OracleDbType.Int32) { Value = bookingId });
                                    ticketCmd.Parameters.Add(new OracleParameter("seatNumber", OracleDbType.Varchar2) { Value = seat });
                                    await ticketCmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        transaction.Commit();
                        
                        // Redirect to payment page instead of confirmation
                        return RedirectToAction("Index", "Payment", new { bookingId });
                    }
                    catch (OracleException ex)
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

        // POST: Cancel a booking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int bookingId)
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

                    // Check booking ownership and show time
                    string checkSql = @"
                        SELECT b.Status, s.ShowDateTime
                        FROM T_Bookings b
                        JOIN M_Shows s ON b.ShowId = s.ShowId
                        WHERE b.BookingId = :bookingId AND b.UserId = :userId";

                    using (var checkCmd = new OracleCommand(checkSql, connection))
                    {
                        checkCmd.Parameters.Add(new OracleParameter("bookingId", OracleDbType.Int32) { Value = bookingId });
                        checkCmd.Parameters.Add(new OracleParameter("userId", OracleDbType.Int32) { Value = userId });

                        using (var reader = await checkCmd.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                            {
                                TempData["Error"] = "Booking not found.";
                                return RedirectToAction("MyBookings");
                            }

                            string status = reader.GetString(0);
                            DateTime showTime = reader.GetDateTime(1);

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
                    string updateSql = "UPDATE T_Bookings SET Status = 'CANCELLED' WHERE BookingId = :bookingId";
                    using (var updateCmd = new OracleCommand(updateSql, connection))
                    {
                        updateCmd.Parameters.Add(new OracleParameter("bookingId", OracleDbType.Int32) { Value = bookingId });
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
