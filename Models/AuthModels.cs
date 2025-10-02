namespace EasyOps.Models
{
    public class AuthRequest
    {
        public string Username { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
    }

    public class PasswordAuthRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ApiToken { get; set; }
    }

    public class UserCredentials
    {
        public string Username { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
    }

    public class JenkinsTokenResponse
    {
        public string? TokenName { get; set; }
        public string? TokenValue { get; set; }
        public string? TokenUuid { get; set; }
    }
}
