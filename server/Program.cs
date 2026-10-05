using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using Lastlight;
using Microsoft.EntityFrameworkCore;

if (args.Contains("--self-test")) { GameTests.Run(); return; }

var builder = WebApplication.CreateBuilder(args);

// ── Database ────────────────────────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=arena.db";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connStr));

// ── Game services ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<RoomServer>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<RoomServer>());

var app = builder.Build();

// ── Auto-create / migrate the database on startup ───────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated(); // creates arena.db + tables if they don't exist
}

// ── WebSocket middleware ─────────────────────────────────────────────────────
var socketOptions = new WebSocketOptions
{
    KeepAliveInterval  = TimeSpan.FromSeconds(10),
    KeepAliveTimeout   = TimeSpan.FromSeconds(20),
};
foreach (var origin in builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [])
    socketOptions.AllowedOrigins.Add(origin);
app.UseWebSockets(socketOptions);

// ── REST endpoints ───────────────────────────────────────────────────────────
app.MapGet("/", () => Results.Json(new
{
    name            = "LASTLIGHT",
    service         = "C# authoritative game server with SQLite leaderboard",
    endpoint        = "/ws",
    playersPerRoom  = 5,
    leaderboard     = "/leaderboard",
    matches         = "/matches",
}));

app.MapGet("/health", () => Results.Ok(new { status = "ready" }));

// Top-25 leaderboard sorted by wins, then kills
app.MapGet("/leaderboard", async (AppDbContext db) =>
{
    var rows = await db.PlayerStats
        .OrderByDescending(p => p.Wins)
        .ThenByDescending(p => p.Kills)
        .Take(25)
        .Select(p => new
        {
            p.Name,
            p.Wins,
            p.Kills,
            p.GamesPlayed,
            WinRate = p.GamesPlayed == 0 ? 0.0 : Math.Round((double)p.Wins / p.GamesPlayed * 100, 1),
        })
        .ToListAsync();
    return Results.Json(rows);
});

// Last-20 completed matches
app.MapGet("/matches", async (AppDbContext db) =>
{
    var raw = await db.MatchRecords
        .OrderByDescending(m => m.PlayedAt)
        .Take(20)
        .ToListAsync();
    var rows = raw.Select(m => new
    {
        m.RoomCode,
        m.WinnerName,
        m.DurationSeconds,
        Players = m.PlayerNames.Split(',', StringSplitOptions.RemoveEmptyEntries),
        Kills   = m.PlayerKills.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => int.TryParse(k, out var n) ? n : 0).ToArray(),
        m.PlayedAt,
    });
    return Results.Json(rows);
});

// ── WebSocket game endpoint ──────────────────────────────────────────────────
var clients  = new ConcurrentDictionary<string, int>();
var capacity = new SemaphoreSlim(600, 600);

app.Map("/ws", async (HttpContext context, RoomServer games) =>
{
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
    if (!await capacity.WaitAsync(0, context.RequestAborted)) { context.Response.StatusCode = 503; return; }

    var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var count   = clients.AddOrUpdate(address, 1, (_, n) => n + 1);
    if (count > 30)
    {
        clients.AddOrUpdate(address, 0, (_, n) => Math.Max(0, n - 1));
        capacity.Release();
        context.Response.StatusCode = 429;
        return;
    }

    try
    {
        using var socket   = await context.WebSockets.AcceptWebSocketAsync();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var peer    = new Peer(socket);
        var sending = peer.Pump(lifetime.Token);
        var buffer  = new byte[4096];
        var windowStart = Environment.TickCount64;
        var messages    = 0;

        try
        {
            while (socket.State == WebSocketState.Open && !lifetime.IsCancellationRequested)
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(peer.Room is null ? 15 : 30));

                var total = 0;
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer, total, buffer.Length - total), deadline.Token);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    total += result.Count;
                    if (result.MessageType != WebSocketMessageType.Text || total >= buffer.Length)
                        { socket.Abort(); return; }
                } while (!result.EndOfMessage);

                var now = Environment.TickCount64;
                if (now - windowStart >= 1000) { windowStart = now; messages = 0; }
                if (++messages > 60) { socket.Abort(); return; }

                var input = JsonSerializer.Deserialize<Input>(buffer.AsSpan(0, total), RoomServer.Json);
                if (input is null || input.Name is null || input.Code is null) { socket.Abort(); return; }
                games.Handle(peer, input);
            }
        }
        catch (Exception e) when
            (e is WebSocketException or OperationCanceledException or JsonException or ObjectDisposedException) { }
        finally
        {
            // Pass a scope factory so Remove() can persist stats to the DB
            games.Remove(peer, context.RequestServices.GetRequiredService<IServiceScopeFactory>());
            peer.Outbox.Writer.TryComplete();
            await lifetime.CancelAsync();
            await sending;
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var closing = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Left the grove", closing.Token); }
                catch (Exception e) when (e is WebSocketException or OperationCanceledException) { }
            }
        }
    }
    finally
    {
        var remaining = clients.AddOrUpdate(address, 0, (_, n) => Math.Max(0, n - 1));
        if (remaining == 0) ((ICollection<KeyValuePair<string, int>>)clients).Remove(new(address, 0));
        capacity.Release();
    }
});

app.Run();
