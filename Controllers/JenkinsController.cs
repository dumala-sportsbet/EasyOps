using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using EasyOps.Models;
using EasyOps.Services;
using Microsoft.Extensions.Options;

namespace EasyOps.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JenkinsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly JenkinsConfiguration _jenkinsConfig;
        private readonly ILogger<JenkinsController> _logger;
        private readonly IAuthenticationService _authService;
        private readonly IDatabaseService _databaseService;

        public JenkinsController(IHttpClientFactory httpClientFactory, IOptions<JenkinsConfiguration> jenkinsConfig, ILogger<JenkinsController> logger, IAuthenticationService authService, IDatabaseService databaseService)
        {
            _httpClientFactory = httpClientFactory;
            _jenkinsConfig = jenkinsConfig.Value;
            _logger = logger;
            _authService = authService;
            _databaseService = databaseService;
        }

        private HttpClient CreateAuthenticatedHttpClient()
        {
            var httpClient = _httpClientFactory.CreateClient();

            // Get credentials from session first, fallback to config for development
            var userCredentials = _authService.GetCurrentUserCredentials(HttpContext);

            string username, apiToken;
            if (userCredentials != null)
            {
                username = userCredentials.Username;
                apiToken = userCredentials.ApiToken;
            }
            else
            {
                // Fallback to config credentials (for development/backward compatibility)
                username = _jenkinsConfig.Username;
                apiToken = _jenkinsConfig.ApiToken;
            }

            // Add Basic Authentication header
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(apiToken))
            {
                var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{apiToken}"));
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");
            }

            return httpClient;
        }

        private IActionResult? CheckAuthentication()
        {
            if (!_authService.IsAuthenticated(HttpContext))
            {
                return Unauthorized(new { error = "Authentication required. Please log in with your Jenkins credentials." });
            }
            return null;
        }

        private JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        [HttpGet("monorepos")]
        public async Task<ActionResult<List<MonorepoOption>>> GetAvailableMonorepos()
        {
            try
            {
                var monorepos = await _databaseService.GetMonoreposAsync();
                var options = monorepos.Select(m => new MonorepoOption
                {
                    Name = m.Name,
                    JobPath = m.JobPath,
                    Description = m.Description
                }).ToList();

                return Ok(options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching available monorepos");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("projects")]
        public async Task<IActionResult> GetProjects([FromQuery] string? monorepo = null)
        {
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            try
            {
                var httpClient = CreateAuthenticatedHttpClient();

                // Use the provided monorepo or fall back to the default configured one
                var selectedMonorepo = !string.IsNullOrEmpty(monorepo) ? monorepo : _jenkinsConfig.MonorepoJob;

                // Get projects from Jenkins API - based on your structure: Sports/sb-rtp-sports-afl
                var url = $"{_jenkinsConfig.BaseUrl}job/Sports/job/{selectedMonorepo}/api/json";
                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to fetch projects from Jenkins. Status: {response.StatusCode}");
                    return StatusCode(500, "Failed to fetch projects from Jenkins");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var jenkinsData = JsonSerializer.Deserialize<JenkinsJobResponse>(jsonContent, GetJsonOptions());

                var projects = jenkinsData?.Jobs?.Where(job => !string.IsNullOrEmpty(job.Name) &&
                        job.Name != "java-common" && job.Name != "central-router")
                    .Select(job => new ProjectInfo
                    {
                        Name = job.Name!,
                        DisplayName = job.DisplayName ?? job.Name!,
                        Url = job.Url
                    }).ToList() ?? new List<ProjectInfo>();

                return Ok(projects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching projects from Jenkins");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("build-version")]
        public async Task<IActionResult> GetBuildVersion([FromQuery] string project, [FromQuery] string branch, [FromQuery] string? monorepo = null)
        {
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            try
            {
                if (string.IsNullOrEmpty(project) || string.IsNullOrEmpty(branch))
                {
                    return BadRequest("Project and branch parameters are required");
                }

                var httpClient = CreateAuthenticatedHttpClient();

                // Use the provided monorepo or fall back to the default configured one
                var selectedMonorepo = !string.IsNullOrEmpty(monorepo) ? monorepo : _jenkinsConfig.MonorepoJob;

                // Get build pipeline information
                var buildInfo = await GetPipelineInfo(httpClient, project, branch, "build-pipeline", selectedMonorepo);

                // TEMPORARILY COMMENTED OUT - Get deployment information for all environments
                // var deployInfo = await GetDeploymentInfoForAllEnvironments(httpClient, project, branch, selectedMonorepo);

                var result = new BuildVersionInfo
                {
                    Project = project,
                    Branch = branch,
                    BuildNumber = buildInfo.BuildNumber,
                    Version = buildInfo.Version,
                    BuildUrl = buildInfo.BuildUrl,
                    Timestamp = buildInfo.Timestamp,
                    Status = buildInfo.Status,
                    IsBuilding = buildInfo.IsBuilding,
                    LastSuccessfulBuildNumber = buildInfo.LastSuccessfulBuildNumber,
                    LastSuccessfulVersion = buildInfo.LastSuccessfulVersion,
                    LastSuccessfulBuildUrl = buildInfo.LastSuccessfulBuildUrl,
                    // TEMPORARILY COMMENTED OUT - Deployment info
                    // DevDeploy = deployInfo.DevDeploy,
                    // StagingDeploy = deployInfo.StagingDeploy,
                    // ProductionDeploy = deployInfo.ProductionDeploy
                    DevDeploy = new DeployEnvironmentInfo(),
                    StagingDeploy = new DeployEnvironmentInfo(),
                    ProductionDeploy = new DeployEnvironmentInfo()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting build version for project {project} and branch {branch}");
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task<(int BuildNumber, string Version, string? BuildUrl, long? Timestamp, string? Status, bool IsBuilding, 
            int? LastSuccessfulBuildNumber, string? LastSuccessfulVersion, string? LastSuccessfulBuildUrl)> GetPipelineInfo(
            HttpClient httpClient, string project, string branch, string pipelineType, string? monorepo = null)
        {
            try
            {
                // Use the provided monorepo or fall back to the default configured one
                var selectedMonorepo = !string.IsNullOrEmpty(monorepo) ? monorepo : _jenkinsConfig.MonorepoJob;

                // Get the pipeline job for the project and branch
                var pipelineUrl = $"{_jenkinsConfig.BaseUrl}job/Sports/job/{selectedMonorepo}/job/{project}/job/{pipelineType}/job/{branch}/api/json";
                var response = await httpClient.GetAsync(pipelineUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"{pipelineType} not found for project {project} and branch {branch}. Status: {response.StatusCode}");
                    return (0, pipelineType == "build-pipeline" ? "No previous builds" : "No previous deploys", null, null, "NotFound", false, null, null, null);
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var branchData = JsonSerializer.Deserialize<JenkinsBranchResponse>(jsonContent, GetJsonOptions());

                if (branchData?.LastBuild?.Number == null)
                {
                    return (0, pipelineType == "build-pipeline" ? "No previous builds" : "No previous deploys", null, null, "NoBuildHistory", false, null, null, null);
                }

                // Get the last build (regardless of status)
                var lastBuildNumber = branchData.LastBuild.Number;
                var lastBuildDetailsUrl = $"{_jenkinsConfig.BaseUrl}job/Sports/job/{selectedMonorepo}/job/{project}/job/{pipelineType}/job/{branch}/{lastBuildNumber}/api/json";
                
                var lastBuildResponse = await httpClient.GetAsync(lastBuildDetailsUrl);
                if (!lastBuildResponse.IsSuccessStatusCode)
                {
                    return (lastBuildNumber, "Unknown version", null, null, "Unknown", false, null, null, null);
                }

                var lastBuildJsonContent = await lastBuildResponse.Content.ReadAsStringAsync();
                var lastBuildData = JsonSerializer.Deserialize<JenkinsBuildResponse>(lastBuildJsonContent, GetJsonOptions());

                // Extract version from last build
                var lastVersion = ExtractVersionFromBuild(lastBuildData);
                var lastStatus = lastBuildData?.Result ?? (lastBuildData?.Building == true ? "IN_PROGRESS" : "UNKNOWN");
                var isBuilding = lastBuildData?.Building ?? false;

                // Get last successful build info (if different from last build)
                int? lastSuccessfulBuildNumber = null;
                string? lastSuccessfulVersion = null;
                string? lastSuccessfulBuildUrl = null;

                // Only get last successful build if the last build is not successful
                if (lastStatus != "SUCCESS" && branchData.LastSuccessfulBuild?.Number != null)
                {
                    lastSuccessfulBuildNumber = branchData.LastSuccessfulBuild.Number;
                    var successBuildDetailsUrl = $"{_jenkinsConfig.BaseUrl}job/Sports/job/{selectedMonorepo}/job/{project}/job/{pipelineType}/job/{branch}/{lastSuccessfulBuildNumber}/api/json";
                    
                    try
                    {
                        var successBuildResponse = await httpClient.GetAsync(successBuildDetailsUrl);
                        if (successBuildResponse.IsSuccessStatusCode)
                        {
                            var successBuildJsonContent = await successBuildResponse.Content.ReadAsStringAsync();
                            var successBuildData = JsonSerializer.Deserialize<JenkinsBuildResponse>(successBuildJsonContent, GetJsonOptions());
                            
                            lastSuccessfulVersion = ExtractVersionFromBuild(successBuildData);
                            lastSuccessfulBuildUrl = successBuildData?.Url;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Error getting last successful build info for {project}");
                    }
                }

                return (lastBuildNumber, lastVersion, lastBuildData?.Url, lastBuildData?.Timestamp, lastStatus, isBuilding,
                    lastSuccessfulBuildNumber, lastSuccessfulVersion, lastSuccessfulBuildUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error getting {pipelineType} info for project {project} and branch {branch}");
                return (0, "Error getting info", null, null, "Error", false, null, null, null);
            }
        }

        private async Task<(DeployEnvironmentInfo DevDeploy, DeployEnvironmentInfo StagingDeploy, DeployEnvironmentInfo ProductionDeploy)>
            GetDeploymentInfoForAllEnvironments(HttpClient httpClient, string project, string branch, string? monorepo = null)
        {
            var devDeploy = new DeployEnvironmentInfo();
            var stagingDeploy = new DeployEnvironmentInfo();
            var productionDeploy = new DeployEnvironmentInfo();

            try
            {
                // Use the provided monorepo or fall back to the default configured one
                var selectedMonorepo = !string.IsNullOrEmpty(monorepo) ? monorepo : _jenkinsConfig.MonorepoJob;

                // Get the deploy pipeline job for the project and branch with all builds
                var pipelineUrl = $"{_jenkinsConfig.BaseUrl}job/Sports/job/{selectedMonorepo}/job/{project}/job/deploy-pipeline/job/{branch}/api/json?tree=builds[number,url]";
                var response = await httpClient.GetAsync(pipelineUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Deploy pipeline not found for project {project} and branch {branch}. Status: {response.StatusCode}");
                    return (devDeploy, stagingDeploy, productionDeploy);
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var branchData = JsonSerializer.Deserialize<JenkinsBranchWithBuildsResponse>(jsonContent, GetJsonOptions());

                if (branchData?.Builds == null || !branchData.Builds.Any())
                {
                    return (devDeploy, stagingDeploy, productionDeploy);
                }

                // Loop through builds from newest to oldest to find last successful deploy for each environment
                var environments = new Dictionary<string, DeployEnvironmentInfo>
                {
                    { "DEV", devDeploy },
                    { "STG", stagingDeploy },
                    { "PRD", productionDeploy }
                };

                var foundEnvironments = new HashSet<string>();

                foreach (var build in branchData.Builds.OrderByDescending(b => b.Number))
                {
                    if (foundEnvironments.Count == 3) break; // Found all environments

                    try
                    {
                        // Get build details to check result and extract deployment info
                        var buildDetailsUrl = $"{_jenkinsConfig.BaseUrl}job/Sports/job/{selectedMonorepo}/job/{project}/job/deploy-pipeline/job/{branch}/{build.Number}/api/json";
                        var buildResponse = await httpClient.GetAsync(buildDetailsUrl);

                        if (!buildResponse.IsSuccessStatusCode) continue;

                        var buildJsonContent = await buildResponse.Content.ReadAsStringAsync();
                        var buildData = JsonSerializer.Deserialize<JenkinsBuildResponse>(buildJsonContent, GetJsonOptions());

                        // Only process successful builds
                        if (buildData?.Result != "SUCCESS") continue;

                        // Extract deployment information from description
                        var deploymentInfo = ExtractDeploymentInfoFromDescription(buildData.Description);

                        if (deploymentInfo.AppVersion != null)
                        {
                            foreach (var env in deploymentInfo.Environments)
                            {
                                if (!foundEnvironments.Contains(env) && environments.ContainsKey(env))
                                {
                                    environments[env].BuildNumber = build.Number;
                                    environments[env].Version = deploymentInfo.AppVersion;
                                    environments[env].Url = buildData.Url;
                                    environments[env].Timestamp = buildData.Timestamp;
                                    foundEnvironments.Add(env);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Error processing deploy build {build.Number} for project {project} branch {branch}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error getting deployment info for all environments for project {project} and branch {branch}");
            }

            return (devDeploy, stagingDeploy, productionDeploy);
        }

        private (string? AppVersion, List<string> Environments) ExtractDeploymentInfoFromDescription(string? description)
        {
            var environments = new List<string>();
            string? appVersion = null;

            if (string.IsNullOrEmpty(description))
                return (appVersion, environments);

            // Parse description like "APP_VERSION=3.8.0,<br>DEPLOY_TO_PRD=true,<br>CHANGE_DESCRIPTION=CHG0058546"
            // Handle both literal <br> and Unicode encoded \u003Cbr\u003E
            var normalizedDescription = description
                .Replace("\\u003Cbr\\u003E", "<br>")  // Handle Unicode encoded <br>
                .Replace("\u003Cbr\u003E", "<br>")    // Handle actual Unicode <br>
                .Replace(",<br>", "<br>")             // Remove comma before <br>
                .Replace(", <br>", "<br>");           // Remove comma with space before <br>

            var lines = normalizedDescription.Split(new[] { "<br>", "\n", "\r\n", "," }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("APP_VERSION="))
                {
                    appVersion = trimmedLine.Substring("APP_VERSION=".Length).Trim();
                }
                else if (trimmedLine.StartsWith("DEPLOY_TO_") && trimmedLine.EndsWith("=true"))
                {
                    var envPart = trimmedLine.Substring("DEPLOY_TO_".Length);
                    var envName = envPart.Substring(0, envPart.Length - "=true".Length);
                    environments.Add(envName);
                }
            }

            return (appVersion, environments);
        }

        [HttpPost("execute-job")]
        public async Task<IActionResult> ExecuteJob([FromBody] ExecuteJobRequest request)
        {
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            try
            {
                if (string.IsNullOrEmpty(request.Project) || string.IsNullOrEmpty(request.Branch))
                {
                    return BadRequest("Project and branch parameters are required");
                }

                // Use the monorepo from request, fallback to config if not provided
                string monorepoJob = GetMonorepoJobPath(request.Monorepo);

                var httpClient = CreateAuthenticatedHttpClient();

                // Properly encode the branch name for Jenkins URL
                var encodedBranch = EncodeJenkinsBranch(request.Branch);

                // First, get the current next build number before triggering the build
                string jobInfoPath;
                if (request.JobType?.ToLower() == "deploy")
                {
                    jobInfoPath = $"/job/Sports/job/{monorepoJob}/job/{request.Project}/job/deploy-pipeline/job/{encodedBranch}/api/json";
                }
                else
                {
                    jobInfoPath = $"/job/Sports/job/{monorepoJob}/job/{request.Project}/job/build-pipeline/job/{encodedBranch}/api/json";
                }

                var jobInfoUrl = $"{_jenkinsConfig.BaseUrl}{jobInfoPath}";
                var jobInfoResponse = await httpClient.GetAsync(jobInfoUrl);

                int nextBuildNumber = 1; // Default fallback
                bool hasPreviousBuilds = false;
                if (jobInfoResponse.IsSuccessStatusCode)
                {
                    var jobInfoContent = await jobInfoResponse.Content.ReadAsStringAsync();
                    var jobInfo = JsonSerializer.Deserialize<JenkinsPipelineResponse>(jobInfoContent, GetJsonOptions());
                    nextBuildNumber = (jobInfo?.NextBuildNumber ?? 1);
                    hasPreviousBuilds = nextBuildNumber > 1;
                }

                // Determine the job path based on job type and whether there are previous builds
                string jobPath;
                string buildEndpoint = hasPreviousBuilds ? "buildWithParameters" : "build";

                if (request.JobType?.ToLower() == "deploy")
                {
                    // For deploy jobs, always use buildWithParameters since they require parameters
                    //buildEndpoint = "buildWithParameters";
                    jobPath = $"/job/Sports/job/{monorepoJob}/job/{request.Project}/job/deploy-pipeline/job/{encodedBranch}/{buildEndpoint}";
                }
                else
                {
                    // For build jobs, use build-pipeline
                    jobPath = $"/job/Sports/job/{monorepoJob}/job/{request.Project}/job/build-pipeline/job/{encodedBranch}/{buildEndpoint}";
                }

                var jobUrl = $"{_jenkinsConfig.BaseUrl}{jobPath}";

                // Prepare the request content
                HttpContent? content = null;
                if (request.JobType?.ToLower() == "deploy" && request.DeployParams != null)
                {
                    // Create form data for deploy parameters
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
                    // For build jobs that need parameters, just send empty form data
                    content = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>());
                }

                // Execute the Jenkins job
                HttpResponseMessage response;
                if (content != null)
                {
                    response = await httpClient.PostAsync(jobUrl, content);
                }
                else
                {
                    response = await httpClient.PostAsync(jobUrl, null);
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to execute {request.JobType} job for project {request.Project} and branch {request.Branch}. Status: {response.StatusCode}");
                    return StatusCode(500, $"Failed to execute {request.JobType} job");
                }

                // Get the queue URL from the Location header (if available)
                var queueUrl = response.Headers.Location?.ToString();

                var result = new ExecuteJobResponse
                {
                    Project = request.Project,
                    Branch = request.Branch,
                    JobType = request.JobType ?? "build",
                    Status = "Started",
                    JobUrl = jobUrl.Replace($"/{buildEndpoint}", ""), // Remove build endpoint from URL for viewing
                    QueueUrl = queueUrl,
                    ExecutedAt = DateTimeOffset.UtcNow,
                    NextBuildNumber = nextBuildNumber
                };

                _logger.LogInformation($"Successfully started {request.JobType} job for project {request.Project} on branch {request.Branch}. Next build number: {nextBuildNumber}. Used endpoint: {buildEndpoint}");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing {request.JobType} job for project {request.Project} and branch {request.Branch}");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("build-status")]
        public async Task<IActionResult> GetBuildStatus(string project, string branch = "develop", string jobType = "build", int? buildNumber = null, string? monorepo = null)
        {
            try
            {
                if (string.IsNullOrEmpty(project))
                {
                    return BadRequest("Project parameter is required");
                }

                var httpClient = CreateAuthenticatedHttpClient();
                
                // Get the correct monorepo job path
                string monorepoJob = GetMonorepoJobPath(monorepo);

                // If buildNumber is provided, get status for that specific build
                if (buildNumber.HasValue)
                {
                    // Determine the specific build path based on job type
                    string buildPath;
                    if (jobType?.ToLower() == "deploy")
                    {
                        buildPath = $"/job/Sports/job/{monorepoJob}/job/{project}/job/deploy-pipeline/job/{branch}/{buildNumber}/api/json";
                    }
                    else
                    {
                        buildPath = $"/job/Sports/job/{monorepoJob}/job/{project}/job/build-pipeline/job/{branch}/{buildNumber}/api/json";
                    }

                    var buildUrl = $"{_jenkinsConfig.BaseUrl}{buildPath}";
                    var buildResponse = await httpClient.GetAsync(buildUrl);

                    if (!buildResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"Build #{buildNumber} not found for project {project} and branch {branch}. Status: {buildResponse.StatusCode}");
                        return Ok(new BuildStatusResponse
                        {
                            Project = project,
                            Branch = branch,
                            JobType = jobType ?? "build",
                            BuildNumber = buildNumber.Value,
                            Status = "NotFound",
                            IsBuilding = false,
                            Message = $"Build #{buildNumber} not found"
                        });
                    }

                    var buildContent = await buildResponse.Content.ReadAsStringAsync();
                    var buildData = JsonSerializer.Deserialize<JenkinsBuildResponse>(buildContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    var specificBuildResult = new BuildStatusResponse
                    {
                        Project = project,
                        Branch = branch,
                        JobType = jobType ?? "build",
                        BuildNumber = buildData?.Number ?? buildNumber.Value,
                        Status = buildData?.Result ?? (buildData?.Building == true ? "IN_PROGRESS" : "UNKNOWN"),
                        IsBuilding = buildData?.Building ?? false,
                        BuildUrl = buildData?.Url,
                        Version = ExtractVersionFromBuild(buildData),
                        Timestamp = buildData?.Timestamp,
                        Duration = buildData?.Duration,
                        EstimatedDuration = buildData?.EstimatedDuration
                    };

                    return Ok(specificBuildResult);
                }

                // Fallback: If no specific build number, get the comprehensive build status
                string jobPath;
                if (jobType?.ToLower() == "deploy")
                {
                    jobPath = $"/job/Sports/job/{monorepoJob}/job/{project}/job/deploy-pipeline/job/{branch}/api/json";
                }
                else
                {
                    jobPath = $"/job/Sports/job/{monorepoJob}/job/{project}/job/build-pipeline/job/{branch}/api/json";
                }

                var url = $"{_jenkinsConfig.BaseUrl}{jobPath}";
                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Build pipeline not found for project {project} and branch {branch}. Status: {response.StatusCode}");
                    return Ok(new BuildStatusResponse
                    {
                        Project = project,
                        Branch = branch,
                        JobType = jobType ?? "build",
                        Status = "NotFound",
                        IsBuilding = false,
                        Message = "Build pipeline not found"
                    });
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var pipelineData = JsonSerializer.Deserialize<JenkinsPipelineResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Initialize result object
                var result = new BuildStatusResponse
                {
                    Project = project,
                    Branch = branch,
                    JobType = jobType ?? "build",
                    Status = "NoBuilds",
                    IsBuilding = false,
                    Message = "No builds found"
                };

                // Get last successful build information
                if (pipelineData?.LastSuccessfulBuild != null)
                {
                    var lastSuccessfulBuildUrl = $"{pipelineData.LastSuccessfulBuild.Url}api/json";
                    var lastSuccessfulResponse = await httpClient.GetAsync(lastSuccessfulBuildUrl);

                    if (lastSuccessfulResponse.IsSuccessStatusCode)
                    {
                        var lastSuccessfulContent = await lastSuccessfulResponse.Content.ReadAsStringAsync();
                        var lastSuccessfulData = JsonSerializer.Deserialize<JenkinsBuildResponse>(lastSuccessfulContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        result.LastSuccessfulBuildNumber = lastSuccessfulData?.Number;
                        result.LastSuccessfulVersion = ExtractVersionFromBuild(lastSuccessfulData);
                    }
                }

                // Check current/last build status
                if (pipelineData?.LastBuild != null)
                {
                    var lastBuildUrl = $"{pipelineData.LastBuild.Url}api/json";
                    var buildResponse = await httpClient.GetAsync(lastBuildUrl);

                    if (buildResponse.IsSuccessStatusCode)
                    {
                        var buildContent = await buildResponse.Content.ReadAsStringAsync();
                        var buildData = JsonSerializer.Deserialize<JenkinsBuildResponse>(buildContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        result.BuildNumber = buildData?.Number ?? 0;
                        result.Status = buildData?.Result ?? (buildData?.Building == true ? "IN_PROGRESS" : "UNKNOWN");
                        result.IsBuilding = buildData?.Building ?? false;
                        result.BuildUrl = buildData?.Url;
                        result.Version = ExtractVersionFromBuild(buildData);
                        result.Timestamp = buildData?.Timestamp;
                        result.Duration = buildData?.Duration;
                        result.EstimatedDuration = buildData?.EstimatedDuration;
                        
                        // For production deployments, always provide approval URL
                        if (jobType == "deploy" && !string.IsNullOrEmpty(buildData?.Url))
                        {
                            result.ApprovalUrl = $"{buildData.Url}input/";
                        }

                        // Check if this is an in-progress build (different from last successful)
                        if (result.IsBuilding || result.Status == "IN_PROGRESS")
                        {
                            result.HasInProgressBuild = true;
                            result.InProgressVersion = result.Version;
                            // Keep the main version as the last successful for display
                            if (!string.IsNullOrEmpty(result.LastSuccessfulVersion))
                            {
                                result.Version = result.LastSuccessfulVersion;
                            }
                        }

                        return Ok(result);
                    }
                }

                return Ok(new BuildStatusResponse
                {
                    Project = project,
                    Branch = branch,
                    JobType = jobType ?? "build",
                    Status = "NoBuilds",
                    IsBuilding = false,
                    Message = "No builds found"
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting build status for project {project} and branch {branch}");
                return StatusCode(500, "Internal server error");
            }
        }


        private string ExtractVersionFromBuild(JenkinsBuildResponse? buildData)
        {
            if (buildData == null) return "Unknown";

            _logger.LogInformation($"Extracting version from build #{buildData.Number}");
            _logger.LogInformation($"Description: '{buildData.Description}'");
            _logger.LogInformation($"DisplayName: '{buildData.DisplayName}'");

            // First, try to extract version from description field
            // Example: "Build develop:3.8.0-alpha-46" -> "3.8.0-alpha-46"
            if (!string.IsNullOrEmpty(buildData.Description))
            {
                var colonIndex = buildData.Description.LastIndexOf(':');
                if (colonIndex >= 0 && colonIndex < buildData.Description.Length - 1)
                {
                    var versionFromDesc = buildData.Description.Substring(colonIndex + 1).Trim();
                    if (!string.IsNullOrEmpty(versionFromDesc) && IsValidVersionString(versionFromDesc))
                    {
                        _logger.LogInformation($"Version extracted from description: '{versionFromDesc}'");
                        return versionFromDesc;
                    }
                }
            }

            // Second, try to extract from displayName field
            // Example: "Branch: develop Version: 3.8.0-alpha-46" -> "3.8.0-alpha-46"
            if (!string.IsNullOrEmpty(buildData.DisplayName))
            {
                // Look for "Version: " pattern
                var versionPattern = "Version: ";
                var versionIndex = buildData.DisplayName.IndexOf(versionPattern, StringComparison.OrdinalIgnoreCase);
                if (versionIndex >= 0)
                {
                    var versionStart = versionIndex + versionPattern.Length;
                    var versionFromDisplay = buildData.DisplayName.Substring(versionStart).Trim();
                    if (!string.IsNullOrEmpty(versionFromDisplay) && IsValidVersionString(versionFromDisplay))
                    {
                        _logger.LogInformation($"Version extracted from displayName (Version pattern): '{versionFromDisplay}'");
                        return versionFromDisplay;
                    }
                }

                // Alternative: Look for colon pattern in displayName
                var colonIndex = buildData.DisplayName.LastIndexOf(':');
                if (colonIndex >= 0 && colonIndex < buildData.DisplayName.Length - 1)
                {
                    var versionFromDisplay = buildData.DisplayName.Substring(colonIndex + 1).Trim();
                    if (!string.IsNullOrEmpty(versionFromDisplay) && IsValidVersionString(versionFromDisplay))
                    {
                        _logger.LogInformation($"Version extracted from displayName (colon pattern): '{versionFromDisplay}'");
                        return versionFromDisplay;
                    }
                }
            }

            // Third, look for version in build parameters (fallback)
            if (buildData.Actions != null)
            {
                _logger.LogInformation($"Checking {buildData.Actions.Count} actions for parameters");
                foreach (var action in buildData.Actions)
                {
                    if (action.Parameters != null)
                    {
                        _logger.LogInformation($"Found {action.Parameters.Count} parameters in action");
                        foreach (var param in action.Parameters)
                        {
                            _logger.LogInformation($"Parameter: {param.Name} = {param.GetValueAsString()}");
                        }

                        var versionParam = action.Parameters.FirstOrDefault(p =>
                            p.Name?.ToLower().Contains("version") == true ||
                            p.Name?.ToLower().Contains("tag") == true);

                        if (versionParam != null)
                        {
                            var versionValue = versionParam.GetValueAsString();
                            if (!string.IsNullOrEmpty(versionValue) && IsValidVersionString(versionValue))
                            {
                                _logger.LogInformation($"Version extracted from parameters: '{versionValue}'");
                                return versionValue;
                            }
                        }
                    }

                    // Look for version in environment variables
                    if (action.Environment != null)
                    {
                        _logger.LogInformation($"Found {action.Environment.Count} environment variables in action");
                        foreach (var env in action.Environment)
                        {
                            _logger.LogInformation($"Environment: {env.Key} = {env.Value}");
                            if (env.Key?.ToLower().Contains("version") == true ||
                                env.Key?.ToLower().Contains("tag") == true)
                            {
                                var envValue = env.Value?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(envValue) && IsValidVersionString(envValue))
                                {
                                    _logger.LogInformation($"Version extracted from environment: '{envValue}'");
                                    return envValue;
                                }
                            }
                        }
                    }
                }
            }

            // Final fallback to build number if no version found
            _logger.LogWarning($"No version found for build #{buildData.Number}, using fallback");
            return $"Build-{buildData.Number}";
        }

        /// <summary>
        /// Validates if a string looks like a valid version number.
        /// Rejects boolean values like "true"/"false" and other non-version strings.
        /// </summary>
        private bool IsValidVersionString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // Reject obvious boolean values
            if (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;

            // Reject if it's just a number without any version indicators
            if (int.TryParse(value, out _))
                return false;

            // Accept if it contains common version patterns
            // Examples: 1.0.0, v1.2.3, 2.1.4-alpha, 3.0.0-beta.1, etc.
            return value.Contains('.') || 
                   value.Contains('-') || 
                   value.StartsWith("v", StringComparison.OrdinalIgnoreCase) ||
                   System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d+\.\d+");
        }

        private string GetMonorepoJobPath(string? monorepoName)
        {
            if (string.IsNullOrEmpty(monorepoName))
                return _jenkinsConfig.MonorepoJob;

            // Map monorepo names to their actual Jenkins job paths
            return monorepoName;
        }

        private string EncodeJenkinsBranch(string branch)
        {
            // Jenkins requires branch names with forward slashes to be double-encoded
            // First encode the branch, then encode it again
            return Uri.EscapeDataString(Uri.EscapeDataString(branch));
        }

        [HttpGet("job-parameters")]
        public async Task<IActionResult> GetJobParameters([FromQuery] string project, [FromQuery] string branch, [FromQuery] string jobType = "build", [FromQuery] string? monorepo = null)
        {
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            try
            {
                if (string.IsNullOrEmpty(project) || string.IsNullOrEmpty(branch))
                {
                    return BadRequest("Project and branch parameters are required");
                }

                var httpClient = CreateAuthenticatedHttpClient();

                // Use the provided monorepo or fall back to the default configured one
                var selectedMonorepo = GetMonorepoJobPath(monorepo);

                // Properly encode the branch name for Jenkins URL
                var encodedBranch = EncodeJenkinsBranch(branch);

                // Determine the job path based on job type
                string jobPath;
                if (jobType?.ToLower() == "deploy")
                {
                    jobPath = $"/job/Sports/job/{selectedMonorepo}/job/{project}/job/deploy-pipeline/job/{encodedBranch}/api/json";
                }
                else
                {
                    jobPath = $"/job/Sports/job/{selectedMonorepo}/job/{project}/job/build-pipeline/job/{encodedBranch}/api/json";
                }

                var jobUrl = $"{_jenkinsConfig.BaseUrl}{jobPath}";
                var response = await httpClient.GetAsync(jobUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Job not found for project {project} and branch {branch}. Status: {response.StatusCode}");
                    return Ok(new JenkinsJobParametersResponse
                    {
                        Project = project,
                        Branch = branch,
                        JobType = jobType ?? "build",
                        HasParameters = false,
                        ParameterDefinitions = new List<JenkinsParameterDefinition>()
                    });
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var jobData = JsonSerializer.Deserialize<JenkinsJobDetailsResponse>(jsonContent, GetJsonOptions());

                var parameterDefinitions = ExtractParameterDefinitions(jobData?.Property);

                var result = new JenkinsJobParametersResponse
                {
                    Project = project,
                    Branch = branch,
                    JobType = jobType ?? "build",
                    HasParameters = parameterDefinitions.Count > 0,
                    ParameterDefinitions = parameterDefinitions
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting job parameters for project {project} and branch {branch}");
                return StatusCode(500, "Internal server error");
            }
        }

        private List<JenkinsParameterDefinition> ExtractParameterDefinitions(List<JenkinsProperty>? properties)
        {
            var parameterDefinitions = new List<JenkinsParameterDefinition>();

            if (properties == null) return parameterDefinitions;

            foreach (var property in properties)
            {
                if (property.Class == "hudson.model.ParametersDefinitionProperty" && property.ParameterDefinitions != null)
                {
                    foreach (var paramDef in property.ParameterDefinitions)
                    {
                        var parameter = new JenkinsParameterDefinition
                        {
                            Name = paramDef.Name ?? "",
                            Type = ExtractParameterType(paramDef.Class),
                            Description = paramDef.Description ?? ""
                        };

                        // Extract default value based on parameter type
                        if (paramDef.DefaultParameterValue != null)
                        {
                            parameter.DefaultValue = ExtractDefaultValue(paramDef.DefaultParameterValue, paramDef.Class);
                        }

                        // Extract choices for choice parameters
                        if (paramDef.Choices != null && paramDef.Choices.Count > 0)
                        {
                            parameter.Choices = paramDef.Choices;
                        }

                        parameterDefinitions.Add(parameter);
                    }
                }
            }

            return parameterDefinitions;
        }

        private string ExtractParameterType(string? className)
        {
            return className switch
            {
                "hudson.model.BooleanParameterDefinition" => "boolean",
                "hudson.model.StringParameterDefinition" => "string",
                "hudson.model.TextParameterDefinition" => "text",
                "hudson.model.ChoiceParameterDefinition" => "choice",
                "hudson.model.PasswordParameterDefinition" => "password",
                _ => "string" // Default to string for unknown types
            };
        }

        private object? ExtractDefaultValue(JenkinsParameterValue defaultValue, string? className)
        {
            return className switch
            {
                "hudson.model.BooleanParameterDefinition" => defaultValue.Value?.ToString()?.ToLower() == "true",
                _ => defaultValue.Value?.ToString()
            };
        }

        [HttpPost("execute-job-with-parameters")]
        public async Task<IActionResult> ExecuteJobWithParameters([FromBody] JobExecuteWithParametersRequest request)
        {
            var authCheck = CheckAuthentication();
            if (authCheck != null) return authCheck;

            try
            {
                if (string.IsNullOrEmpty(request.Project) || string.IsNullOrEmpty(request.Branch))
                {
                    return BadRequest("Project and branch parameters are required");
                }

                // Use the monorepo from request, fallback to config if not provided
                string monorepoJob = GetMonorepoJobPath(request.Monorepo);

                var httpClient = CreateAuthenticatedHttpClient();

                // Properly encode the branch name for Jenkins URL
                var encodedBranch = EncodeJenkinsBranch(request.Branch);

                // Get job info for next build number
                string jobInfoPath;
                if (request.JobType?.ToLower() == "deploy")
                {
                    jobInfoPath = $"/job/Sports/job/{monorepoJob}/job/{request.Project}/job/deploy-pipeline/job/{encodedBranch}/api/json";
                }
                else
                {
                    jobInfoPath = $"/job/Sports/job/{monorepoJob}/job/{request.Project}/job/build-pipeline/job/{encodedBranch}/api/json";
                }

                var jobInfoUrl = $"{_jenkinsConfig.BaseUrl}{jobInfoPath}";
                var jobInfoResponse = await httpClient.GetAsync(jobInfoUrl);

                int nextBuildNumber = 1;
                if (jobInfoResponse.IsSuccessStatusCode)
                {
                    var jobInfoContent = await jobInfoResponse.Content.ReadAsStringAsync();
                    var jobInfo = JsonSerializer.Deserialize<JenkinsPipelineResponse>(jobInfoContent, GetJsonOptions());
                    nextBuildNumber = jobInfo?.NextBuildNumber ?? 1;
                }

                // Always use buildWithParameters endpoint when parameters are provided
                string jobPath;
                if (request.JobType?.ToLower() == "deploy")
                {
                    jobPath = $"/job/Sports/job/{monorepoJob}/job/{request.Project}/job/deploy-pipeline/job/{encodedBranch}/buildWithParameters";
                }
                else
                {
                    jobPath = $"/job/Sports/job/{monorepoJob}/job/{request.Project}/job/build-pipeline/job/{encodedBranch}/buildWithParameters";
                }

                var jobUrl = $"{_jenkinsConfig.BaseUrl}{jobPath}";

                // Prepare form data with parameters
                var formData = new List<KeyValuePair<string, string>>();

                // Add custom parameters from the request
                foreach (var param in request.Parameters)
                {
                    var value = param.Value?.ToString() ?? "";
                    formData.Add(new KeyValuePair<string, string>(param.Key, value));
                }

                // Add deploy parameters if in deploy mode
                if (request.JobType?.ToLower() == "deploy" && request.DeployParams != null)
                {
                    formData.Add(new("APP_VERSION", request.DeployParams.APP_VERSION));
                    formData.Add(new("DEPLOY_TO_DEV", request.DeployParams.DEPLOY_TO_DEV.ToString().ToLower()));
                    formData.Add(new("DEPLOY_TO_STG", request.DeployParams.DEPLOY_TO_STG.ToString().ToLower()));
                    formData.Add(new("DEPLOY_TO_PRD", request.DeployParams.DEPLOY_TO_PRD.ToString().ToLower()));
                    formData.Add(new("CHANGE_DESCRIPTION", request.DeployParams.CHANGE_DESCRIPTION));
                }

                var content = new FormUrlEncodedContent(formData);

                // Execute the Jenkins job
                var response = await httpClient.PostAsync(jobUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to execute {request.JobType} job with parameters for project {request.Project} and branch {request.Branch}. Status: {response.StatusCode}");
                    return StatusCode(500, $"Failed to execute {request.JobType} job with parameters");
                }

                var queueUrl = response.Headers.Location?.ToString();

                var result = new ExecuteJobResponse
                {
                    Project = request.Project,
                    Branch = request.Branch,
                    JobType = request.JobType ?? "build",
                    Status = "Started",
                    JobUrl = jobUrl.Replace("/buildWithParameters", ""),
                    QueueUrl = queueUrl,
                    ExecutedAt = DateTimeOffset.UtcNow,
                    NextBuildNumber = nextBuildNumber
                };

                _logger.LogInformation($"Successfully started {request.JobType} job with parameters for project {request.Project} on branch {request.Branch}. Next build number: {nextBuildNumber}");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing {request.JobType} job with parameters for project {request.Project} and branch {request.Branch}");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    // Data models for Jenkins API responses
    public class JenkinsJobResponse
    {
        public List<JenkinsJob>? Jobs { get; set; }
    }

    public class JenkinsJob
    {
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public string? Url { get; set; }
    }

    public class JenkinsBranchResponse
    {
        public JenkinsBuildReference? LastSuccessfulBuild { get; set; }
        public JenkinsBuildReference? LastBuild { get; set; }
    }

    public class JenkinsBranchWithBuildsResponse
    {
        public List<JenkinsBuildReference>? Builds { get; set; }
    }

    public class JenkinsBuildReference
    {
        public int Number { get; set; }
        public string? Url { get; set; }
    }

    public class JenkinsBuildResponse
    {
        public int Number { get; set; }
        public string? Url { get; set; }
        public long? Timestamp { get; set; }
        public string? Description { get; set; }
        public string? DisplayName { get; set; }
        public bool Building { get; set; }
        public int Duration { get; set; }
        public string? Result { get; set; } // SUCCESS, FAILURE, ABORTED, etc.
        public long? EstimatedDuration { get; set; }
        public List<JenkinsAction>? Actions { get; set; }
    }

    public class JenkinsAction
    {
        [JsonPropertyName("_class")]
        public string? Class { get; set; }
        public List<JenkinsParameter>? Parameters { get; set; }
        public Dictionary<string, object>? Environment { get; set; }
    }

    public class JenkinsParameter
    {
        public string? Name { get; set; }
        public JsonElement Value { get; set; }

        // Helper method to get value as string regardless of original type
        public string? GetValueAsString()
        {
            return Value.ValueKind switch
            {
                JsonValueKind.String => Value.GetString(),
                JsonValueKind.Number => Value.GetDecimal().ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => Value.ToString()
            };
        }
    }

    // Response models
    public class ProjectInfo
    {
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Url { get; set; }
    }

    public class BuildVersionInfo
    {
        public string Project { get; set; } = "";
        public string Branch { get; set; } = "";
        public int BuildNumber { get; set; }
        public string Version { get; set; } = "";
        public string? BuildUrl { get; set; }
        public long? Timestamp { get; set; }
        public string? Status { get; set; } // SUCCESS, FAILURE, IN_PROGRESS, etc.
        public bool IsBuilding { get; set; } // Whether build is currently running

        // Last successful build information (if last build failed)
        public int? LastSuccessfulBuildNumber { get; set; }
        public string? LastSuccessfulVersion { get; set; }
        public string? LastSuccessfulBuildUrl { get; set; }

        // Deploy pipeline information per environment
        public DeployEnvironmentInfo DevDeploy { get; set; } = new();
        public DeployEnvironmentInfo StagingDeploy { get; set; } = new();
        public DeployEnvironmentInfo ProductionDeploy { get; set; } = new();
    }

    public class DeployEnvironmentInfo
    {
        public int BuildNumber { get; set; } = 0;
        public string Version { get; set; } = "No previous deploys";
        public string? Url { get; set; }
        public long? Timestamp { get; set; }
    }

    public class ExecuteJobRequest
    {
        public string Project { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string JobType { get; set; } = "build"; // "build" or "deploy"
        public string Monorepo { get; set; } = string.Empty;
        public DeployParameters? DeployParams { get; set; }
    }

    public class ExecuteJobResponse
    {
        public string Project { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string JobType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string JobUrl { get; set; } = string.Empty;
        public string? QueueUrl { get; set; }
        public DateTimeOffset ExecutedAt { get; set; }
        public int NextBuildNumber { get; set; }
    }

    public class BuildStatusResponse
    {
        public string Project { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string JobType { get; set; } = string.Empty;
        public int BuildNumber { get; set; }
        public string Status { get; set; } = string.Empty; // SUCCESS, FAILURE, IN_PROGRESS, ABORTED, etc.
        public bool IsBuilding { get; set; }
        public string? BuildUrl { get; set; }
        public string Version { get; set; } = string.Empty;
        public long? Timestamp { get; set; }
        public long? Duration { get; set; }
        public long? EstimatedDuration { get; set; }
        public string? Message { get; set; }

        // Additional properties for tracking in-progress builds
        public int? LastSuccessfulBuildNumber { get; set; }
        public string? LastSuccessfulVersion { get; set; }
        public string? InProgressVersion { get; set; }
        public bool HasInProgressBuild { get; set; }
        public string? ApprovalUrl { get; set; }
    }

    public class JenkinsPipelineResponse
    {
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public string? Url { get; set; }
        public JenkinsBuildReference? LastBuild { get; set; }
        public JenkinsBuildReference? LastCompletedBuild { get; set; }
        public JenkinsBuildReference? LastSuccessfulBuild { get; set; }
        public JenkinsBuildReference? LastFailedBuild { get; set; }
        public bool? InQueue { get; set; }
        public bool? Buildable { get; set; }
        public int NextBuildNumber { get; set; }
    }

    public class JenkinsJobDetailsResponse
    {
        public string? Name { get; set; }
        public string? DisplayName { get; set; }
        public string? Url { get; set; }
        public List<JenkinsProperty>? Property { get; set; }
    }

    public class JenkinsProperty
    {
        [JsonPropertyName("_class")]
        public string? Class { get; set; }
        public List<JenkinsParameterDefinitionItem>? ParameterDefinitions { get; set; }
    }

    public class JenkinsParameterDefinitionItem
    {
        [JsonPropertyName("_class")]
        public string? Class { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public JenkinsParameterValue? DefaultParameterValue { get; set; }
        public List<string>? Choices { get; set; }
    }

    public class JenkinsParameterValue
    {
        [JsonPropertyName("_class")]
        public string? Class { get; set; }
        public string? Name { get; set; }
        public object? Value { get; set; }
    }

    public class JobExecuteWithParametersRequest : ExecuteJobRequest
    {
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}
