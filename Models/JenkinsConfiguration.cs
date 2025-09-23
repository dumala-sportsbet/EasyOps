namespace EasyOps.Models
{
    public class JenkinsConfiguration
    {
        public string BaseUrl { get; set; } = "";
        public string Username { get; set; } = "";
        public string ApiToken { get; set; } = "";
        public string MonorepoJob { get; set; } = "";
        public List<MonorepoOption> AvailableMonorepos { get; set; } = new List<MonorepoOption>();
    }
    
    public class MonorepoOption
    {
        public string Name { get; set; } = "";
        public string JobPath { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class JenkinsJobParametersResponse
    {
        public string Project { get; set; } = "";
        public string Branch { get; set; } = "";
        public string JobType { get; set; } = "";
        public bool HasParameters { get; set; }
        public List<JenkinsParameterDefinition> ParameterDefinitions { get; set; } = new List<JenkinsParameterDefinition>();
    }

    public class JenkinsParameterDefinition
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public object? DefaultValue { get; set; }
        public List<string>? Choices { get; set; } // For choice parameters
    }

    public class JobExecuteWithParametersRequest
    {
        public string Project { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string JobType { get; set; } = "build"; // "build" or "deploy"
        public string Monorepo { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    public class DeployParameters
    {
        public string APP_VERSION { get; set; } = string.Empty;
        public bool DEPLOY_TO_DEV { get; set; }
        public bool DEPLOY_TO_STG { get; set; }
        public bool DEPLOY_TO_PRD { get; set; }
        public string CHANGE_DESCRIPTION { get; set; } = string.Empty;
    }
}
