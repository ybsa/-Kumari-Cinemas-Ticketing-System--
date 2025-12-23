using Microsoft.AspNetCore.Mvc;
using KumariCinemas.Web.Models;
using Oracle.ManagedDataAccess.Client;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace KumariCinemas.Web.Controllers
{
    public class UserController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserController> _logger;

        public UserController(IConfiguration configuration, ILogger<UserController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string username, string password, string address)
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            {
                ViewBag.Error = "Username must be at least 3 characters long.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                ViewBag.Error = "Password must be at least 6 characters long.";
                return View();
            }

            try
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
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Database error during registration for user: {Username}", username);
                ViewBag.Error = "An error occurred during registration. Please try again.";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during registration");
                ViewBag.Error = "An unexpected error occurred. Please try again later.";
                return View();
            }

            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Username and password are required.";
                return View();
            }

            try
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
                                    // Login Success - Set Secure Cookie
                                    var claims = new List<Claim>
                                    {
                                        new Claim(ClaimTypes.Name, username),
                                        new Claim("UserId", userId.ToString())
                                    };

                                    var claimsIdentity = new ClaimsIdentity(claims, "CookieAuth");
                                    var authProperties = new AuthenticationProperties
                                    {
                                        IsPersistent = true,
                                        ExpiresUtc = DateTime.UtcNow.AddHours(2)
                                    };

                                    await HttpContext.SignInAsync("CookieAuth", new ClaimsPrincipal(claimsIdentity), authProperties);
                                    return RedirectToAction("Index", "Ticket");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user: {Username}", username);
                ViewBag.Error = "An error occurred during login. Please try again.";
                return View();
            }

            ViewBag.Error = "Invalid username or password";
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("CookieAuth");
            return RedirectToAction("Login");
        }
    }
}
