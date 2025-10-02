using EasyOps.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace EasyOps.Services
{
    public interface IAuthenticationService
    {
        Task<AuthResponse> ValidateCredentialsAsync(string username, string apiToken);
        Task<AuthResponse> LoginWithPasswordAsync(string username, string password);
        UserCredentials? GetCurrentUserCredentials(HttpContext httpContext);
        void SetUserCredentials(HttpContext httpContext, string username, string apiToken);
        void ClearUserCredentials(HttpContext httpContext);
        bool IsAuthenticated(HttpContext httpContext);
    }

    public class AuthenticationService : IAuthenticationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly JenkinsConfiguration _jenkinsConfig;
        private const string USERNAME_SESSION_KEY = "JenkinsUsername";
        private const string API_TOKEN_SESSION_KEY = "JenkinsApiToken";

        public AuthenticationService(IHttpClientFactory httpClientFactory, IOptions<JenkinsConfiguration> jenkinsConfig)
        {
            _httpClientFactory = httpClientFactory;
            _jenkinsConfig = jenkinsConfig.Value;
        }

        public async Task<AuthResponse> ValidateCredentialsAsync(string username, string apiToken)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                
                // Create basic auth header
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{apiToken}"));
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

                // Test credentials by making a simple API call to Jenkins
                var response = await httpClient.GetAsync($"{_jenkinsConfig.BaseUrl}/api/json");

                if (response.IsSuccessStatusCode)
                {
                    return new AuthResponse { Success = true, Message = "Authentication successful" };
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return new AuthResponse { Success = false, Message = "Invalid username or API token" };
                }
                else
                {
                    return new AuthResponse { Success = false, Message = $"Jenkins API error: {response.StatusCode}" };
                }
            }
            catch (Exception ex)
            {
                return new AuthResponse { Success = false, Message = $"Connection error: {ex.Message}" };
            }
        }

        public async Task<AuthResponse> LoginWithPasswordAsync(string username, string password)
        {
            try
            {
                // Step 1: Validate username/password credentials
                var validationResult = await ValidatePasswordCredentials(username, password);
                if (!validationResult.Success)
                {
                    return validationResult;
                }

                // Step 2: Generate API token
                var tokenResult = await GenerateJenkinsApiToken(username, password);
                if (!tokenResult.Success)
                {
                    return tokenResult;
                }

                return new AuthResponse 
                { 
                    Success = true, 
                    Message = "Login successful", 
                    ApiToken = tokenResult.ApiToken 
                };
            }
            catch (Exception ex)
            {
                return new AuthResponse { Success = false, Message = $"Login error: {ex.Message}" };
            }
        }

        private async Task<AuthResponse> ValidatePasswordCredentials(string username, string password)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                
                // Create basic auth header with username/password
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

                // Test credentials by making a simple API call to Jenkins
                var response = await httpClient.GetAsync($"{_jenkinsConfig.BaseUrl}/api/json");

                if (response.IsSuccessStatusCode)
                {
                    return new AuthResponse { Success = true, Message = "Credentials validated" };
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return new AuthResponse { Success = false, Message = "Invalid username or password" };
                }
                else
                {
                    return new AuthResponse { Success = false, Message = $"Jenkins API error: {response.StatusCode}" };
                }
            }
            catch (Exception ex)
            {
                return new AuthResponse { Success = false, Message = $"Connection error: {ex.Message}" };
            }
        }

        private async Task<AuthResponse> GenerateJenkinsApiToken(string username, string password)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                
                // Create basic auth header with username/password
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

                // Generate new API token
                var tokenUrl = $"{_jenkinsConfig.BaseUrl}/user/{username}/descriptorByName/jenkins.security.ApiTokenProperty/generateNewToken";
                var response = await httpClient.PostAsync(tokenUrl, null);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    
                    // Parse the JSON response to extract the token
                    // Jenkins returns: {"tokenName":"...", "tokenValue":"...", "tokenUuid":"..."}
                    var tokenData = JsonSerializer.Deserialize<JenkinsTokenResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (tokenData?.TokenValue != null)
                    {
                        return new AuthResponse 
                        { 
                            Success = true, 
                            Message = "Token generated successfully",
                            ApiToken = tokenData.TokenValue
                        };
                    }
                    else
                    {
                        return new AuthResponse { Success = false, Message = "Failed to extract token from response" };
                    }
                }
                else
                {
                    return new AuthResponse { Success = false, Message = $"Token generation failed: {response.StatusCode}" };
                }
            }
            catch (Exception ex)
            {
                return new AuthResponse { Success = false, Message = $"Token generation error: {ex.Message}" };
            }
        }

        public UserCredentials? GetCurrentUserCredentials(HttpContext httpContext)
        {
            var username = httpContext.Session.GetString(USERNAME_SESSION_KEY);
            var apiToken = httpContext.Session.GetString(API_TOKEN_SESSION_KEY);

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(apiToken))
            {
                return null;
            }

            return new UserCredentials { Username = username, ApiToken = apiToken };
        }

        public void SetUserCredentials(HttpContext httpContext, string username, string apiToken)
        {
            httpContext.Session.SetString(USERNAME_SESSION_KEY, username);
            httpContext.Session.SetString(API_TOKEN_SESSION_KEY, apiToken);
        }

        public void ClearUserCredentials(HttpContext httpContext)
        {
            httpContext.Session.Remove(USERNAME_SESSION_KEY);
            httpContext.Session.Remove(API_TOKEN_SESSION_KEY);
        }

        public bool IsAuthenticated(HttpContext httpContext)
        {
            return GetCurrentUserCredentials(httpContext) != null;
        }
    }
}
