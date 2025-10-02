using Microsoft.AspNetCore.Mvc.RazorPages;
using EasyOps.Services;
using EasyOps.Models;
using EasyOps.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace EasyOps.Pages.Admin
{
    public class IndexModel : PageModel
    {
        private readonly IDatabaseService _databaseService;

        public IndexModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public List<Monorepo> Monorepos { get; set; } = new();
        public List<AwsEnvironment> Environments { get; set; } = new();
        public List<Cluster> Clusters { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            Monorepos = await _databaseService.GetMonoreposAsync();
            Environments = await _databaseService.GetEnvironmentsAsync();
            Clusters = await _databaseService.GetClustersAsync();
        }

        // Monorepo CRUD operations
        public async Task<IActionResult> OnPostCreateMonorepoAsync(string name, string jobPath, string description)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(jobPath))
            {
                TempData["Error"] = "Name and Job Path are required.";
                return RedirectToPage();
            }

            var monorepo = new Monorepo
            {
                Name = name.Trim(),
                JobPath = jobPath.Trim(),
                Description = description?.Trim() ?? ""
            };

            await _databaseService.CreateMonorepoAsync(monorepo);
            TempData["Success"] = "Monorepo created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateMonorepoAsync(int id, string name, string jobPath, string description)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(jobPath))
            {
                TempData["Error"] = "Name and Job Path are required.";
                return RedirectToPage();
            }

            var monorepo = await _databaseService.GetMonorepoByIdAsync(id);
            if (monorepo == null)
            {
                TempData["Error"] = "Monorepo not found.";
                return RedirectToPage();
            }

            monorepo.Name = name.Trim();
            monorepo.JobPath = jobPath.Trim();
            monorepo.Description = description?.Trim() ?? "";

            await _databaseService.UpdateMonorepoAsync(monorepo);
            TempData["Success"] = "Monorepo updated successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteMonorepoAsync(int id)
        {
            var monorepo = await _databaseService.GetMonorepoByIdAsync(id);
            if (monorepo == null)
            {
                TempData["Error"] = "Monorepo not found.";
                return RedirectToPage();
            }

            await _databaseService.DeleteMonorepoAsync(id);
            TempData["Success"] = "Monorepo deleted successfully.";
            return RedirectToPage();
        }

        // Environment CRUD operations
        public async Task<IActionResult> OnPostCreateEnvironmentAsync(string name, string environmentType, string awsProfile, string accountId, string samlRole, string description, bool isDefault)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(environmentType) || string.IsNullOrWhiteSpace(awsProfile))
            {
                TempData["Error"] = "Name, Environment Type, and AWS Profile are required.";
                return RedirectToPage();
            }

            var environment = new AwsEnvironment
            {
                Name = name.Trim(),
                EnvironmentType = environmentType.Trim(),
                AwsProfile = awsProfile.Trim(),
                AccountId = accountId?.Trim() ?? "",
                SamlRole = samlRole?.Trim() ?? "",
                Description = description?.Trim() ?? "",
                IsDefault = isDefault
            };

            await _databaseService.CreateEnvironmentAsync(environment);
            TempData["Success"] = "Environment created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateEnvironmentAsync(int id, string name, string environmentType, string awsProfile, string accountId, string samlRole, string description, bool isDefault)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(environmentType) || string.IsNullOrWhiteSpace(awsProfile))
            {
                TempData["Error"] = "Name, Environment Type, and AWS Profile are required.";
                return RedirectToPage();
            }

            var environment = await _databaseService.GetEnvironmentByIdAsync(id);
            if (environment == null)
            {
                TempData["Error"] = "Environment not found.";
                return RedirectToPage();
            }

            environment.Name = name.Trim();
            environment.EnvironmentType = environmentType.Trim();
            environment.AwsProfile = awsProfile.Trim();
            environment.AccountId = accountId?.Trim() ?? "";
            environment.SamlRole = samlRole?.Trim() ?? "";
            environment.Description = description?.Trim() ?? "";
            environment.IsDefault = isDefault;

            await _databaseService.UpdateEnvironmentAsync(environment);
            TempData["Success"] = "Environment updated successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteEnvironmentAsync(int id)
        {
            var environment = await _databaseService.GetEnvironmentByIdAsync(id);
            if (environment == null)
            {
                TempData["Error"] = "Environment not found.";
                return RedirectToPage();
            }

            await _databaseService.DeleteEnvironmentAsync(id);
            TempData["Success"] = "Environment deleted successfully.";
            return RedirectToPage();
        }

        // Cluster CRUD operations
        public async Task<IActionResult> OnPostCreateClusterAsync(string name, string clusterName, string description, string awsProfile, string accountId, int environmentId, int? monorepoId)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(clusterName) || environmentId <= 0)
            {
                TempData["Error"] = "Name, Cluster Name, and Environment are required.";
                return RedirectToPage();
            }

            var cluster = new Cluster
            {
                Name = name.Trim(),
                ClusterName = clusterName.Trim(),
                Description = description?.Trim() ?? "",
                AwsProfile = awsProfile?.Trim() ?? "",
                AccountId = accountId?.Trim() ?? "",
                EnvironmentId = environmentId,
                MonorepoId = monorepoId > 0 ? monorepoId : null
            };

            await _databaseService.CreateClusterAsync(cluster);
            TempData["Success"] = "Cluster created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateClusterAsync(int id, string name, string clusterName, string description, string awsProfile, string accountId, int environmentId, int? monorepoId)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(clusterName) || environmentId <= 0)
            {
                TempData["Error"] = "Name, Cluster Name, and Environment are required.";
                return RedirectToPage();
            }

            var cluster = await _databaseService.GetClusterByIdAsync(id);
            if (cluster == null)
            {
                TempData["Error"] = "Cluster not found.";
                return RedirectToPage();
            }

            cluster.Name = name.Trim();
            cluster.ClusterName = clusterName.Trim();
            cluster.Description = description?.Trim() ?? "";
            cluster.AwsProfile = awsProfile?.Trim() ?? "";
            cluster.AccountId = accountId?.Trim() ?? "";
            cluster.EnvironmentId = environmentId;
            cluster.MonorepoId = monorepoId > 0 ? monorepoId : null;

            await _databaseService.UpdateClusterAsync(cluster);
            TempData["Success"] = "Cluster updated successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteClusterAsync(int id)
        {
            var cluster = await _databaseService.GetClusterByIdAsync(id);
            if (cluster == null)
            {
                TempData["Error"] = "Cluster not found.";
                return RedirectToPage();
            }

            await _databaseService.DeleteClusterAsync(id);
            TempData["Success"] = "Cluster deleted successfully.";
            return RedirectToPage();
        }

        // GET handlers for AJAX calls
        public async Task<IActionResult> OnGetGetMonorepoAsync(int id)
        {
            var monorepo = await _databaseService.GetMonorepoByIdAsync(id);
            if (monorepo == null)
            {
                return NotFound();
            }
            
            var dto = new MonorepoDTO
            {
                Id = monorepo.Id,
                Name = monorepo.Name,
                Description = monorepo.Description,
                JobPath = monorepo.JobPath
            };
            
            return new JsonResult(dto);
        }

        public async Task<IActionResult> OnGetGetEnvironmentAsync(int id)
        {
            var environment = await _databaseService.GetEnvironmentByIdAsync(id);
            if (environment == null)
            {
                return NotFound();
            }
            
            var dto = new EnvironmentDTO
            {
                Id = environment.Id,
                Name = environment.Name,
                Description = environment.Description,
                EnvironmentType = environment.EnvironmentType,
                AwsProfile = environment.AwsProfile,
                AccountId = environment.AccountId,
                SamlRole = environment.SamlRole,
                IsDefault = environment.IsDefault
            };
            
            return new JsonResult(dto);
        }

        public async Task<IActionResult> OnGetGetClusterAsync(int id)
        {
            var cluster = await _databaseService.GetClusterByIdAsync(id);
            if (cluster == null)
            {
                return NotFound();
            }
            
            var dto = new ClusterDTO
            {
                Id = cluster.Id,
                Name = cluster.Name,
                ClusterName = cluster.ClusterName,
                Description = cluster.Description,
                AwsProfile = cluster.AwsProfile,
                AccountId = cluster.AccountId,
                EnvironmentId = cluster.EnvironmentId,
                MonorepoId = cluster.MonorepoId,
                EnvironmentName = cluster.Environment?.Name,
                MonorepoName = cluster.Monorepo?.Name
            };
            
            return new JsonResult(dto);
        }
    }
}