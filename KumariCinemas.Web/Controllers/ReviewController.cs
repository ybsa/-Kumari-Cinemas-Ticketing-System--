using Microsoft.AspNetCore.Mvc;
using KumariCinemas.Web.Models;
using Oracle.ManagedDataAccess.Client;

namespace KumariCinemas.Web.Controllers
{
    public class ReviewController : Controller
    {
        private readonly IConfiguration _configuration;

        public ReviewController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int movieId, int userId, int rating, string comment)
        {
            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = @"
                    INSERT INTO T_Reviews (MovieId, UserId, Rating, CommentText) 
                    VALUES (:mid, :uid, :r, :c)";

                using (var command = new OracleCommand(sql, connection))
                {
                    command.Parameters.Add("mid", movieId);
                    command.Parameters.Add("uid", userId);
                    command.Parameters.Add("r", rating);
                    command.Parameters.Add("c", comment);
                    await command.ExecuteNonQueryAsync();
                }
            }

            return RedirectToAction("Index", "Ticket"); // For now redirect to home
        }
    }
}
