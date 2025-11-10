using EasyOps.Shared.Models;

namespace EasyOps.Shared.Services;

public interface IJenkinsService
{
    Task<JenkinsJobResult> ExecuteJobAsync(ExecuteJobRequest request);
    Task<BuildStatusResult> GetBuildStatusAsync(string project, string branch, string monorepo);
    Task<List<ProjectMapping>> GetProjectMappingsAsync();
    Task<List<ProjectInfo>> GetProjectsFromJenkinsAsync(string monorepo);
    Task<List<string>> GetAvailableMonoreposAsync();
}
