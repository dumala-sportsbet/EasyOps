using EasyOps.Models;

namespace EasyOps.Services
{
    public interface IDatabaseService
    {
        Task InitializeDatabaseAsync();
        Task<List<Monorepo>> GetMonoreposAsync();
        Task<List<AwsEnvironment>> GetEnvironmentsAsync();
        Task<List<Cluster>> GetClustersAsync();
        Task<List<ClusterWithEnvironment>> GetClustersWithEnvironmentAsync();
        Task<Monorepo?> GetMonorepoByIdAsync(int id);
        Task<AwsEnvironment?> GetEnvironmentByIdAsync(int id);
        Task<Cluster?> GetClusterByIdAsync(int id);
        Task CreateMonorepoAsync(Monorepo monorepo);
        Task CreateEnvironmentAsync(AwsEnvironment environment);
        Task CreateClusterAsync(Cluster cluster);
        Task UpdateMonorepoAsync(Monorepo monorepo);
        Task UpdateEnvironmentAsync(AwsEnvironment environment);
        Task UpdateClusterAsync(Cluster cluster);
        Task DeleteMonorepoAsync(int id);
        Task DeleteEnvironmentAsync(int id);
        Task DeleteClusterAsync(int id);
    }
}