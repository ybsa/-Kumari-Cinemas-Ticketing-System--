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
                
                // Hash the password before storing
                string passwordHash = Services.PasswordHelper.HashPassword(password);

                string sql = "INSERT INTO M_Users (Username, Password, Address) VALUES (:u, :p, :a)";
                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.Parameters.Add("u", username);
                    cmd.Parameters.Add("p", passwordHash); // Storing the hash
                    cmd.Parameters.Add("a", address);
                    try 
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch (OracleException ex) when (ex.Number == 1) // Unique constraint violation
                    {
                        ViewBag.Error = "Username already exists.";
                        return View();
                    }
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
                string sql = "SELECT UserId, Password FROM M_Users WHERE Username = :u"; 
                
                using (var command = new OracleCommand(sql, connection))
                {
                    command.Parameters.Add("u", username);
                    
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int userId = reader.GetInt32(0);
                            string storedHash = reader.GetString(1);

                            if (Services.PasswordHelper.VerifyPassword(storedHash, password))
                            {
                                // Login Success
                                // In a real app, set cookies/claims here. 
                                return RedirectToAction("Index", "Ticket");
                            }
                        }
                    }
                }
            }

            ViewBag.Error = "Invalid username or password";
            return View();
        }
    }
}
