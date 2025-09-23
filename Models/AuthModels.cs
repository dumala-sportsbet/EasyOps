namespace EasyOps.Models
{
    public class AuthRequest
    {
        public string Username { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class UserCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
    }
}
