using System.Text;
using System.Text.Json;
using EasyOps.Shared.Models;
using Microsoft.Extensions.Logging;

namespace EasyOps.Shared.Services;

public class JenkinsService : IJenkinsService
{
    private readonly HttpClient _httpClient;
    private readonly JenkinsConfiguration _config;
    private readonly ILogger<JenkinsService> _logger;
    private readonly Dictionary<string, ProjectMapping> _projectMappings;

    public JenkinsService(HttpClient httpClient, JenkinsConfiguration config, ILogger<JenkinsService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _projectMappings = InitializeProjectMappings();
        
        // Setup authentication
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

    private Dictionary<string, ProjectMapping> InitializeProjectMappings()
    {
        // This maps friendly names to actual Jenkins project names
        return new Dictionary<string, ProjectMapping>(StringComparer.OrdinalIgnoreCase)
        {
            ["afl pricer"] = new ProjectMapping
            {
                FriendlyName = "AFL Pricer",
                JenkinsProjectName = "sb-rtp-sports-afl-pricer",
                DefaultMonorepo = "sb-rtp-sports-afl",
                Description = "AFL Sports Pricer Service"
            },
            ["afl orchestrator"] = new ProjectMapping
            {
                FriendlyName = "AFL Orchestrator",
                JenkinsProjectName = "sb-rtp-sports-afl-orchestrator",
                DefaultMonorepo = "sb-rtp-sports-afl",
                Description = "AFL Orchestrator Service"
            },
            ["rl pricer"] = new ProjectMapping
            {
                FriendlyName = "RL Pricer",
                JenkinsProjectName = "sb-rtp-sports-rl-pricer",
                DefaultMonorepo = "sb-rtp-sports-rl",
                Description = "Rugby League Pricer Service"
            },
            ["rl orchestrator"] = new ProjectMapping
            {
                FriendlyName = "RL Orchestrator",
                JenkinsProjectName = "sb-rtp-sports-rl-orchestrator",
                DefaultMonorepo = "sb-rtp-sports-rl",
                Description = "Rugby League Orchestrator Service"
            },
            // Add more mappings as needed
        };
    }

    public async Task<List<ProjectMapping>> GetProjectMappingsAsync()
    {
        return _projectMappings.Values.ToList();
    }

    public async Task<List<ProjectInfo>> GetProjectsFromJenkinsAsync(string monorepo)
    {
        try
        {
            // Call Jenkins API to get actual projects
            var url = $"{_config.BaseUrl}/job/Sports/job/{monorepo}/api/json";
            _logger.LogInformation($"Fetching projects from Jenkins: {url}");
            
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to fetch projects from Jenkins. Status: {response.StatusCode}");
                return new List<ProjectInfo>();
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var jenkinsData = JsonSerializer.Deserialize<JenkinsJobResponse>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var projects = jenkinsData?.Jobs?.Where(job => !string.IsNullOrEmpty(job.Name) &&
                    job.Name != "java-common" && job.Name != "central-router")
                .Select(job => new ProjectInfo
                {
                    Name = job.Name!,
                    DisplayName = job.DisplayName ?? job.Name!,
                    Url = job.Url ?? ""
                }).ToList() ?? new List<ProjectInfo>();

            _logger.LogInformation($"Found {projects.Count} projects in monorepo {monorepo}");
            return projects;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching projects from Jenkins for monorepo {monorepo}");
            return new List<ProjectInfo>();
        }
    }

    public async Task<List<string>> GetAvailableMonoreposAsync()
    {
        // Return the common monorepos
        // In the future, this could also query Jenkins to get the list dynamically
        return new List<string>
        {
            "sb-rtp-sports-afl",
            "sb-rtp-sports-rl",
            "sb-rtp-sports-racing",
            "sb-rtp-sports-soccer"
        };
    }

    public async Task<JenkinsJobResult> ExecuteJobAsync(ExecuteJobRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Project) || string.IsNullOrEmpty(request.Branch))
            {
                return new JenkinsJobResult
                {
                    Success = false,
                    Message = "Project and branch parameters are required"
                };
            }

            // Resolve monorepo
            var monorepo = string.IsNullOrEmpty(request.Monorepo) 
                ? GetDefaultMonorepoForProject(request.Project) 
                : request.Monorepo;

            // Encode branch name for Jenkins URL
            var encodedBranch = EncodeJenkinsBranch(request.Branch);

            // Determine pipeline type
            var pipelineType = request.JobType?.ToLower() == "deploy" ? "deploy-pipeline" : "build-pipeline";

            // Get next build number
            var jobInfoPath = $"/job/Sports/job/{monorepo}/job/{request.Project}/job/{pipelineType}/job/{encodedBranch}/api/json";
            var jobInfoUrl = $"{_config.BaseUrl}{jobInfoPath}";
            var jobInfoResponse = await _httpClient.GetAsync(jobInfoUrl);

            int nextBuildNumber = 1;
            bool hasPreviousBuilds = false;
            if (jobInfoResponse.IsSuccessStatusCode)
            {
                var jobInfoContent = await jobInfoResponse.Content.ReadAsStringAsync();
                var jobInfo = JsonSerializer.Deserialize<JenkinsPipelineResponse>(jobInfoContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                nextBuildNumber = jobInfo?.NextBuildNumber ?? 1;
                hasPreviousBuilds = nextBuildNumber > 1;
            }

            // Build the job URL
            var buildEndpoint = hasPreviousBuilds ? "buildWithParameters" : "build";
            var jobPath = $"/job/Sports/job/{monorepo}/job/{request.Project}/job/{pipelineType}/job/{encodedBranch}/{buildEndpoint}";
            var jobUrl = $"{_config.BaseUrl}{jobPath}";

            // Prepare request content
            HttpContent? content = null;
            if (request.JobType?.ToLower() == "deploy" && request.DeployParams != null)
            {
                var formData = new List<KeyValuePair<string, string>>
                {
                    new("APP_VERSION", request.DeployParams.APP_VERSION),
                    new("DEPLOY_TO_DEV", request.DeployParams.DEPLOY_TO_DEV.ToString().ToLower()),
                    new("DEPLOY_TO_STG", request.DeployParams.DEPLOY_TO_STG.ToString().ToLower()),
                    new("DEPLOY_TO_PRD", request.DeployParams.DEPLOY_TO_PRD.ToString().ToLower()),
                    new("CHANGE_DESCRIPTION", request.DeployParams.CHANGE_DESCRIPTION)
                };
                content = new FormUrlEncodedContent(formData);
            }
            else if (buildEndpoint == "buildWithParameters")
            {
                content = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>());
            }

            // Execute the job
            HttpResponseMessage response = content != null
                ? await _httpClient.PostAsync(jobUrl, content)
                : await _httpClient.PostAsync(jobUrl, null);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to execute {request.JobType} job for project {request.Project}. Status: {response.StatusCode}");
                return new JenkinsJobResult
                {
                    Success = false,
                    Message = $"Failed to execute job. Status: {response.StatusCode}",
                    Project = request.Project,
                    Branch = request.Branch
                };
            }

            var jobDisplayUrl = jobUrl.Replace($"/{buildEndpoint}", "");
            _logger.LogInformation($"Successfully started {request.JobType} job for project {request.Project} on branch {request.Branch}. Build #{nextBuildNumber}");

            return new JenkinsJobResult
            {
                Success = true,
                Message = $"Successfully started {request.JobType} job for {request.Project} on branch {request.Branch}",
                JobUrl = jobDisplayUrl,
                BuildNumber = nextBuildNumber,
                Project = request.Project,
                Branch = request.Branch
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing job for project {request.Project}");
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
            var resolvedMonorepo = string.IsNullOrEmpty(monorepo) 
                ? GetDefaultMonorepoForProject(project) 
                : monorepo;

            var encodedBranch = EncodeJenkinsBranch(branch);
            var jobPath = $"/job/Sports/job/{resolvedMonorepo}/job/{project}/job/build-pipeline/job/{encodedBranch}/lastBuild/api/json";
            var jobUrl = $"{_config.BaseUrl}{jobPath}";

            var response = await _httpClient.GetAsync(jobUrl);
            if (!response.IsSuccessStatusCode)
            {
                return new BuildStatusResult
                {
                    Project = project,
                    Branch = branch,
                    Status = "Not Found"
                };
            }

            var content = await response.Content.ReadAsStringAsync();
            var buildInfo = JsonSerializer.Deserialize<JenkinsBuildInfo>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return new BuildStatusResult
            {
                Project = project,
                Branch = branch,
                BuildNumber = buildInfo?.Number ?? 0,
                Status = buildInfo?.Result ?? (buildInfo?.Building == true ? "BUILDING" : "UNKNOWN"),
                IsBuilding = buildInfo?.Building ?? false,
                BuildUrl = buildInfo?.Url ?? "",
                Timestamp = buildInfo?.Timestamp != null ? DateTimeOffset.FromUnixTimeMilliseconds(buildInfo.Timestamp).DateTime : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting build status for project {project}");
            return new BuildStatusResult
            {
                Project = project,
                Branch = branch,
                Status = $"Error: {ex.Message}"
            };
        }
    }

    private string GetDefaultMonorepoForProject(string project)
    {
        // Try to find in mappings first
        var mapping = _projectMappings.Values.FirstOrDefault(m => 
            m.JenkinsProjectName.Equals(project, StringComparison.OrdinalIgnoreCase));
        
        if (mapping != null)
        {
            return mapping.DefaultMonorepo;
        }

        // Default fallback logic based on project name
        if (project.Contains("afl", StringComparison.OrdinalIgnoreCase))
            return "sb-rtp-sports-afl";
        if (project.Contains("rl", StringComparison.OrdinalIgnoreCase) || 
            project.Contains("rugby", StringComparison.OrdinalIgnoreCase))
            return "sb-rtp-sports-rl";

        // Default to AFL if unknown
        return "sb-rtp-sports-afl";
    }

    private string EncodeJenkinsBranch(string branch)
    {
        // Double encode to handle special characters like "/" in branch names
        return Uri.EscapeDataString(Uri.EscapeDataString(branch));
    }

    // Helper classes for JSON deserialization
    private class JenkinsPipelineResponse
    {
        public int NextBuildNumber { get; set; }
    }

    private class JenkinsBuildInfo
    {
        public int Number { get; set; }
        public string? Result { get; set; }
        public bool Building { get; set; }
        public string? Url { get; set; }
        public long Timestamp { get; set; }
    }

    private class JenkinsJobResponse
    {
        public List<JenkinsJob>? Jobs { get; set; }
    }

    private class JenkinsJob
    {
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public string? Url { get; set; }
    }
}
