using Bacs.Archive.Web.Backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bacs.Archive.Web.Backend.Contexts
{
    public class ProblemsDbContext : DbContext
    {
        public ProblemsDbContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TestGroup>()
                .HasMany(x => x.Tests)
                .WithOne(x => x.TestGroup)
                .HasForeignKey(x => x.TestGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        public DbSet<Entities.Problem> Problems { get; set; }
        public DbSet<TestGroup> TestGroups { get; set; }
        public DbSet<Test> Tests { get; set; }
        public DbSet<CacheRevision> CacheRevisions { get; set; }
    }
}