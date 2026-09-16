using Campfire.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Campfire.Api.Data;

public class CampfireDbContext(DbContextOptions<CampfireDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Content> Content => Set<Content>();
    public DbSet<MembershipTier> MembershipTiers => Set<MembershipTier>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<MembershipTier>(entity =>
        {
            entity.HasOne(t => t.Creator)
                .WithMany()
                .HasForeignKey(t => t.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(t => t.Price).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Content>(entity =>
        {
            entity.HasOne(c => c.Creator)
                .WithMany(u => u.Content)
                .HasForeignKey(c => c.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.RequiredTier)
                .WithMany(t => t.Content)
                .HasForeignKey(c => c.RequiredTierId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasOne(s => s.Member)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(s => s.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.Tier)
                .WithMany(t => t.Subscriptions)
                .HasForeignKey(s => s.TierId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
