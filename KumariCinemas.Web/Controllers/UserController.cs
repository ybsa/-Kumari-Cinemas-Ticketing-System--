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

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null) return RedirectToAction("Login");
            int userId = int.Parse(userIdClaim.Value);

            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT Username, Address FROM M_Users WHERE UserId = :userId";
                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.Parameters.Add("userId", userId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            ViewBag.Username = reader.GetString(0);
                            ViewBag.Address = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        }
                    }
                }
            }
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string address, string currentPassword, string newPassword)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim == null) return RedirectToAction("Login");
            int userId = int.Parse(userIdClaim.Value);

            string connectionString = _configuration.GetConnectionString("OracleDb");
            using (var connection = new OracleConnection(connectionString))
            {
                await connection.OpenAsync();

                // Update Address
                if (address != null) // Allow empty string
                {
                    string updateSql = "UPDATE M_Users SET Address = :address WHERE UserId = :userId";
                    using (var cmd = new OracleCommand(updateSql, connection))
                    {
                        cmd.Parameters.Add("address", address);
                        cmd.Parameters.Add("userId", userId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                // Update Password if provided
                if (!string.IsNullOrEmpty(currentPassword) && !string.IsNullOrEmpty(newPassword))
                {
                    // Verify current password
                    // Note: In real app, re-hash and check. Here we assume simple hashing from Login.
                    // For brevity, we are using the Helper.
                    string currentHash = KumariCinemas.Web.Services.PasswordHelper.HashPassword(currentPassword);

                    string checkSql = "SELECT COUNT(*) FROM M_Users WHERE UserId = :userId AND Password = :password";
                    using (var cmd = new OracleCommand(checkSql, connection))
                    {
                        cmd.Parameters.Add("userId", userId);
                        cmd.Parameters.Add("password", currentHash);
                        int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                        if (count > 0)
                        {
                            string newHash = KumariCinemas.Web.Services.PasswordHelper.HashPassword(newPassword);
                            string passSql = "UPDATE M_Users SET Password = :password WHERE UserId = :userId";
                            using (var updateCmd = new OracleCommand(passSql, connection))
                            {
                                updateCmd.Parameters.Add("password", newHash);
                                updateCmd.Parameters.Add("userId", userId);
                                await updateCmd.ExecuteNonQueryAsync();
                            }
                            TempData["Success"] = "Profile and Password updated successfully!";
                        }
                        else
                        {
                            TempData["Error"] = "Current password incorrect.";
                        }
                    }
                }
                else
                {
                    TempData["Success"] = "Profile updated successfully!";
                }
            }

            return RedirectToAction("Profile");
        }
    }
}
