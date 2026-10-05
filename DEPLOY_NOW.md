# 🎮 Deploy Your Game — Play With Friends From Anywhere

This guide gets 5 people playing from different locations in ~10 minutes.
Everything is **free**.

---

## How it works

```
  Friend A (creates room)        Friends B–E (join with code)
        │                                 │
        ▼                                 ▼
  [Your game website]  ──────  [Same game website]
        │                                 │
        └──────────────┬──────────────────┘
                       ▼
              [C# game server]   ← this is what you need to deploy
              (handles rooms,
               codes, live play)
```

You need **two deployments**:
1. **Server** → Railway (handles rooms + live gameplay)
2. **Website** → Vercel (the game UI your friends open)

---

## STEP 1 — Deploy the game server to Railway (free)

> Railway runs the C# server that manages rooms and syncs gameplay.

1. Go to **https://railway.app** and sign up (GitHub login is easiest)
2. Click **New Project → Deploy from GitHub repo**
3. Push your `server/` folder to a GitHub repo (or the whole project)
4. When Railway asks for the root directory, type: `server`
5. Railway auto-detects the `Dockerfile` and builds it
6. Go to your service → **Settings → Networking → Generate Domain**
7. Copy the domain — it looks like: `arena-game-server.up.railway.app`
8. Add a **Volume** (Settings → Volumes) mounted at `/app/data` — this keeps the leaderboard database between redeploys

✅ Your server URL is: `wss://arena-game-server.up.railway.app/ws`

---

## STEP 2 — Configure your website to use the server

1. In the project root, copy `.env.example` → `.env.local`
2. Open `.env.local` and replace the placeholder:
   ```
   NEXT_PUBLIC_GAME_SERVER_URL=wss://arena-game-server.up.railway.app/ws
   ```
   (Use your actual Railway URL from Step 1)

---

## STEP 3 — Deploy the website to Vercel (free)

> Vercel hosts the Next.js game frontend your friends will open in their browser.

1. Go to **https://vercel.com** and sign up (GitHub login)
2. Click **Add New → Project → Import Git Repository**
3. Select your repo, leave all defaults
4. Under **Environment Variables**, add:
   - Key: `NEXT_PUBLIC_GAME_SERVER_URL`
   - Value: `wss://arena-game-server.up.railway.app/ws`
5. Click **Deploy** — takes ~2 minutes
6. Vercel gives you a URL like `arena-game.vercel.app`

✅ Share `https://arena-game.vercel.app` with your 4 friends!

---

## STEP 4 — Start a match!

1. **You** open `https://arena-game.vercel.app`
2. Enter your name → click **With Friends** → **Create or join a room**
3. Click **Create private room** → you get a 6-character code like `XK7F2Q`
4. Share that code with your 4 friends (Discord, WhatsApp, whatever)
5. Each friend opens the same URL, clicks **Join a room**, enters the code
6. **When all 5 players are in → the game starts automatically** ⚔️

---

## Troubleshooting

| Problem | Fix |
|---|---|
| "Could not connect" error | Make sure your Railway server is running. Check Railway dashboard logs. |
| Friends can't join | Confirm they're using the exact same 6-char code. Codes expire when the room ends. |
| Game won't start | Needs exactly 5 players. Check the lobby count shown in the dialog. |
| Leaderboard not saving | Check Railway volume is mounted at `/app/data`. |

---

## Leaderboard & Match History

After matches are played:
- Visit `https://YOUR_RAILWAY_URL/leaderboard` — top 25 players by wins
- Visit `https://YOUR_RAILWAY_URL/matches` — last 20 match records
- The in-game leaderboard tab shows this automatically

---

*Questions? The server supports up to 600 concurrent connections — plenty for you and your friends.*
