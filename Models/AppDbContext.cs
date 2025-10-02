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

            // Seed data will be added via migration or initialization service
        }
    }
}