using System.ComponentModel.DataAnnotations;

namespace EasyOps.Models
{
    public class AwsEnvironment
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string EnvironmentType { get; set; } = string.Empty; // Development, Staging, Production

        [Required]
        [StringLength(50)]
        public string AwsProfile { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string AccountId { get; set; } = string.Empty;

        [StringLength(500)]
        public string SamlRole { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public bool IsDefault { get; set; } = false;

        // Navigation property for related clusters
        public ICollection<Cluster> Clusters { get; set; } = new List<Cluster>();
    }
}