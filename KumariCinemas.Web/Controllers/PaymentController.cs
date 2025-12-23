using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Oracle.ManagedDataAccess.Client;

namespace KumariCinemas.Web.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly IConfiguration _configuration;

        public PaymentController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // GET: Payment page for a booking
        [HttpGet]
        public async Task<IActionResult> Index(int bookingId)
        {
            // Verify booking exists and belongs to user
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null) return RedirectToAction("Login", "User");
            int userId = int.Parse(userIdClaim.Value);

            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = @"
                    SELECT b.BookingId, b.FinalPrice, b.Status, m.Title, s.ShowDateTime
                    FROM T_Bookings b
                    JOIN M_Shows s ON b.ShowId = s.ShowId
                    JOIN M_Movies m ON s.MovieId = m.MovieId
                    WHERE b.BookingId = :bookingId AND b.UserId = :userId";

                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.Parameters.Add(new OracleParameter("bookingId", OracleDbType.Int32) { Value = bookingId });
                    cmd.Parameters.Add(new OracleParameter("userId", OracleDbType.Int32) { Value = userId });
                    
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var status = reader.GetString(2);
                            
                            // If already paid, redirect to confirmation
                            if (status == "PAID")
                            {
                                return RedirectToAction("BookingConfirmation", "Ticket", new { id = bookingId });
                            }

                            ViewBag.BookingId = reader.GetInt32(0);
                            ViewBag.Amount = reader.GetDecimal(1);
                            ViewBag.MovieTitle = reader.GetString(3);
                            ViewBag.ShowDateTime = reader.GetDateTime(4);
                            
                            return View();
                        }
                    }
                }
            }

            // Booking not found or doesn't belong to user
            TempData["Error"] = "Booking not found.";
            return RedirectToAction("MyBookings", "Ticket");
        }

        // POST: Process payment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int bookingId, string cardNumber, string cardName, string expiryDate, string cvv)
        {
            // Verify user owns booking
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null) return RedirectToAction("Login", "User");
            int userId = int.Parse(userIdClaim.Value);

            // Basic validation
            if (string.IsNullOrWhiteSpace(cardNumber) || cardNumber.Length < 13)
            {
                TempData["Error"] = "Invalid card number.";
                return RedirectToAction("Index", new { bookingId });
            }

            if (string.IsNullOrWhiteSpace(cvv) || cvv.Length != 3)
            {
                TempData["Error"] = "Invalid CVV.";
                return RedirectToAction("Index", new { bookingId });
            }

            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();

                // Verify booking belongs to user and is in BOOKED status
                string checkSql = "SELECT Status FROM T_Bookings WHERE BookingId = :bookingId AND UserId = :userId";
                using (var checkCmd = new OracleCommand(checkSql, connection))
                {
                    checkCmd.Parameters.Add(new OracleParameter("bookingId", OracleDbType.Int32) { Value = bookingId });
                    checkCmd.Parameters.Add(new OracleParameter("userId", OracleDbType.Int32) { Value = userId });
                    
                    var status = await checkCmd.ExecuteScalarAsync();
                    if (status == null)
                    {
                        TempData["Error"] = "Booking not found.";
                        return RedirectToAction("MyBookings", "Ticket");
                    }

                    if (status.ToString() == "PAID")
                    {
                        TempData["Success"] = "This booking is already paid.";
                        return RedirectToAction("BookingConfirmation", "Ticket", new { id = bookingId });
                    }

                    if (status.ToString() == "CANCELLED")
                    {
                        TempData["Error"] = "This booking has been cancelled.";
                        return RedirectToAction("MyBookings", "Ticket");
                    }
                }

                // MOCK PAYMENT PROCESSING
                // In production, this would integrate with payment gateway (Stripe, PayPal, etc.)
                await Task.Delay(1000); // Simulate API call

                // Update booking status to PAID
                string updateSql = "UPDATE T_Bookings SET Status = 'PAID' WHERE BookingId = :bookingId";
                using (var updateCmd = new OracleCommand(updateSql, connection))
                {
                    updateCmd.Parameters.Add(new OracleParameter("bookingId", OracleDbType.Int32) { Value = bookingId });
                    await updateCmd.ExecuteNonQueryAsync();
                }

                TempData["Success"] = "Payment successful! Your booking is confirmed.";
                return RedirectToAction("BookingConfirmation", "Ticket", new { id = bookingId });
            }
        }
    }
}
