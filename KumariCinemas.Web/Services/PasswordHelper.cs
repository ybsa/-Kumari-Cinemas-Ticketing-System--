using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace KumariCinemas.Web.Services
{
    public static class PasswordHelper
    {
        // Use a secure random number generator to create a 128-bit salt
        public static string HashPassword(string password)
        {
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // derive a 256-bit subkey (use HMACSHA256 with 100,000 iterations)
            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 256 / 8));

            // Format: {iterations}.{salt}.{hash}
            return $"100000.{Convert.ToBase64String(salt)}.{hashed}";
        }

        public static bool VerifyPassword(string hash, string password)
        {
            try
            {
                var parts = hash.Split('.', 3);
                if (parts.Length != 3) return false;

                var iterations = Convert.ToInt32(parts[0]);
                var salt = Convert.FromBase64String(parts[1]);
                var storedHash = parts[2];

                var needsUpgrade = iterations != 100000;

                string newHash = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                    password: password,
                    salt: salt,
                    prf: KeyDerivationPrf.HMACSHA256,
                    iterationCount: iterations,
                    numBytesRequested: 256 / 8));

                return storedHash == newHash;
            }
            catch
            {
                return false;
            }
        }
    }
}
