using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EasyOps.Pages
{
    public class ReplayModel : PageModel
    {
        private readonly ILogger<ReplayModel> _logger;

        public ReplayModel(ILogger<ReplayModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            _logger.LogInformation("Replay page accessed");
        }
    }
}
