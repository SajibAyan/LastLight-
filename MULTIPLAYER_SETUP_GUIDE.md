# 🎮 Lastlight Arena — Multiplayer Setup Guide

Your game is **fully built** for 5-player multiplayer with a database leaderboard.
This guide walks you through getting it live so you and your friends can play.

---

## What was added

| File | What changed |
|------|--------------|
| `server/DatabaseModels.cs` | **NEW** — SQLite database with `PlayerStat` and `MatchRecord` tables |
| `server/Lastlight.Server.csproj` | Added EF Core + SQLite NuGet packages |
| `server/Program.cs` | Added DB setup, `/leaderboard`, `/matches` endpoints |
| `server/RoomServer.cs` | Saves match results + player stats to DB when a match ends |
| `server/appsettings.json` | Added `ConnectionStrings.DefaultConnection` for SQLite |
| `components/game/leaderboard.tsx` | **NEW** — React leaderboard component (fetch from server) |

---

## How the database works

- Uses **SQLite** — zero setup, single file (`arena.db` next to the server binary)
- Automatically created on first run (no migrations needed)
- After every completed match:
  - A `MatchRecord` is inserted (room code, winner, duration, player names & kills)
  - Each player's `PlayerStat` is upserted (wins, kills, games played)
- REST endpoints:
  - `GET /leaderboard` — top 25 players by wins, then kills
  - `GET /matches` — last 20 completed matches
  - `GET /health` — health check for deployment platforms

---

## Step 1 — Run locally first (test with friends on same network)

```bash
# In the server/ folder:
cd server
dotnet run
# Server starts on http://0.0.0.0:8080
```

Then in the game client, enter your local IP as the server address:
```
ws://192.168.x.x:8080/ws
```

Share the room code with up to 4 friends on the same network.

---

## Step 2 — Deploy so anyone can join (recommended: Railway)

### Option A: Railway (easiest, free tier available)

1. Go to https://railway.app and sign up
2. Click **New Project → Deploy from GitHub Repo**
   - Push your code to GitHub first, or use **Deploy from local** via the CLI
3. Set the **Root Directory** to `server`
4. Railway will detect the `Dockerfile` automatically
5. Click **Deploy**
6. Go to **Settings → Networking → Generate Domain**
7. Your server URL will be something like `lastlight-arena.up.railway.app`

In the game client, enter:
```
wss://lastlight-arena.up.railway.app/ws
```

> **Important:** Railway gives you a persistent disk volume — mount it at `/app/data`
> and update `appsettings.json` connection string to:
> `"Data Source=/app/data/arena.db"`
> This keeps your leaderboard data across deploys.

### Option B: Render (also free)

1. Go to https://render.com → New → Web Service
2. Connect your GitHub repo, set **Root Directory** to `server`
3. Runtime: **Docker**
4. Add a **Disk** (under Advanced) mounted at `/app/data`
5. Set env var: `ConnectionStrings__DefaultConnection=Data Source=/app/data/arena.db`
6. Deploy and copy the `.onrender.com` URL

### Option C: Fly.io

```bash
cd server
fly launch          # follow prompts
fly volumes create arena_data --size 1
# Edit fly.toml to mount the volume at /app/data
fly deploy
```

---

## Step 3 — Update the frontend to know your server

In `components/game/online-dialog.tsx`, find the default server input.
You can pre-fill it with your deployed URL so friends don't have to type it:

```tsx
// In game-shell.tsx or wherever server state is initialized:
const [server, setServer] = useState('wss://YOUR-DEPLOYED-URL.railway.app/ws')
```

Or share the invite link — the game already encodes the room code in the URL.

---

## Step 4 — Add the leaderboard to the game UI

In `components/game/game-shell.tsx` (or `app/page.tsx`), import and use it:

```tsx
import { Leaderboard } from '@/components/game/leaderboard'

// Inside your component, wherever you want to show it:
<Leaderboard server={server} />
```

It fetches `/leaderboard` from your server and displays wins, kills, games played,
and win rate for all players.

---

## How to play with friends (summary)

1. **Host** opens the game → clicks **Play Online** → **Create a room**
2. Copies the **6-character room code** and sends it to 4 friends
3. Each friend opens the game → **Play Online** → **Join a room** → types the code
4. Match starts automatically when all 5 players have joined
5. After the match, stats are saved to the database automatically
6. View the leaderboard at `https://YOUR-SERVER/leaderboard`

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| "Server did not respond" | Make sure the C# server is running and the URL is correct |
| "Use a secure wss://" | Your deployed server must use HTTPS/WSS (Railway/Render do this automatically) |
| Leaderboard empty | Play a full match to completion — partial/abandoned games aren't saved |
| `arena.db` disappears on redeploy | Mount a persistent volume (see Step 2) |
| Build fails | Make sure you're on .NET 10 SDK (`dotnet --version`) |

---

## Database schema (for reference)

```sql
-- Auto-created by EF Core on first run
CREATE TABLE PlayerStats (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Name        TEXT NOT NULL UNIQUE,
    Wins        INTEGER NOT NULL DEFAULT 0,
    Kills       INTEGER NOT NULL DEFAULT 0,
    GamesPlayed INTEGER NOT NULL DEFAULT 0,
    LastSeen    TEXT NOT NULL
);

CREATE TABLE MatchRecords (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    RoomCode        TEXT NOT NULL,
    WinnerName      TEXT,
    DurationSeconds REAL NOT NULL,
    PlayerNames     TEXT NOT NULL,  -- comma-separated
    PlayerKills     TEXT NOT NULL,  -- comma-separated, same order
    PlayedAt        TEXT NOT NULL
);
```
