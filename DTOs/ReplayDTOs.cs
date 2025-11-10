namespace EasyOps.DTOs
{
    /// <summary>
    /// DTO for fetching game data from production
    /// </summary>
    public class FetchGameRequest
    {
        public string GameId { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    /// <summary>
    /// DTO for game event data from production
    /// </summary>
    public class GameEventDto
    {
        public string EventIdentifier { get; set; } = string.Empty;
        public string Sequence { get; set; } = string.Empty;
        public byte[] Payload { get; set; } = Array.Empty<byte>();
        public string PayloadType { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for fetching game response
    /// </summary>
    public class FetchGameResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? ReplayGameId { get; set; }
        public int EventCount { get; set; }
    }

    /// <summary>
    /// DTO for listing saved games
    /// </summary>
    public class SavedGameDto
    {
        public int Id { get; set; }
        public string GameId { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public DateTime FetchedAt { get; set; }
        public string? FetchedBy { get; set; }
        public int TotalEvents { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// DTO for replay execution request
    /// </summary>
    public class ReplayExecutionRequest
    {
        public int ReplayGameId { get; set; }
        public string TargetEnvironment { get; set; } = string.Empty;
        public DateTime GameStartDateTime { get; set; }
        public bool DryRun { get; set; } = false;
    }

    /// <summary>
    /// DTO for replay execution response
    /// </summary>
    public class ReplayExecutionResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int EventsProcessed { get; set; }
        public int EventsFailed { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? NewGameId { get; set; }
    }

    /// <summary>
    /// DTO for replay progress updates
    /// </summary>
    public class ReplayProgressDto
    {
        public int ReplayGameId { get; set; }
        public int TotalEvents { get; set; }
        public int ProcessedEvents { get; set; }
        public string CurrentEvent { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int PercentComplete { get; set; }
    }
}
