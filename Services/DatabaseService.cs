using EasyOps.Models;
using Microsoft.EntityFrameworkCore;

namespace EasyOps.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DatabaseService> _logger;

        public DatabaseService(AppDbContext context, ILogger<DatabaseService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Initializing database...");

                // Create database if it doesn't exist
                await _context.Database.EnsureCreatedAsync();

                // Seed initial data if tables are empty
                await SeedInitialDataAsync();

                _logger.LogInformation("Database initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing database");
                throw;
            }
        }

        private async Task SeedInitialDataAsync()
        {
            // Seed monorepos if none exist
            if (!await _context.Monorepos.AnyAsync())
            {
                _logger.LogInformation("Seeding initial monorepo data...");
                var monorepos = new List<Monorepo>
                {
                    new Monorepo { Name = "SMF AFL", JobPath = "sb-rtp-sports-afl", Description = "AFL Sports applications and services" },
                    new Monorepo { Name = "Legacy AFL", JobPath = "sb-rtp-sports-afl-legacy", Description = "AFL Legacy Sports applications and services" },
                    new Monorepo { Name = "Cricket", JobPath = "sb-rtp-sports-cricket", Description = "Cricket Sports applications and services" },
                    new Monorepo { Name = "NRL", JobPath = "sb-rtp-sports-rl", Description = "NRL Sports applications and services" },
                    new Monorepo { Name = "Minor Sports", JobPath = "sb-rtp-sports-minor", Description = "Minor Sports applications and services" }
                };

                await _context.Monorepos.AddRangeAsync(monorepos);
                await _context.SaveChangesAsync();
            }

            // Seed environments if none exist
            if (!await _context.Environments.AnyAsync())
            {
                _logger.LogInformation("Seeding initial environment data...");
                var environments = new List<AwsEnvironment>
                {
                    new AwsEnvironment
                    {
                        Name = "Development",
                        EnvironmentType = "Development",
                        AwsProfile = "dev",
                        AccountId = "668885027178",
                        SamlRole = "arn:aws:iam::668885027178:role/cloud-saml-ts-dev-developer",
                        Description = "Development AWS Account",
                        IsDefault = true
                    },
                    new AwsEnvironment
                    {
                        Name = "Staging",
                        EnvironmentType = "Staging",
                        AwsProfile = "stg",
                        AccountId = "442532169006",
                        SamlRole = "arn:aws:iam::123456789012:role/cloud-saml-ts-stg-developer",
                        Description = "Staging AWS Account",
                        IsDefault = false
                    },
                    new AwsEnvironment
                    {
                        Name = "Production",
                        EnvironmentType = "Production",
                        AwsProfile = "prd",
                        AccountId = "987654321098",
                        SamlRole = "arn:aws:iam::987654321098:role/cloud-saml-ts-prd-support",
                        Description = "Production AWS Account",
                        IsDefault = false
                    }
                };

                await _context.Environments.AddRangeAsync(environments);
                await _context.SaveChangesAsync();
            }

            // Seed clusters if none exist
            if (!await _context.Clusters.AnyAsync())
            {
                _logger.LogInformation("Seeding initial cluster data...");
                var devEnv = await _context.Environments.FirstOrDefaultAsync(e => e.EnvironmentType == "Development");
                var stgEnv = await _context.Environments.FirstOrDefaultAsync(e => e.EnvironmentType == "Staging");
                var prdEnv = await _context.Environments.FirstOrDefaultAsync(e => e.EnvironmentType == "Production");

                if (devEnv != null && stgEnv != null && prdEnv != null)
                {
                    var clusters = new List<Cluster>
                    {
                        new Cluster { Name = "AFL DEV Environment", ClusterName = "sb-rtp-sports-afl-shared-ecs-dev", EnvironmentId = devEnv.Id, Description = "Development environment ECS cluster", AwsProfile = "dev", AccountId = "668885027178" },
                        new Cluster { Name = "AFL STG Environment", ClusterName = "sb-rtp-sports-afl-shared-ecs-stg", EnvironmentId = stgEnv.Id, Description = "Staging environment ECS cluster", AwsProfile = "stg", AccountId = "442532169006" },
                        new Cluster { Name = "AFL PRD Environment", ClusterName = "sb-rtp-sports-afl-shared-ecs-prd", EnvironmentId = prdEnv.Id, Description = "Production environment ECS cluster", AwsProfile = "prd", AccountId = "987654321098" },
                        new Cluster { Name = "Cricket DEV Environment", ClusterName = "cricket-ecs-dev", EnvironmentId = devEnv.Id, Description = "Development environment ECS cluster", AwsProfile = "dev", AccountId = "668885027178" },
                        new Cluster { Name = "NRL Dev Environment", ClusterName = "smf-rl-ecs-dev", EnvironmentId = devEnv.Id, Description = "Development environment ECS cluster", AwsProfile = "dev", AccountId = "668885027178" },
                        new Cluster { Name = "Cricket STG Environment", ClusterName = "cricket-ecs-stg", EnvironmentId = stgEnv.Id, Description = "Staging environment ECS cluster", AwsProfile = "stg", AccountId = "442532169006" },
                        new Cluster { Name = "NRL STG Environment", ClusterName = "smf-rl-ecs-stg", EnvironmentId = stgEnv.Id, Description = "Staging environment ECS cluster", AwsProfile = "stg", AccountId = "442532169006" },
                        new Cluster { Name = "Cricket PRD Environment", ClusterName = "cricket-ecs-prd", EnvironmentId = prdEnv.Id, Description = "Production environment ECS cluster", AwsProfile = "prd", AccountId = "987654321098" },
                        new Cluster { Name = "NRL PRD Environment", ClusterName = "smf-rl-ecs-prd", EnvironmentId = prdEnv.Id, Description = "Production environment ECS cluster", AwsProfile = "prd", AccountId = "987654321098" }
                    };

                    await _context.Clusters.AddRangeAsync(clusters);
                    await _context.SaveChangesAsync();
                }
            }
        }

        public async Task<List<Monorepo>> GetMonoreposAsync()
        {
            return await _context.Monorepos
                .Include(m => m.Clusters)
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<List<AwsEnvironment>> GetEnvironmentsAsync()
        {
            return await _context.Environments
                .Include(e => e.Clusters)
                .OrderBy(e => e.Name)
                .ToListAsync();
        }

        public async Task<List<Cluster>> GetClustersAsync()
        {
            return await _context.Clusters
                .Include(c => c.Environment)
                .Include(c => c.Monorepo)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<List<ClusterWithEnvironment>> GetClustersWithEnvironmentAsync()
        {
            return await _context.Clusters
                .Join(_context.Environments,
                    c => c.EnvironmentId,
                    e => e.Id,
                    (c, e) => new ClusterWithEnvironment
                    {
                        Id = c.Id,
                        Name = c.Name,
                        ClusterName = c.ClusterName,
                        Description = c.Description,
                        AwsProfile = c.AwsProfile,
                        AccountId = c.AccountId,
                        EnvironmentId = c.EnvironmentId,
                        EnvironmentType = e.EnvironmentType,
                        MonorepoId = c.MonorepoId
                    })
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Monorepo?> GetMonorepoByIdAsync(int id)
        {
            return await _context.Monorepos
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<AwsEnvironment?> GetEnvironmentByIdAsync(int id)
        {
            return await _context.Environments
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<Cluster?> GetClusterByIdAsync(int id)
        {
            return await _context.Clusters
                .Include(c => c.Environment)
                .Include(c => c.Monorepo)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task CreateMonorepoAsync(Monorepo monorepo)
        {
            _context.Monorepos.Add(monorepo);
            await _context.SaveChangesAsync();
        }

        public async Task CreateEnvironmentAsync(AwsEnvironment environment)
        {
            _context.Environments.Add(environment);
            await _context.SaveChangesAsync();
        }

        public async Task CreateClusterAsync(Cluster cluster)
        {
            _context.Clusters.Add(cluster);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateMonorepoAsync(Monorepo monorepo)
        {
            _context.Monorepos.Update(monorepo);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateEnvironmentAsync(AwsEnvironment environment)
        {
            _context.Environments.Update(environment);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateClusterAsync(Cluster cluster)
        {
            _context.Clusters.Update(cluster);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteMonorepoAsync(int id)
        {
            var monorepo = await _context.Monorepos.FindAsync(id);
            if (monorepo != null)
            {
                _context.Monorepos.Remove(monorepo);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteEnvironmentAsync(int id)
        {
            var environment = await _context.Environments.FindAsync(id);
            if (environment != null)
            {
                _context.Environments.Remove(environment);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteClusterAsync(int id)
        {
            var cluster = await _context.Clusters.FindAsync(id);
            if (cluster != null)
            {
                _context.Clusters.Remove(cluster);
                await _context.SaveChangesAsync();
            }
        }
    }
}