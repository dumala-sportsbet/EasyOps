namespace EasyOps.DTOs
{
    public class MonorepoDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JobPath { get; set; } = string.Empty;
    }

    public class EnvironmentDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string EnvironmentType { get; set; } = string.Empty;
        public string AwsProfile { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string SamlRole { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    public class ClusterDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ClusterName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AwsProfile { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public int EnvironmentId { get; set; }
        public int? MonorepoId { get; set; }
        
        // Include simple names instead of full objects to avoid circular references
        public string? EnvironmentName { get; set; }
        public string? MonorepoName { get; set; }
    }
}