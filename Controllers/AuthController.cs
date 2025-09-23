using EasyOps.Models;
using EasyOps.Services;
using Microsoft.AspNetCore.Mvc;

namespace EasyOps.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthenticationService _authService;

        public AuthController(IAuthenticationService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AuthRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.ApiToken))
            {
                return BadRequest(new AuthResponse { Success = false, Message = "Username and API token are required" });
            }

            var result = await _authService.ValidateCredentialsAsync(request.Username, request.ApiToken);
            
            if (result.Success)
            {
                _authService.SetUserCredentials(HttpContext, request.Username, request.ApiToken);
            }

            return Ok(result);
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            _authService.ClearUserCredentials(HttpContext);
            return Ok(new AuthResponse { Success = true, Message = "Logged out successfully" });
        }

        [HttpGet("status")]
        public IActionResult GetAuthStatus()
        {
            var isAuthenticated = _authService.IsAuthenticated(HttpContext);
            var credentials = _authService.GetCurrentUserCredentials(HttpContext);
            
            return Ok(new 
            { 
                isAuthenticated = isAuthenticated,
                username = credentials?.Username ?? ""
            });
        }
    }
}
