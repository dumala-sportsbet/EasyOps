using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using EasyOps.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace EasyOps.Services
{
    public interface IAwsAuthenticationService
    {
        Task<AwsCredentialStatus> CheckCredentialStatusAsync(string? awsProfile = null);
        Task<string> GetManualLoginInstructionsAsync(string? environmentName = null);
        Task<bool> SwitchEnvironmentAsync(string environmentName);
        bool AreCredentialsValid();
        string GetCurrentProfile();
        string GetCurrentRegion();
        List<AwsEnvironmentConfiguration> GetAvailableEnvironments();
        AwsEnvironmentConfiguration? GetCurrentEnvironment();
        Task InitializeAsync();
    }

    public class AwsAuthenticationService : IAwsAuthenticationService
    {
        private readonly AwsConfiguration _awsConfig;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<AwsAuthenticationService> _logger;
        private readonly IDatabaseService _databaseService;
        private AwsCredentialStatus? _lastStatus;
        private DateTime _lastCheck = DateTime.MinValue;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5);
        private string _currentProfile = "";
        private AwsEnvironmentConfiguration? _currentEnvironment;

        public AwsAuthenticationService(IOptions<AwsConfiguration> awsConfig, IWebHostEnvironment webHostEnvironment, ILogger<AwsAuthenticationService> logger, IDatabaseService databaseService)
        {
            _awsConfig = awsConfig.Value;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
            _databaseService = databaseService;
        }

        public async Task<AwsCredentialStatus> CheckCredentialStatusAsync(string? awsProfile = null)
        {
            string profileToUse = awsProfile ?? _currentProfile;
            
            // Return cached status if recent and for same profile
            if (_lastStatus != null && DateTime.UtcNow - _lastCheck < _cacheTimeout && _lastStatus.Profile == profileToUse)
            {
                return _lastStatus;
            }

            var status = new AwsCredentialStatus();

            try
            {
                _logger.LogInformation("Checking AWS credentials for profile: {Profile}", profileToUse);
                
                // In development, skip actual AWS validation and return mock valid status
                if (_webHostEnvironment.IsDevelopment())
                {
                    _logger.LogInformation("Development mode: Skipping AWS credential validation");
                    status.IsValid = true;
                    status.AccountId = "123456789012"; // Mock account ID
                    status.UserArn = $"arn:aws:iam::123456789012:user/{profileToUse}-dev-user";
                    status.Region = _awsConfig.Region;
                    status.Profile = profileToUse;
                    
                    var environments = await _databaseService.GetEnvironmentsAsync();
                    var environment = environments.FirstOrDefault(e => e.AwsProfile == profileToUse);
                    if (environment != null)
                    {
                        status.Environment = environment.EnvironmentType;
                        status.EnvironmentName = environment.Name;
                    }

                    // Set expiration to 1 hour from now
                    status.ExpiresAt = DateTime.UtcNow.AddHours(1);
                }
                else
                {
                    // Try to call STS to validate credentials with specific profile
                    var awsCredentialsProvider = CreateCredentialsProvider(profileToUse);
                    using var stsClient = new AmazonSecurityTokenServiceClient(awsCredentialsProvider, Amazon.RegionEndpoint.GetBySystemName(_awsConfig.Region));
                    var request = new GetCallerIdentityRequest();
                    var response = await stsClient.GetCallerIdentityAsync(request);

                    status.IsValid = true;
                    status.AccountId = response.Account;
                    status.UserArn = response.Arn;
                    status.Region = _awsConfig.Region;
                    status.Profile = profileToUse;
                    
                    var environments = await _databaseService.GetEnvironmentsAsync();
                    var environment = environments.FirstOrDefault(e => e.AwsProfile == profileToUse);
                    if (environment != null)
                    {
                        status.Environment = environment.EnvironmentType;
                        status.EnvironmentName = environment.Name;
                    }

                    // Estimate expiration time (SAML2AWS sessions typically last based on config)
                    status.ExpiresAt = DateTime.UtcNow.AddMinutes(_awsConfig.CredentialTimeoutMinutes);

                    _logger.LogInformation("AWS credentials validated successfully. Account: {Account}, User: {User}", 
                        response.Account, response.Arn);
                }
            }
            catch (Exception ex)
            {
                status.IsValid = false;
                status.ErrorMessage = $"AWS credential validation failed: {ex.Message}";
                _logger.LogWarning("AWS credential validation failed: {Error}", ex.Message);
            }

            _lastStatus = status;
            _lastCheck = DateTime.UtcNow;
            return status;
        }

        public async Task<string> GetManualLoginInstructionsAsync(string? environmentName = null)
        {
            AwsEnvironmentConfiguration? environment;
            if (string.IsNullOrEmpty(environmentName))
            {
                environment = _currentEnvironment;
            }
            else
            {
                var environments = await _databaseService.GetEnvironmentsAsync();
                var env = environments.FirstOrDefault(e => e.Name == environmentName);
                environment = env != null ? new AwsEnvironmentConfiguration
                {
                    Name = env.Name,
                    Environment = env.EnvironmentType,
                    AwsProfile = env.AwsProfile,
                    AccountId = env.AccountId,
                    SamlRole = env.SamlRole,
                    Description = env.Description,
                    IsDefault = env.IsDefault
                } : null;
            }

            if (environment == null)
            {
                return "Environment not found. Please check your configuration.";
            }

            var instructions = new StringBuilder();
            instructions.AppendLine("🔐 **AWS Authentication Required**");
            instructions.AppendLine();
            instructions.AppendLine($"Please run the following command in your terminal to authenticate with AWS:");
            instructions.AppendLine();
            instructions.AppendLine("```bash");
            instructions.AppendLine($"saml2aws login --profile={environment.AwsProfile}");
            instructions.AppendLine("```");
            instructions.AppendLine();
            instructions.AppendLine("**Step-by-step:**");
            instructions.AppendLine("1. Open your terminal/command prompt");
            instructions.AppendLine($"2. Run: `saml2aws login --profile={environment.AwsProfile}`");
            instructions.AppendLine("3. Follow the Okta authentication prompts");
            instructions.AppendLine("4. Approve the push notification on your mobile device");
            instructions.AppendLine("5. Return to this application and refresh the page");
            instructions.AppendLine();
            instructions.AppendLine("**Environment Details:**");
            instructions.AppendLine($"- **Environment**: {environment.Name}");
            instructions.AppendLine($"- **AWS Profile**: {environment.AwsProfile}");
            instructions.AppendLine($"- **Account ID**: {environment.AccountId}");
            instructions.AppendLine($"- **Role**: {environment.SamlRole}");
            instructions.AppendLine();
            instructions.AppendLine("**Need help with SAML2AWS setup?**");
            instructions.AppendLine("Run the setup script: `./setup-okta-saml2aws.ps1`");

            return instructions.ToString();
        }

        public bool AreCredentialsValid()
        {
            if (_lastStatus == null || DateTime.UtcNow - _lastCheck > _cacheTimeout)
            {
                // Trigger async check but return best guess
                _ = Task.Run(async () => await CheckCredentialStatusAsync());
                return _lastStatus?.IsValid ?? false;
            }

            return _lastStatus.IsValid;
        }

        public string GetCurrentProfile()
        {
            return _currentProfile ?? System.Environment.GetEnvironmentVariable("AWS_PROFILE") ?? "default";
        }

        public string GetCurrentRegion()
        {
            return _lastStatus?.Region ?? _awsConfig.Region;
        }

        private Amazon.Runtime.AWSCredentials CreateCredentialsProvider(string profileName)
        {
            // Use profile-based credentials
            var credentialProfileStoreChain = new Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain();
            if (credentialProfileStoreChain.TryGetAWSCredentials(profileName, out var credentials))
            {
                return credentials;
            }
            
            // Fallback to anonymous credentials (will fail auth, but won't crash)
            return new Amazon.Runtime.AnonymousAWSCredentials();
        }

        public async Task<bool> SwitchEnvironmentAsync(string environmentName)
        {
            var environments = await _databaseService.GetEnvironmentsAsync();
            var environment = environments.FirstOrDefault(e => e.Name == environmentName);
            if (environment == null)
            {
                _logger.LogError("Environment not found: {EnvironmentName}", environmentName);
                return false;
            }

            _currentEnvironment = new AwsEnvironmentConfiguration
            {
                Name = environment.Name,
                Environment = environment.EnvironmentType,
                AwsProfile = environment.AwsProfile,
                AccountId = environment.AccountId,
                SamlRole = environment.SamlRole,
                Description = environment.Description,
                IsDefault = environment.IsDefault
            };
            _currentProfile = environment.AwsProfile;
            
            // Clear cache to force recheck with new profile
            _lastStatus = null;
            _lastCheck = DateTime.MinValue;
            
            _logger.LogInformation("Switched to environment: {EnvironmentName} (Profile: {Profile})", 
                environmentName, _currentProfile);
                
            return true;
        }

        public async Task InitializeAsync()
        {
            try
            {
                var environments = await _databaseService.GetEnvironmentsAsync();
                var defaultEnv = environments.FirstOrDefault(e => e.IsDefault) ?? environments.FirstOrDefault();

                if (defaultEnv != null)
                {
                    _currentEnvironment = new AwsEnvironmentConfiguration
                    {
                        Name = defaultEnv.Name,
                        Environment = defaultEnv.EnvironmentType,
                        AwsProfile = defaultEnv.AwsProfile,
                        AccountId = defaultEnv.AccountId,
                        SamlRole = defaultEnv.SamlRole,
                        Description = defaultEnv.Description,
                        IsDefault = defaultEnv.IsDefault
                    };
                    _currentProfile = _currentEnvironment.AwsProfile;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing AWS authentication service");
            }
        }

        public List<AwsEnvironmentConfiguration> GetAvailableEnvironments()
        {
            try
            {
                var environments = _databaseService.GetEnvironmentsAsync().GetAwaiter().GetResult();
                return environments.Select(e => new AwsEnvironmentConfiguration
                {
                    Name = e.Name,
                    Environment = e.EnvironmentType,
                    AwsProfile = e.AwsProfile,
                    AccountId = e.AccountId,
                    SamlRole = e.SamlRole,
                    Description = e.Description,
                    IsDefault = e.IsDefault
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available environments");
                return new List<AwsEnvironmentConfiguration>();
            }
        }

        public AwsEnvironmentConfiguration? GetCurrentEnvironment()
        {
            return _currentEnvironment;
        }
    }
}
