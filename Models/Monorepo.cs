using System.ComponentModel.DataAnnotations;

namespace EasyOps.Models
{
    public class Monorepo
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string JobPath { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        // Navigation property for related clusters
        public ICollection<Cluster> Clusters { get; set; } = new List<Cluster>();
    }
}