using Microsoft.EntityFrameworkCore;
using SkillIssue.Domain;

namespace SkillIssue.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Repo> Repos => Set<Repo>();
    public DbSet<Bug> Bugs => Set<Bug>();
    public DbSet<HintTier> HintTiers => Set<HintTier>();
    public DbSet<Attempt> Attempts => Set<Attempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.GitHubId)
            .IsUnique();

        modelBuilder.Entity<HintTier>()
            .HasOne(h => h.Bug)
            .WithMany(b => b.Hints)
            .HasForeignKey(h => h.BugId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Attempt>()
            .HasOne(a => a.User)
            .WithMany(u => u.Attempts)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Attempt>()
            .HasOne(a => a.Bug)
            .WithMany(b => b.Attempts)
            .HasForeignKey(a => a.BugId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
