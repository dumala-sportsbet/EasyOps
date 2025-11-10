using System.Text;
using System.Text.Json;
using EasyOps.Shared.Models;
using Microsoft.Extensions.Logging;

namespace EasyOps.Shared.Services;

/// <summary>
/// Service that calls the EasyOps Web API instead of directly calling Jenkins.
/// This ensures we use the existing authentication and business logic from the web app.
/// </summary>
public class EasyOpsApiService : IJenkinsService
{
    private readonly HttpClient _httpClient;
    private readonly EasyOpsApiConfiguration _config;
    private readonly ILogger<EasyOpsApiService> _logger;

    public EasyOpsApiService(HttpClient httpClient, EasyOpsApiConfiguration config, ILogger<EasyOpsApiService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        
        SetupAuthentication();
    }

    private void SetupAuthentication()
    {
        if (!string.IsNullOrEmpty(_config.Username) && !string.IsNullOrEmpty(_config.ApiToken))
        {
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_config.Username}:{_config.ApiToken}"));
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");
        }
    }

    public async Task<List<ProjectInfo>> GetProjectsFromJenkinsAsync(string monorepo)
    {
        try
        {
            var url = $"{_config.BaseUrl}/api/jenkins/projects?monorepo={Uri.EscapeDataString(monorepo)}";
            _logger.LogInformation($"Calling EasyOps API: {url}");
            
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to get projects from EasyOps API. Status: {response.StatusCode}, Error: {error}");
                return new List<ProjectInfo>();
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var projects = JsonSerializer.Deserialize<List<ProjectInfo>>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _logger.LogInformation($"Got {projects?.Count ?? 0} projects from EasyOps API");
            return projects ?? new List<ProjectInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error calling EasyOps API to get projects for monorepo {monorepo}");
            return new List<ProjectInfo>();
        }
    }

    public async Task<List<string>> GetAvailableMonoreposAsync()
    {
        try
        {
            var url = $"{_config.BaseUrl}/api/jenkins/monorepos";
            _logger.LogInformation($"Calling EasyOps API: {url}");
            
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to get monorepos from EasyOps API. Status: {response.StatusCode}. Using defaults.");
                return GetDefaultMonorepos();
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var monorepos = JsonSerializer.Deserialize<List<MonorepoInfo>>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return monorepos?.Select(m => m.JobPath).ToList() ?? GetDefaultMonorepos();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling EasyOps API to get monorepos. Using defaults.");
            return GetDefaultMonorepos();
        }
    }

    public async Task<JenkinsJobResult> ExecuteJobAsync(ExecuteJobRequest request)
    {
        try
        {
            var url = $"{_config.BaseUrl}/api/jenkins/execute-job";
            _logger.LogInformation($"Calling EasyOps API: {url}");
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to execute job via EasyOps API. Status: {response.StatusCode}, Response: {responseContent}");
                return new JenkinsJobResult
                {
                    Success = false,
                    Message = $"Failed to execute job. Status: {response.StatusCode}",
                    Project = request.Project,
                    Branch = request.Branch
                };
            }

            var result = JsonSerializer.Deserialize<ExecuteJobResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return new JenkinsJobResult
            {
                Success = true,
                Message = $"Successfully started {request.JobType} job",
                JobUrl = result?.JobUrl ?? "",
                BuildNumber = result?.NextBuildNumber ?? 0,
                Project = request.Project,
                Branch = request.Branch
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error calling EasyOps API to execute job for {request.Project}");
            return new JenkinsJobResult
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                Project = request.Project,
                Branch = request.Branch
            };
        }
    }

    public async Task<BuildStatusResult> GetBuildStatusAsync(string project, string branch, string monorepo)
    {
        try
        {
            var url = $"{_config.BaseUrl}/api/jenkins/build-version?project={Uri.EscapeDataString(project)}&branch={Uri.EscapeDataString(branch)}&monorepo={Uri.EscapeDataString(monorepo)}";
            _logger.LogInformation($"Calling EasyOps API: {url}");
            
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return new BuildStatusResult
                {
                    Project = project,
                    Branch = branch,
                    Status = "Not Found"
                };
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var buildInfo = JsonSerializer.Deserialize<BuildVersionInfo>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return new BuildStatusResult
            {
                Project = project,
                Branch = branch,
                BuildNumber = buildInfo?.BuildNumber ?? 0,
                Status = buildInfo?.Status ?? "UNKNOWN",
                IsBuilding = buildInfo?.IsBuilding ?? false,
                BuildUrl = buildInfo?.BuildUrl ?? "",
                Version = buildInfo?.Version ?? "",
                Timestamp = buildInfo?.Timestamp
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error calling EasyOps API to get build status for {project}");
            return new BuildStatusResult
            {
                Project = project,
                Branch = branch,
                Status = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<List<ProjectMapping>> GetProjectMappingsAsync()
    {
        // Return empty - we don't need friendly name mappings when using the API
        // The user will use actual project names from Jenkins
        return new List<ProjectMapping>();
    }

    private List<string> GetDefaultMonorepos()
    {
        return new List<string>
        {
            "sb-rtp-sports-afl",
            "sb-rtp-sports-rl",
            "sb-rtp-sports-racing",
            "sb-rtp-sports-soccer"
        };
    }

    // Helper classes for API response deserialization
    private class ExecuteJobResponse
    {
        public string Project { get; set; } = "";
        public string Branch { get; set; } = "";
        public string JobType { get; set; } = "";
        public string Status { get; set; } = "";
        public string JobUrl { get; set; } = "";
        public string? QueueUrl { get; set; }
        public DateTimeOffset ExecutedAt { get; set; }
        public int NextBuildNumber { get; set; }
    }

    private class BuildVersionInfo
    {
        public string Project { get; set; } = "";
        public string Branch { get; set; } = "";
        public int BuildNumber { get; set; }
        public string Version { get; set; } = "";
        public string BuildUrl { get; set; } = "";
        public DateTime? Timestamp { get; set; }
        public string Status { get; set; } = "";
        public bool IsBuilding { get; set; }
    }
}
