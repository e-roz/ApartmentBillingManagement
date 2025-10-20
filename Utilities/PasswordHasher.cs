namespace Apartment.Utilities
{
    public class PasswordHasher
    {

        public static string HashPassword(string password)
        {
            // Using BCrypt to hash the password
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            // Using BCrypt to verify the password against the hashed password
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}
