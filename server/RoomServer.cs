using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;

namespace Lastlight;

public sealed class Peer(WebSocket socket)
{
    public WebSocket Socket { get; } = socket;
    public Player   Player  { get; } = new();
    public Room?    Room    { get; set; }
    public Channel<byte[]> Outbox { get; } = Channel.CreateBounded<byte[]>(
        new BoundedChannelOptions(3) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public void Send(object value) =>
        Outbox.Writer.TryWrite(JsonSerializer.SerializeToUtf8Bytes(value, RoomServer.Json));

    public async Task Pump(CancellationToken cancellation)
    {
        try
        {
            await foreach (var bytes in Outbox.Reader.ReadAllAsync(cancellation))
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                deadline.CancelAfter(TimeSpan.FromSeconds(5));
                await Socket.SendAsync(bytes, WebSocketMessageType.Text, true, deadline.Token);
            }
        }
        catch (Exception e) when
            (e is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            Socket.Abort();
        }
    }
}

public sealed class Room(string code)
{
    public string Code    { get; } = code;
    public object Gate    { get; } = new();
    public Game   Game    { get; } = new();
    public List<Peer> Peers { get; } = [];
    public long Created   { get; } = Environment.TickCount64;
    /// <summary>True once the match result has been persisted to the DB.</summary>
    public bool Saved     { get; set; }

    public void Broadcast()
    {
        foreach (var peer in Peers)
            peer.Send(new { type = "state", code = Code, you = peer.Player.Id, host = Peers[0].Player.Id, state = Game.Snapshot(peer.Player) });
    }
}

public sealed class RoomServer : BackgroundService
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, Room> rooms = new();
    private readonly object creationGate = new();

    // ─────────────────────────────────────────────────────────
    // Handle incoming WebSocket messages
    // ─────────────────────────────────────────────────────────
    public void Handle(Peer peer, Input input)
    {
        if (input.Type == "input")
        {
            if (peer.Room is not { } room || !input.IsValid()) return;
            input.Clamp();
            lock (room.Gate) { peer.Player.Input = input; peer.Player.LastInput = Environment.TickCount64; }
            return;
        }

        if (peer.Room is not null)
        {
            peer.Send(new { type = "error", message = "You are already in a room." });
            return;
        }

        if (input.Type != "create" && input.Type != "join")
        {
            peer.Send(new { type = "error", message = "Create or join a room first." });
            return;
        }

        var name = new string(input.Name.Where(c => !char.IsControl(c)).Take(18).ToArray()).Trim();
        if (name.Length == 0) { peer.Send(new { type = "error", message = "Choose a player name." }); return; }
        peer.Player.Name = name;

        Room target;
        if (input.Type == "create")
        {
            lock (creationGate)
            {
                if (rooms.Count >= 100)
                {
                    peer.Send(new { type = "error", message = "The game server is full. Please try again later." });
                    return;
                }
                string code;
                do { code = Convert.ToHexString(RandomNumberGenerator.GetBytes(3)); } while (rooms.ContainsKey(code));
                target = new Room(code);
                rooms[code] = target;
            }
        }
        else
        {
            if (input.Code.Length != 6 || !rooms.TryGetValue(input.Code.ToUpperInvariant(), out var found))
            {
                peer.Send(new { type = "error", message = "That room does not exist or has expired." });
                return;
            }
            target = found;
        }

        lock (target.Gate)
        {
            if (target.Game.State.Players.Count >= 5)
            {
                peer.Send(new { type = "error", message = "This room is full. Only five players can enter." });
                return;
            }
            if (target.Game.State.Status != "waiting")
            {
                peer.Send(new { type = "error", message = "This match has already started." });
                return;
            }
            peer.Room = target;
            target.Peers.Add(peer);
            target.Game.State.Players.Add(peer.Player);
            target.Game.Event($"{name} entered the grove.");
            target.Game.Start();
            target.Broadcast();
        }
    }

    // ─────────────────────────────────────────────────────────
    // Called when a peer disconnects — save results if match is over
    // ─────────────────────────────────────────────────────────
    public void Remove(Peer peer, IServiceScopeFactory scopeFactory)
    {
        if (peer.Room is not { } room) return;

        bool shouldSave = false;
        lock (room.Gate)
        {
            room.Peers.Remove(peer);
            if (room.Game.State.Status == "waiting")
                room.Game.State.Players.Remove(peer.Player);
            else
            {
                peer.Player.Alive  = false;
                peer.Player.Hp     = 0;
                peer.Player.Input  = new();
            }

            room.Game.Event($"{peer.Player.Name} left the grove.");

            if (room.Peers.Count == 0)
            {
                rooms.TryRemove(room.Code, out _);
                // Save only finished matches, and only once
                if (room.Game.State.Status == "finished" && !room.Saved)
                {
                    room.Saved = true;
                    shouldSave = true;
                }
            }
            else
            {
                room.Broadcast();
                // If the match just finished (last player alive), save immediately
                if (room.Game.State.Status == "finished" && !room.Saved)
                {
                    room.Saved = true;
                    shouldSave = true;
                }
            }
        }

        peer.Room = null;

        if (shouldSave)
            _ = Task.Run(() => PersistMatchAsync(room, scopeFactory));
    }

    // ─────────────────────────────────────────────────────────
    // Persist match result + update player stats in SQLite
    // ─────────────────────────────────────────────────────────
    private static async Task PersistMatchAsync(Room room, IServiceScopeFactory scopeFactory)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var state   = room.Game.State;
            var players = state.Players;
            var winnerId  = state.Winner;
            var winnerPlayer = players.Find(p => p.Id == winnerId);

            // ─ Insert match record ─────────────────────────────────────
            var match = new MatchRecord
            {
                RoomCode        = room.Code,
                WinnerName      = winnerPlayer?.Name,
                DurationSeconds = state.Time,
                PlayerNames     = string.Join(",", players.Select(p => p.Name)),
                PlayerKills     = string.Join(",", players.Select(p => p.Kills.ToString())),
                PlayedAt        = DateTime.UtcNow,
            };
            db.MatchRecords.Add(match);

            // ─ Upsert player stats ───────────────────────────────────
            foreach (var player in players)
            {
                if (player.Bot) continue; // don't track bots
                var stat = await db.PlayerStats.FirstOrDefaultAsync(s => s.Name == player.Name);
                if (stat is null)
                {
                    stat = new PlayerStat { Name = player.Name };
                    db.PlayerStats.Add(stat);
                }
                stat.GamesPlayed++;
                stat.Kills    += player.Kills;
                stat.LastSeen  = DateTime.UtcNow;
                if (player.Id == winnerId) stat.Wins++;
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Non-fatal — log and continue; the game still ran fine
            Console.Error.WriteLine($"[DB] Failed to persist match {room.Code}: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────
    // Game loop — runs every 40 ms (25 FPS)
    // ─────────────────────────────────────────────────────────
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(40));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var room in rooms.Values)
                {
                    lock (room.Gate)
                    {
                        // Expire rooms older than 30 minutes
                        if (Environment.TickCount64 - room.Created > 30 * 60 * 1000)
                        {
                            foreach (var p in room.Peers) p.Socket.Abort();
                            rooms.TryRemove(room.Code, out _);
                            continue;
                        }
                        room.Game.Tick(.04);
                        room.Broadcast();
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
