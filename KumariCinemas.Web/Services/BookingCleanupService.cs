using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace KumariCinemas.Web.Services
{
    public class BookingCleanupService : BackgroundService
    {
        private readonly ILogger<BookingCleanupService> _logger;
        private readonly IConfiguration _configuration;

        public BookingCleanupService(ILogger<BookingCleanupService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Booking Cleanup Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Checking for expired bookings...");

                try
                {
                    await CleanupExpiredBookings();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while cleaning up bookings.");
                }

                // Run every 15 minutes
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }

        private async Task CleanupExpiredBookings()
        {
            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();

                // Logic: Find Bookings where status is 'BOOKED' (not PAID) 
                // and the show start time is less than 1 hour away.
                string sql = @"
                    UPDATE T_Bookings b
                    SET b.Status = 'CANCELLED'
                    WHERE b.Status = 'BOOKED'
                    AND EXISTS (
                        SELECT 1 FROM M_Shows s 
                        WHERE s.ShowId = b.ShowId 
                        AND s.ShowDateTime < (SYSDATE + 1/24) -- Cancel 1 hr before show
                    )";

                using (var command = new OracleCommand(sql, connection))
                {
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected > 0)
                    {
                        _logger.LogInformation($"{rowsAffected} bookings were automatically cancelled.");
                    }
                }
            }
        }
    }
}
