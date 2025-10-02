using EasyOps.Models;
using EasyOps.Services;
using Microsoft.AspNetCore.Mvc;

namespace EasyOps.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AwsController : ControllerBase
    {
        private readonly IAwsAuthenticationService _awsAuthService;
        private readonly IAwsEcsService _awsEcsService;
        private readonly ILogger<AwsController> _logger;

        public AwsController(
            IAwsAuthenticationService awsAuthService,
            IAwsEcsService awsEcsService,
            ILogger<AwsController> logger)
        {
            _awsAuthService = awsAuthService;
            _awsEcsService = awsEcsService;
            _logger = logger;
        }

        [HttpGet("auth/status")]
        public async Task<IActionResult> GetAuthStatus()
        {
            try
            {
                var status = await _awsAuthService.CheckCredentialStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking AWS authentication status");
                return StatusCode(500, new { error = "Failed to check AWS authentication status" });
            }
        }

        [HttpPost("auth/manual-instructions")]
        public async Task<IActionResult> GetManualLoginInstructions([FromBody] GetLoginInstructionsRequest? request = null)
        {
            try
            {
                var instructions = await _awsAuthService.GetManualLoginInstructionsAsync(request?.EnvironmentName);
                return Ok(new { success = true, instructions = instructions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting manual login instructions");
                return StatusCode(500, new { success = false, error = "Failed to get login instructions" });
            }
        }

        [HttpGet("environments")]
        public IActionResult GetAvailableEnvironments()
        {
            try
            {
                var environments = _awsAuthService.GetAvailableEnvironments();
                return Ok(environments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available environments");
                return StatusCode(500, new { error = "Failed to get available environments" });
            }
        }

        [HttpGet("environments/current")]
        public IActionResult GetCurrentEnvironment()
        {
            try
            {
                var currentEnv = _awsAuthService.GetCurrentEnvironment();
                return Ok(currentEnv);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current environment");
                return StatusCode(500, new { error = "Failed to get current environment" });
            }
        }

        [HttpPost("environments/switch")]
        public async Task<IActionResult> SwitchEnvironment([FromBody] SwitchEnvironmentRequest request)
        {
            try
            {
                var success = await _awsAuthService.SwitchEnvironmentAsync(request.EnvironmentName);
                if (success)
                {
                    var newStatus = await _awsAuthService.CheckCredentialStatusAsync();
                    return Ok(new { success = true, status = newStatus });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to switch environment" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching environment to {EnvironmentName}", request.EnvironmentName);
                return StatusCode(500, new { error = "Failed to switch environment" });
            }
        }

        [HttpGet("clusters")]
        public async Task<IActionResult> GetClusters()
        {
            try
            {
                var clusters = await _awsEcsService.GetAvailableClustersAsync();
                return Ok(clusters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching available clusters");
                return StatusCode(500, new { error = "Failed to fetch available clusters" });
            }
        }

        [HttpGet("clusters/{clusterName}/services")]
        public async Task<IActionResult> GetClusterServices(string clusterName)
        {
            try
            {
                // Check authentication first
                var authStatus = await _awsAuthService.CheckCredentialStatusAsync();
                if (!authStatus.IsValid)
                {
                    return Unauthorized(new { error = authStatus.ErrorMessage });
                }

                var services = await _awsEcsService.GetClusterServicesAsync(clusterName);
                return Ok(services);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching services for cluster {ClusterName}", clusterName);
                return StatusCode(500, new { error = $"Failed to fetch services for cluster {clusterName}" });
            }
        }

        [HttpGet("clusters/{clusterName}/services/{serviceName}")]
        public async Task<IActionResult> GetServiceDetails(string clusterName, string serviceName)
        {
            try
            {
                // Check authentication first
                var authStatus = await _awsAuthService.CheckCredentialStatusAsync();
                if (!authStatus.IsValid)
                {
                    return Unauthorized(new { error = authStatus.ErrorMessage });
                }

                var service = await _awsEcsService.GetServiceDetailsAsync(clusterName, serviceName);
                if (service == null)
                {
                    return NotFound(new { error = $"Service {serviceName} not found in cluster {clusterName}" });
                }

                return Ok(service);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching service details for {ServiceName} in cluster {ClusterName}", serviceName, clusterName);
                return StatusCode(500, new { error = $"Failed to fetch service details for {serviceName}" });
            }
        }
    }
}
