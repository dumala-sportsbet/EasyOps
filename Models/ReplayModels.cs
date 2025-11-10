using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyOps.Models
{
    /// <summary>
    /// Stores metadata about games fetched from production for replay purposes
    /// </summary>
    public class ReplayGame
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string GameId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string GameName { get; set; } = string.Empty;

        public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? FetchedBy { get; set; }

        public int TotalEvents { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Navigation property
        public ICollection<ReplayGameEvent> Events { get; set; } = new List<ReplayGameEvent>();
    }

    /// <summary>
    /// Stores individual events for a game, matching the production database structure
    /// Based on the columns: id (int4), event_identifier (text), sequence (text), payload (bytea), payload_type (text)
    /// </summary>
    public class ReplayGameEvent
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ReplayGameId { get; set; }

        [Required]
        [Column(TypeName = "TEXT")]
        public string EventIdentifier { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "TEXT")]
        public string Sequence { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "BLOB")]
        public byte[] Payload { get; set; } = Array.Empty<byte>();

        [Required]
        [Column(TypeName = "TEXT")]
        public string PayloadType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("ReplayGameId")]
        public ReplayGame ReplayGame { get; set; } = null!;
    }
}
