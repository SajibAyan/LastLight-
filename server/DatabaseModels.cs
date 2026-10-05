using Microsoft.EntityFrameworkCore;

namespace Lastlight;

// ──────────────────────────────────────────────
// EF Core database context
// ──────────────────────────────────────────────
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PlayerStat>  PlayerStats  => Set<PlayerStat>();
    public DbSet<MatchRecord> MatchRecords => Set<MatchRecord>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<PlayerStat>().HasIndex(p => p.Name).IsUnique();
    }
}

// ──────────────────────────────────────────────
// Per-player lifetime stats (upserted after each match)
// ──────────────────────────────────────────────
public sealed class PlayerStat
{
    public int      Id          { get; set; }
    public string   Name        { get; set; } = "";
    public int      Wins        { get; set; }
    public int      Kills       { get; set; }
    public int      GamesPlayed { get; set; }
    public DateTime LastSeen    { get; set; } = DateTime.UtcNow;
}

// ──────────────────────────────────────────────
// One record per completed match
// ──────────────────────────────────────────────
public sealed class MatchRecord
{
    public int      Id              { get; set; }
    public string   RoomCode        { get; set; } = "";
    public string?  WinnerName      { get; set; }
    public double   DurationSeconds { get; set; }
    /// <summary>Comma-separated player names, e.g. "Alice,Bob,Carol,Dave,Eve"</summary>
    public string   PlayerNames     { get; set; } = "";
    /// <summary>Comma-separated kill counts matching PlayerNames order</summary>
    public string   PlayerKills     { get; set; } = "";
    public DateTime PlayedAt        { get; set; } = DateTime.UtcNow;
}
