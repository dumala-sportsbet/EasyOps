namespace EasyOps.Shared.Models;

public class JenkinsConfiguration
{
    public string BaseUrl { get; set; } = "";
    public string Username { get; set; } = "";
    public string ApiToken { get; set; } = "";
}

public class EasyOpsApiConfiguration
{
    public string BaseUrl { get; set; } = "http://localhost:5000";
    public string Username { get; set; } = "";
    public string ApiToken { get; set; } = "";
}

public class ExecuteJobRequest
{
    public string Project { get; set; } = string.Empty;
    public string Branch { get; set; } = "develop";
    public string JobType { get; set; } = "build"; // "build" or "deploy"
    public string Monorepo { get; set; } = string.Empty;
    public DeployParameters? DeployParams { get; set; }
}

public class DeployParameters
{
    public string APP_VERSION { get; set; } = string.Empty;
    public bool DEPLOY_TO_DEV { get; set; }
    public bool DEPLOY_TO_STG { get; set; }
    public bool DEPLOY_TO_PRD { get; set; }
    public string CHANGE_DESCRIPTION { get; set; } = string.Empty;
}

public class JenkinsJobResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string JobUrl { get; set; } = string.Empty;
    public int BuildNumber { get; set; }
    public string Project { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
}

public class BuildStatusResult
{
    public string Project { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public int BuildNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsBuilding { get; set; }
    public string Version { get; set; } = string.Empty;
    public string BuildUrl { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
}

public class ProjectMapping
{
    public string FriendlyName { get; set; } = string.Empty;
    public string JenkinsProjectName { get; set; } = string.Empty;
    public string DefaultMonorepo { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ProjectInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class MonorepoInfo
{
    public string Name { get; set; } = string.Empty;
    public string JobPath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
