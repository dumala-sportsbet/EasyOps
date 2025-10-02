using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyOps.Models
{
    public class Cluster
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string ClusterName { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string AwsProfile { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string AccountId { get; set; } = string.Empty;

        // Foreign key to Environment
        [Required]
        public int EnvironmentId { get; set; }

        // Navigation property
        [ForeignKey("EnvironmentId")]
        public AwsEnvironment Environment { get; set; } = null!;

        // Optional foreign key to Monorepo (if we want to associate clusters with monorepos)
        public int? MonorepoId { get; set; }

        // Navigation property
        [ForeignKey("MonorepoId")]
        public Monorepo? Monorepo { get; set; }
    }

    public class ClusterWithEnvironment
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ClusterName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AwsProfile { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public int EnvironmentId { get; set; }
        public string EnvironmentType { get; set; } = string.Empty;
        public int? MonorepoId { get; set; }
    }
}