using Microsoft.EntityFrameworkCore;

namespace EasyOps.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Monorepo> Monorepos { get; set; } = null!;
        public DbSet<AwsEnvironment> Environments { get; set; } = null!;
        public DbSet<Cluster> Clusters { get; set; } = null!;
        public DbSet<ReplayGame> ReplayGames { get; set; } = null!;
        public DbSet<ReplayGameEvent> ReplayGameEvents { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships
            modelBuilder.Entity<Cluster>()
                .HasOne(c => c.Environment)
                .WithMany(e => e.Clusters)
                .HasForeignKey(c => c.EnvironmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Cluster>()
                .HasOne(c => c.Monorepo)
                .WithMany(m => m.Clusters)
                .HasForeignKey(c => c.MonorepoId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure ReplayGame and ReplayGameEvent relationship
            modelBuilder.Entity<ReplayGameEvent>()
                .HasOne(e => e.ReplayGame)
                .WithMany(g => g.Events)
                .HasForeignKey(e => e.ReplayGameId)
                .OnDelete(DeleteBehavior.Cascade);

            // Seed data will be added via migration or initialization service
        }
    }
}