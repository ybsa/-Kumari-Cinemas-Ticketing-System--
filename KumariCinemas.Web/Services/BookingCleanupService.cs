using System.Data;


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
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString))
            {
                await connection.OpenAsync();

                // Logic: Find Bookings where status is 'BOOKED' (not PAID) 
                // and the show start time is less than 1 hour away.
                // SQLite: datetime('now') is in UTC usually, but we are using local time in seed. 
                // Assuming consistency: check if ShowDateTime < current_time + 1 hour
                string sql = @"
                    UPDATE T_Bookings
                    SET Status = 'CANCELLED'
                    WHERE Status = 'BOOKED'
                    AND EXISTS (
                        SELECT 1 FROM M_Shows s 
                        WHERE s.ShowId = T_Bookings.ShowId 
                        AND s.ShowDateTime < datetime('now', '+1 hour')
                    )";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
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
