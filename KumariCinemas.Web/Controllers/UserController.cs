using Microsoft.AspNetCore.Mvc;
using KumariCinemas.Web.Models;
using Oracle.ManagedDataAccess.Client;

namespace KumariCinemas.Web.Controllers
{
    public class UserController : Controller
    {
        private readonly IConfiguration _configuration;

        public UserController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(string username, string password, string address)
        {
            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = "INSERT INTO M_Users (Username, Password, Address) VALUES (:u, :p, :a)";
                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.Parameters.Add("u", username);
                    cmd.Parameters.Add("p", password); // Should be hashed in production
                    cmd.Parameters.Add("a", address);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT UserId FROM M_Users WHERE Username = :u AND Password = :p"; // In production, use hashing!
                
                using (var command = new OracleCommand(sql, connection))
                {
                    command.Parameters.Add("u", username);
                    command.Parameters.Add("p", password);
                    
                    object result = await command.ExecuteScalarAsync();
                    if (result != null)
                    {
                        // Login Success
                        // In a real app, set cookies/claims here. 
                        // For prototype, we'll just redirect.
                        return RedirectToAction("Index", "Ticket");
                    }
                }
            }

            ViewBag.Error = "Invalid username or password";
            return View();
        }
    }
}
