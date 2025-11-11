using Confluent.Kafka;
using EasyOps.DTOs;
using EasyOps.Models;
using EasyOps.Services.Kafka;
using Flutter.Smf;
using Flutter.Smf.Afl;
using Flutter.Smf.Afl.Ec;
using Flutter.Smf.Feeds.Afl;
using Flutter.Smf.Se.Game.Afl;
using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Npgsql;
using System;
using System.Text.Json;
using static Confluent.Kafka.ConfigPropertyNames;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace EasyOps.Services
{
    public interface IReplayService
    {
        Task<FetchGameResponse> FetchGameFromProdAsync(FetchGameRequest request, string username);
        Task<List<SavedGameDto>> GetSavedGamesAsync();
        Task<SavedGameDto?> GetSavedGameByIdAsync(int replayGameId);
        Task<ReplayExecutionResponse> ExecuteReplayAsync(ReplayExecutionRequest request);
        Task<bool> DeleteSavedGameAsync(int replayGameId);
    }

    public class ReplayService : IReplayService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReplayService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IIdentityManagementService _identityManagementService;
        private readonly IPublisherService _publisherService;

        // Newly added variable
        private string _newGameId = "";
        private string _newRampId = "";

        public ReplayService(AppDbContext context, ILogger<ReplayService> logger, IConfiguration configuration, IIdentityManagementService identityManagementService, IPublisherService publisherService)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _identityManagementService = identityManagementService;
            _publisherService = publisherService;
        }

        /// <summary>
        /// Fetches game data from production database and saves it locally
        /// </summary>
        public async Task<FetchGameResponse> FetchGameFromProdAsync(FetchGameRequest request, string username)
        {
            try
            {
                _logger.LogInformation("Fetching game {GameId} from production", request.GameId);

                // TODO: Implement actual production DB query
                // For now, this is a placeholder that will return sample data
                var events = await FetchEventsFromProdDbAsync(request.GameId);

                // Check if game already exists
                var existingGame = await _context.ReplayGames
                    .FirstOrDefaultAsync(g => g.GameId == request.GameId);

                if (existingGame != null)
                {
                    return new FetchGameResponse
                    {
                        Success = false,
                        Message = $"Game {request.GameId} already exists in the database. Please delete it first or use a different ID.",
                        ReplayGameId = existingGame.Id,
                        EventCount = existingGame.TotalEvents
                    };
                }

                // Create new replay game
                var replayGame = new ReplayGame
                {
                    GameId = request.GameId,
                    GameName = request.GameName,
                    FetchedAt = DateTime.UtcNow,
                    FetchedBy = username,
                    TotalEvents = events.Count,
                    Notes = request.Notes
                };

                // Add events
                foreach (var eventDto in events)
                {
                    replayGame.Events.Add(new ReplayGameEvent
                    {
                        EventIdentifier = eventDto.EventIdentifier,
                        Sequence = eventDto.Sequence,
                        Payload = eventDto.Payload,
                        PayloadType = eventDto.PayloadType,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                _context.ReplayGames.Add(replayGame);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully fetched and saved game {GameId} with {EventCount} events",
                    request.GameId, events.Count);

                return new FetchGameResponse
                {
                    Success = true,
                    Message = $"Successfully fetched {events.Count} events for game {request.GameId}",
                    ReplayGameId = replayGame.Id,
                    EventCount = events.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching game {GameId} from production", request.GameId);
                return new FetchGameResponse
                {
                    Success = false,
                    Message = $"Error fetching game: {ex.Message}",
                    EventCount = 0
                };
            }
        }

        /// <summary>
        /// Gets all saved games from the local database
        /// </summary>
        public async Task<List<SavedGameDto>> GetSavedGamesAsync()
        {
            return await _context.ReplayGames
                .OrderByDescending(g => g.FetchedAt)
                .Select(g => new SavedGameDto
                {
                    Id = g.Id,
                    GameId = g.GameId,
                    GameName = g.GameName,
                    FetchedAt = g.FetchedAt,
                    FetchedBy = g.FetchedBy,
                    TotalEvents = g.TotalEvents,
                    Notes = g.Notes
                })
                .ToListAsync();
        }

        /// <summary>
        /// Gets a specific saved game by ID
        /// </summary>
        public async Task<SavedGameDto?> GetSavedGameByIdAsync(int replayGameId)
        {
            return await _context.ReplayGames
                .Where(g => g.Id == replayGameId)
                .Select(g => new SavedGameDto
                {
                    Id = g.Id,
                    GameId = g.GameId,
                    GameName = g.GameName,
                    FetchedAt = g.FetchedAt,
                    FetchedBy = g.FetchedBy,
                    TotalEvents = g.TotalEvents,
                    Notes = g.Notes
                })
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Executes a replay of a saved game
        /// </summary>
        public async Task<ReplayExecutionResponse> ExecuteReplayAsync(ReplayExecutionRequest request)
        {
            var response = new ReplayExecutionResponse
            {
                StartedAt = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Starting replay for game ID {ReplayGameId}", request.ReplayGameId);

                // Get the game and its events
                var game = await _context.ReplayGames
                    .Include(g => g.Events)
                    .FirstOrDefaultAsync(g => g.Id == request.ReplayGameId);

                if (game == null)
                {
                    response.Success = false;
                    response.Message = "Game not found";
                    return response;
                }

                // Get events ordered by sequence (try numeric first, then text)
                var events = game.Events
                    .OrderBy(e => int.TryParse(e.Sequence, out var seq) ? seq : int.MaxValue)
                    .ThenBy(e => e.Sequence)
                    .ToList();

                _logger.LogInformation("Processing {EventCount} events for game {GameId}",
                    events.Count, game.GameId);

                // TODO: Implement actual replay logic
                // For now, this is a placeholder
                //validate the events
                var gameCreateEvent = events.FirstOrDefault(e => e.PayloadType == "flutter.smf.se.game.afl.AflGameScheduled");
                if (gameCreateEvent == null)
                {
                    throw new InvalidOperationException($"No game creation event found for game ID: {game.GameId}");
                }
                var tradingOpCreatedEvent = events.FirstOrDefault(e => e.PayloadType == "flutter.smf.se.game.afl.TradingOpinionCreated");

                if (tradingOpCreatedEvent == null)
                {
                    throw new InvalidOperationException($"No trading opinion creation event found for game ID: {game.GameId}");
                }

                foreach (var @event in events)
                {
                    await ProcessEventAsync(@event, request.ReplayGameId, request.GameStartDateTime, request.TargetEnvironment);
                }

                response.CompletedAt = DateTime.UtcNow;
                response.Success = response.EventsFailed == 0;
                response.Message = request.DryRun
                    ? $"Dry run completed. Would process {response.EventsProcessed} events."
                    : $"Replay completed. Processed {response.EventsProcessed} events, {response.EventsFailed} failed.";
                response.NewGameId = _newGameId;

                _logger.LogInformation("Replay completed for game {GameId}. Success: {Success}, NewGameId: {NewGameId}",
                    game.GameId, response.Success, _newGameId);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing replay for game ID {ReplayGameId}", request.ReplayGameId);
                response.Success = false;
                response.Message = $"Error executing replay: {ex.Message}";
                response.CompletedAt = DateTime.UtcNow;
                return response;
            }
        }

        /// <summary>
        /// Deletes a saved game and all its events
        /// </summary>
        public async Task<bool> DeleteSavedGameAsync(int replayGameId)
        {
            try
            {
                var game = await _context.ReplayGames
                    .Include(g => g.Events)
                    .FirstOrDefaultAsync(g => g.Id == replayGameId);

                if (game == null)
                {
                    return false;
                }

                _context.ReplayGames.Remove(game);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted replay game {GameId} (ID: {ReplayGameId})",
                    game.GameId, replayGameId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting replay game {ReplayGameId}", replayGameId);
                return false;
            }
        }

        /// <summary>
        /// Fetches events from production database
        /// Retrieves all events for a given event_identifier up to and including the GameStarted event
        /// </summary>
        private async Task<List<GameEventDto>> FetchEventsFromProdDbAsync(string eventIdentifier)
        {
            var events = new List<GameEventDto>();

            try
            {
                var connectionString = _configuration.GetConnectionString("ProductionDatabase");

                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogError("Production database connection string not configured");
                    throw new InvalidOperationException("Production database connection string 'ProductionDatabase' is not configured in appsettings.json");
                }

                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                _logger.LogInformation("Connected to production database, fetching events for identifier {EventIdentifier}", eventIdentifier);

                // First, find the GameStarted event to get the cutoff time
                var gameStartedQuery = @"
                    SELECT created_time_utc 
                    FROM events 
                    WHERE event_identifier = @eventIdentifier 
                      AND payload_type = 'flutter.smf.se.game.afl.GameStarted'
                    ORDER BY created_time_utc 
                    LIMIT 1";

                DateTime? gameStartedTime = null;

                using (var cmd = new NpgsqlCommand(gameStartedQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@eventIdentifier", eventIdentifier);

                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        gameStartedTime = Convert.ToDateTime(result);
                        _logger.LogInformation("Found GameStarted event at {GameStartedTime}", gameStartedTime);
                    }
                    else
                    {
                        _logger.LogWarning("No GameStarted event found for identifier {EventIdentifier}", eventIdentifier);
                        // If no GameStarted event, we'll fetch all events for this identifier
                    }
                }

                // Now fetch all events up to (and including) the GameStarted event
                var eventsQuery = gameStartedTime.HasValue
                    ? @"
                        SELECT id, event_identifier, sequence, payload, payload_type, created_time_utc
                        FROM events 
                        WHERE event_identifier = @eventIdentifier 
                          AND created_time_utc <= @gameStartedTime
                        ORDER BY sequence, created_time_utc"
                    : @"
                        SELECT id, event_identifier, sequence, payload, payload_type, created_time_utc
                        FROM events 
                        WHERE event_identifier = @eventIdentifier 
                        ORDER BY sequence, created_time_utc";

                using (var cmd = new NpgsqlCommand(eventsQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@eventIdentifier", eventIdentifier);
                    if (gameStartedTime.HasValue)
                    {
                        cmd.Parameters.AddWithValue("@gameStartedTime", gameStartedTime.Value);
                    }

                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var payloadType = reader.GetString(reader.GetOrdinal("payload_type"));

                        // Skip GameStarted events - we don't want to save these
                        if (payloadType == "flutter.smf.se.game.afl.GameStarted")
                        {
                            _logger.LogDebug("Skipping GameStarted event for identifier {EventIdentifier}", eventIdentifier);
                            continue;
                        }

                        var eventDto = new GameEventDto
                        {
                            EventIdentifier = reader.GetString(reader.GetOrdinal("event_identifier")),
                            Sequence = reader.GetString(reader.GetOrdinal("sequence")),
                            Payload = reader.IsDBNull(reader.GetOrdinal("payload"))
                                ? Array.Empty<byte>()
                                : (byte[])reader["payload"],
                            PayloadType = payloadType
                        };

                        events.Add(eventDto);
                    }
                }

                _logger.LogInformation("Fetched {EventCount} events from production database for identifier {EventIdentifier}",
                    events.Count, eventIdentifier);

                return events;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching events from production database for identifier {EventIdentifier}", eventIdentifier);
                throw new InvalidOperationException($"Failed to fetch events from production database: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Processes a single event during replay
        /// TODO: Implement actual event processing logic
        /// </summary>
        private async Task ProcessEventAsync(ReplayGameEvent replayEvent, int replayGameId, DateTime gameStartDateTime, string targetEnvironment)
        {
            await Task.Delay(10); // Simulate processing

            switch (replayEvent.PayloadType)
            {
                case "flutter.smf.se.game.afl.AflGameScheduled":
                    var referenceGameScheduled = AflGameScheduled.Parser.ParseFrom(replayEvent.Payload, 5, replayEvent.Payload.Length - 5);
                    if (referenceGameScheduled == null)
                    {
                        throw new InvalidOperationException($"Failed to parse game creation event for game ID: {replayEvent.ReplayGameId}");
                    }
                    var competitionId = referenceGameScheduled.Game.CompetitionId.Uid;
                    var homeTeamId = referenceGameScheduled.Game.Teams[0].Id.Uid;
                    var awayTeamId = referenceGameScheduled.Game.Teams[1].Id.Uid;

                    // Generate GameId and RampId
                    _newGameId = Guid.NewGuid().ToString();

                    var correlationId = Guid.NewGuid().ToString();

                    // Create a game
                    // Create game on the Alium Side 
                    var gameScheduled =
                        GenerateAflGameScheduled(_newGameId, homeTeamId, awayTeamId, competitionId, _newRampId, gameStartDateTime, correlationId);
                    string output = JsonConvert.SerializeObject(gameScheduled);

                    // Push game creation message for Alium
                    await _publisherService.PublishMessage(gameScheduled, "afl.gameui.events", _newGameId,
                        correlationId);

                    await Task.Delay(5000); // Ensure the game is created before proceeding

                    _newRampId = await _identityManagementService.GetRampId(_newGameId);

                    break;

                case "flutter.smf.se.game.afl.TradingOpinionCreated":
                    var tradingOpinionCorrelationId = Guid.NewGuid().ToString();

                    var referenceTradingOpCreated = Flutter.Smf.Se.Game.Afl.TradingOpinionCreated.Parser.ParseFrom(replayEvent.Payload, 5, replayEvent.Payload.Length - 5);

                    if (referenceTradingOpCreated == null)
                    {
                        throw new InvalidOperationException($"Failed to parse Trading Opinion creation event for game ID: {replayGameId}");
                    }

                    if (string.IsNullOrEmpty(_newGameId))
                    {
                        throw new InvalidOperationException($"Game ID not set. Ensure AflGameScheduled event is processed first.");
                    }

                    // Create trading opinion
                    var tradingOpinion = new FeedsTradingOpinionCreated
                    {
                        GameId = new Identifier
                        {
                            Uid = _newGameId,
                            ExternalIds = { new Identifier.Types.ExternalId { System = "ramp", Id = _newRampId } }
                        },

                        Handicap = new Handicap { Line = referenceTradingOpCreated.Handicap.Line },
                        TotalPoints = new TotalPoints { Line = referenceTradingOpCreated.TotalPoints.Line },
                        // TODO: Do we need to figure this out based on if we have players? Just default to 5? 
                        ProductOfferingLevel = new ProductOfferingLevel { Level = 2 },
                        ProductOfferingType = new ProductOfferingType { OfferingType = OfferingType.PrematchAndInRunning },

                        EventHeaders = new FeedsDomainEventHeaders
                        {
                            RecordHeaders = new RecordHeaders
                            {
                                LogicalClock = 1,
                                CorrelationId = tradingOpinionCorrelationId,
                                Origin = new RecordHeaders.Types.Source
                                {
                                    SourceId = RecordHeaders.Types.SourceId.Ui,
                                    Name = "EasyOps"
                                },
                                ProducedAt = DateTimeOffset.Now.ToTimestamp(),
                                OriginTimestamp = DateTimeOffset.Now.ToTimestamp(),
                            },
                            FeedsDomainEventId = new Identifier { Uid = Guid.NewGuid().ToString() }
                        }
                    };

                    await _publisherService.PublishMessage(tradingOpinion, "afl.gameui.events", _newGameId,
                        tradingOpinionCorrelationId);

                    await Task.Delay(1000); // Ensure the Trading Opinion is created before proceeding
                    break;
                case "flutter.smf.se.game.afl.TradingOpinionUpdated":

                    var tradingOpinionUpdateCorrelationId = Guid.NewGuid().ToString();

                    var referenceTradingOpUpdated = Flutter.Smf.Se.Game.Afl.TradingOpinionUpdated.Parser.ParseFrom(replayEvent.Payload, 5, replayEvent.Payload.Length - 5);

                    if (referenceTradingOpUpdated == null)
                    {
                        throw new InvalidOperationException($"Failed to parse Trading Opinion update event for game ID: {replayGameId}");
                    }
                    if (string.IsNullOrEmpty(_newGameId))
                    {
                        throw new InvalidOperationException($"Game ID not set. Ensure AflGameScheduled event is processed first.");
                    }
                    var tradingOpinionUpdated = new FeedsTradingOpinionUpdated
                    {
                        GameId = new Identifier
                        {
                            Uid = _newGameId,
                            ExternalIds = { new Identifier.Types.ExternalId { System = "ramp", Id = _newRampId } }
                        },
                        EventHeaders = new FeedsDomainEventHeaders
                        {
                            RecordHeaders = new RecordHeaders
                            {
                                LogicalClock = 1,
                                CorrelationId = tradingOpinionUpdateCorrelationId,
                                Origin = new RecordHeaders.Types.Source
                                {
                                    SourceId = RecordHeaders.Types.SourceId.Unspecified,
                                    Name = "EasyOps"
                                },
                                ProducedAt = DateTimeOffset.Now.ToTimestamp(),
                                OriginTimestamp = DateTimeOffset.Now.ToTimestamp()
                            },
                            FeedsDomainEventId = new Identifier { Uid = Guid.NewGuid().ToString() }
                        },
                        Updates =
                {
                    new TradingOpinionUpdate
                    {
                        ProductOfferingLevel = new ProductOfferingLevel { Level = 5 }
                    },
                }
                    };

                    await _publisherService.PublishMessage(tradingOpinionUpdated, "afl.gameui.events",
                        _newGameId,
                        tradingOpinionUpdateCorrelationId);
                    break;
                case "flutter.smf.se.game.afl.PlayersTradingOpinionUpdated":

                    var playerTradingOpinionCoorelationId = Guid.NewGuid().ToString();

                    // Get the game creation event payload
                    var referencePlayerTradingOpUpdated = Flutter.Smf.Se.Game.Afl.PlayersTradingOpinionUpdated.Parser.ParseFrom(replayEvent.Payload, 5, replayEvent.Payload.Length - 5);

                    if (referencePlayerTradingOpUpdated == null)
                    {
                        throw new InvalidOperationException($"Failed to parse Player Trading Opinion event for game ID: {replayGameId}");
                    }

                    // Create trading opinion
                    var playerTradingOpinion = new FeedsPlayersTradingOpinionUpdated
                    {
                        GameId = new Identifier
                        {
                            Uid = _newGameId,
                            ExternalIds = { new Identifier.Types.ExternalId { System = "ramp", Id = _newRampId } }
                        },

                        PlayersOfferingStatus = referencePlayerTradingOpUpdated.PlayersOfferingStatus,

                        PlayerTradingOpinionUpdates = { referencePlayerTradingOpUpdated.PlayerTradingOpinionUpdates },

                        EventHeaders = new FeedsDomainEventHeaders
                        {
                            RecordHeaders = new RecordHeaders
                            {
                                LogicalClock = referencePlayerTradingOpUpdated.EventHeaders.RecordHeaders.LogicalClock,
                                CorrelationId = playerTradingOpinionCoorelationId,
                                Origin = new RecordHeaders.Types.Source
                                {
                                    SourceId = RecordHeaders.Types.SourceId.Unspecified,
                                    Name = "EasyOps"
                                },
                                ProducedAt = DateTimeOffset.Now.ToTimestamp(),
                                OriginTimestamp = DateTimeOffset.Now.ToTimestamp(),
                            },
                            FeedsDomainEventId = new Identifier { Uid = Guid.NewGuid().ToString() }
                        }
                    };

                    await _publisherService.PublishMessage(playerTradingOpinion, "afl.gameui.events", _newGameId,
                        playerTradingOpinionCoorelationId);
                    break;
                default:
                    break;
            }
        }

        private FeedsGameScheduled GenerateAflGameScheduled(string newGameId,
    string homeTeamId, string awayTeamId, string competitionId, string rampId, DateTime gameStartDateTime, string correlationId)
        {
            return new FeedsGameScheduled
            {
                Game = new Flutter.Smf.Feeds.Afl.Game
                {
                    Teams =
                    {
                        new[]
                        {
                            new Flutter.Smf.Feeds.Afl.Team
                            {
                                Id = new Identifier { Uid = homeTeamId },
                                Type = TeamHostDesignation.Home
                            },
                            new Flutter.Smf.Feeds.Afl.Team
                            {
                                Id = new Identifier { Uid = awayTeamId },
                                Type = TeamHostDesignation.Away
                            }
                        }
                    },
                    Competition = new Competition { Id = new Identifier { Uid = competitionId } },
                    Id = new Identifier
                    {
                        Uid = newGameId,
                        //ExternalIds = { new Identifier.Types.ExternalId { System = "ramp", Id = rampId } }
                    },
                    StartTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(gameStartDateTime.ToUniversalTime()),
                    Venue = new Venue { Name = "ABC" }
                },
                EventHeaders = new FeedsDomainEventHeaders
                {
                    FeedsDomainEventId = new Identifier { Uid = Guid.NewGuid().ToString() },
                    RecordHeaders = new RecordHeaders
                    {
                        CorrelationId = correlationId,
                        LogicalClock = 1,
                        Origin = new RecordHeaders.Types.Source
                        { SourceId = RecordHeaders.Types.SourceId.Ui, Name = "EasyOps" },
                        ProducedAt = DateTimeOffset.Now.ToTimestamp(),
                        OriginTimestamp = DateTimeOffset.Now.ToTimestamp()
                    }
                }
            };
        }
    }
}
