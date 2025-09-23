namespace EasyOps.Models
{
    public class AwsConfiguration
    {
        public string Region { get; set; } = "ap-southeast-2";
        public List<EcsClusterOption> AvailableClusters { get; set; } = new List<EcsClusterOption>();
        public int CredentialTimeoutMinutes { get; set; } = 60; // SAML2AWS session timeout
        public List<AwsEnvironmentConfiguration> AvailableEnvironments { get; set; } = new List<AwsEnvironmentConfiguration>();
        public OktaConfiguration Okta { get; set; } = new OktaConfiguration();
    }

    public class OktaConfiguration
    {
        public string Provider { get; set; } = "Okta";
        public string MfaType { get; set; } = "push"; // push, token, etc.
        public bool SkipVerify { get; set; } = false;
        public int SessionDuration { get; set; } = 3600; // 1 hour
        public string LoginUrl { get; set; } = "";
        public bool AutoMfa { get; set; } = true;
    }

    public class EcsClusterOption
    {
        public string Name { get; set; } = "";
        public string ClusterName { get; set; } = "";
        public string Environment { get; set; } = "";
        public string Description { get; set; } = "";
        public string AwsProfile { get; set; } = "";
        public string AccountId { get; set; } = "";
    }

    public class AwsEnvironmentConfiguration
    {
        public string Name { get; set; } = "";
        public string Environment { get; set; } = "";
        public string AwsProfile { get; set; } = "";
        public string AccountId { get; set; } = "";
        public string SamlRole { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsDefault { get; set; } = false;
    }

    public class EcsServiceInfo
    {
        public string ServiceName { get; set; } = "";
        public string TaskDefinitionArn { get; set; } = "";
        public string TaskDefinitionFamily { get; set; } = "";
        public int TaskDefinitionRevision { get; set; }
        public List<EcsContainerInfo> Containers { get; set; } = new List<EcsContainerInfo>();
        public string ServiceStatus { get; set; } = "";
        public int RunningCount { get; set; }
        public int DesiredCount { get; set; }
        public int PendingCount { get; set; }
        public string Cpu { get; set; } = "";
        public string Memory { get; set; } = "";
        public DateTime LastUpdated { get; set; }
    }

    public class EcsContainerInfo
    {
        public string Name { get; set; } = "";
        public string Image { get; set; } = "";
        public string ImageTag { get; set; } = "";
        public int? Cpu { get; set; }
        public int? Memory { get; set; }
        public int? MemoryReservation { get; set; }
        public bool Essential { get; set; }
        public List<string> Environment { get; set; } = new List<string>();
    }

    public class AwsCredentialStatus
    {
        public bool IsValid { get; set; }
        public string Profile { get; set; } = "";
        public string Region { get; set; } = "";
        public DateTime LastChecked { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string ErrorMessage { get; set; } = "";
        public string AccountId { get; set; } = "";
        public string UserId { get; set; } = "";
        public string UserArn { get; set; } = "";
        public string Arn { get; set; } = "";
        public string Environment { get; set; } = "";
        public string EnvironmentName { get; set; } = "";
    }

    // Request models for API endpoints
    public class SwitchEnvironmentRequest
    {
        public string EnvironmentName { get; set; } = "";
    }

    public class GetLoginInstructionsRequest
    {
        public string? EnvironmentName { get; set; }
    }
}
