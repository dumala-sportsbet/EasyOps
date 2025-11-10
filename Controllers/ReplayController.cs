using EasyOps.DTOs;
using EasyOps.Services;
using Microsoft.AspNetCore.Mvc;

namespace EasyOps.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReplayController : ControllerBase
    {
        private readonly IReplayService _replayService;
        private readonly ILogger<ReplayController> _logger;

        public ReplayController(IReplayService replayService, ILogger<ReplayController> logger)
        {
            _replayService = replayService;
            _logger = logger;
        }

        /// <summary>
        /// Fetches game data from production database and saves it locally
        /// POST /api/replay/fetch
        /// </summary>
        [HttpPost("fetch")]
        public async Task<ActionResult<FetchGameResponse>> FetchGameFromProd([FromBody] FetchGameRequest request)
        {
            try
            {
                // TODO: Get username from authentication context
                var username = User.Identity?.Name ?? "system";

                var result = await _replayService.FetchGameFromProdAsync(request, username);
                
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FetchGameFromProd");
                return StatusCode(500, new FetchGameResponse
                {
                    Success = false,
                    Message = "An error occurred while fetching game data"
                });
            }
        }

        /// <summary>
        /// Gets all saved games
        /// GET /api/replay/games
        /// </summary>
        [HttpGet("games")]
        public async Task<ActionResult<List<SavedGameDto>>> GetSavedGames()
        {
            try
            {
                var games = await _replayService.GetSavedGamesAsync();
                return Ok(games);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSavedGames");
                return StatusCode(500, "An error occurred while retrieving saved games");
            }
        }

        /// <summary>
        /// Gets a specific saved game by ID
        /// GET /api/replay/games/{id}
        /// </summary>
        [HttpGet("games/{id}")]
        public async Task<ActionResult<SavedGameDto>> GetSavedGameById(int id)
        {
            try
            {
                var game = await _replayService.GetSavedGameByIdAsync(id);
                
                if (game == null)
                {
                    return NotFound($"Game with ID {id} not found");
                }

                return Ok(game);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSavedGameById for ID {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the game");
            }
        }

        /// <summary>
        /// Executes a replay for a saved game
        /// POST /api/replay/execute
        /// </summary>
        [HttpPost("execute")]
        public async Task<ActionResult<ReplayExecutionResponse>> ExecuteReplay([FromBody] ReplayExecutionRequest request)
        {
            try
            {
                var result = await _replayService.ExecuteReplayAsync(request);
                
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExecuteReplay for game ID {ReplayGameId}", request.ReplayGameId);
                return StatusCode(500, new ReplayExecutionResponse
                {
                    Success = false,
                    Message = "An error occurred while executing the replay"
                });
            }
        }

        /// <summary>
        /// Deletes a saved game
        /// DELETE /api/replay/games/{id}
        /// </summary>
        [HttpDelete("games/{id}")]
        public async Task<ActionResult> DeleteSavedGame(int id)
        {
            try
            {
                var result = await _replayService.DeleteSavedGameAsync(id);
                
                if (!result)
                {
                    return NotFound($"Game with ID {id} not found");
                }

                return Ok(new { message = "Game deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteSavedGame for ID {Id}", id);
                return StatusCode(500, "An error occurred while deleting the game");
            }
        }

        /// <summary>
        /// Validates connection to production database
        /// GET /api/replay/validate-connection
        /// </summary>
        [HttpGet("validate-connection")]
        public async Task<ActionResult> ValidateProductionConnection()
        {
            try
            {
                // TODO: Implement actual connection validation
                await Task.CompletedTask;
                
                return Ok(new { 
                    connected = false, 
                    message = "Production DB connection not yet configured" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating production connection");
                return StatusCode(500, new { 
                    connected = false, 
                    message = "Error validating connection" 
                });
            }
        }

        /// <summary>
        /// Gets the current AWS environment information
        /// GET /api/replay/current-environment
        /// </summary>
        [HttpGet("current-environment")]
        public ActionResult GetCurrentEnvironment()
        {
            try
            {
                var awsService = HttpContext.RequestServices.GetRequiredService<IAwsAuthenticationService>();
                var currentEnv = awsService.GetCurrentEnvironment();

                if (currentEnv == null)
                {
                    return Ok(new
                    {
                        environment = "dev",
                        environmentName = "Development",
                        message = "No environment detected, defaulting to Development"
                    });
                }

                return Ok(new
                {
                    environment = currentEnv.Environment?.ToLower() ?? "dev",
                    environmentName = currentEnv.Name,
                    profile = currentEnv.AwsProfile,
                    accountId = currentEnv.AccountId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current environment");
                return Ok(new
                {
                    environment = "dev",
                    environmentName = "Development",
                    message = "Error detecting environment, defaulting to Development"
                });
            }
        }
    }
}
