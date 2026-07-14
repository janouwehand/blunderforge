using BlunderForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlunderForge.Infrastructure.Persistence;

internal sealed class BlunderForgeDbContext(DbContextOptions<BlunderForgeDbContext> options) : DbContext(options)
{
    public DbSet<GameEntity> Games => Set<GameEntity>();

    public DbSet<MoveEntity> Moves => Set<MoveEntity>();

    public DbSet<MoveAnalysisEntity> MoveAnalyses => Set<MoveAnalysisEntity>();

    public DbSet<AppSettingEntity> AppSettings => Set<AppSettingEntity>();

    public DbSet<GameReviewEntity> GameReviews => Set<GameReviewEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameEntity>(entity =>
        {
            entity.ToTable("Games");
            entity.HasKey(game => game.Id);
            entity.Property(game => game.Status).HasMaxLength(32).IsRequired();
            entity.Property(game => game.PlayerColorChoice).HasMaxLength(32).IsRequired();
            entity.Property(game => game.PlayerSide).HasMaxLength(16).IsRequired();
            entity.Property(game => game.Result).HasMaxLength(32).IsRequired();
            entity.Property(game => game.TerminationReason).HasMaxLength(64).IsRequired();
            entity.Property(game => game.InitialFen).HasMaxLength(128).IsRequired();
            entity.Property(game => game.CurrentFen).HasMaxLength(128).IsRequired();
            entity.Property(game => game.Version).IsConcurrencyToken();
            entity.HasIndex(game => game.Status)
                .IsUnique()
                .HasFilter("Status = 'Active'");
        });

        modelBuilder.Entity<MoveEntity>(entity =>
        {
            entity.ToTable("Moves");
            entity.HasKey(move => move.Id);
            entity.Property(move => move.Color).HasMaxLength(16).IsRequired();
            entity.Property(move => move.San).HasMaxLength(32).IsRequired();
            entity.Property(move => move.Uci).HasMaxLength(8).IsRequired();
            entity.Property(move => move.FenBefore).HasMaxLength(128).IsRequired();
            entity.Property(move => move.FenAfter).HasMaxLength(128).IsRequired();
            entity.HasIndex(move => new { move.GameId, move.Ply })
                .IsUnique();
            entity.HasOne(move => move.Game)
                .WithMany(game => game.Moves)
                .HasForeignKey(move => move.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MoveAnalysisEntity>(entity =>
        {
            entity.ToTable("MoveAnalyses");
            entity.HasKey(analysis => analysis.Id);
            entity.Property(analysis => analysis.EngineVersion).HasMaxLength(128).IsRequired();
            entity.Property(analysis => analysis.Classification).HasMaxLength(32).IsRequired();
            entity.Property(analysis => analysis.BestMoveUci).HasMaxLength(8);
            entity.HasIndex(analysis => analysis.MoveId).IsUnique();
            entity.HasOne(analysis => analysis.Move)
                .WithOne(move => move.Analysis)
                .HasForeignKey<MoveAnalysisEntity>(analysis => analysis.MoveId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameReviewEntity>(entity =>
        {
            entity.ToTable("GameReviews");
            entity.HasKey(review => review.GameId);
            entity.Property(review => review.Result).HasMaxLength(32).IsRequired();
            entity.Property(review => review.OverallQuality).HasMaxLength(100).IsRequired();
            entity.Property(review => review.WentWell).HasMaxLength(1000).IsRequired();
            entity.Property(review => review.FutureFocus).HasMaxLength(1000).IsRequired();
            entity.HasOne(review => review.Game)
                .WithOne(game => game.Review)
                .HasForeignKey<GameReviewEntity>(review => review.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppSettingEntity>(entity =>
        {
            entity.ToTable("AppSettings");
            entity.HasKey(setting => setting.Key);
            entity.Property(setting => setting.Key).HasMaxLength(128);
        });

    }
}
