using System.Security.Cryptography;

namespace Lastlight;

public sealed class Game
{
    public GameState State { get; } = new();
    public static readonly double[] Cooldowns = [9, 15, 15, 25, 10, 25, 25, 9, 25, 5];
    private int projectileId;
    public void Event(string text) { State.Events.Insert(0, text); if (State.Events.Count > 4) State.Events.RemoveAt(4); }
    public void Start()
    {
        if (State.Status != "waiting" || State.Players.Count != 5) return;
        var powers = Enumerable.Range(0, 10).ToArray();
        for (var i = powers.Length - 1; i > 0; i--) { var j = RandomNumberGenerator.GetInt32(i + 1); (powers[i], powers[j]) = (powers[j], powers[i]); }
        for (var i = 0; i < 5; i++)
        {
            var p = State.Players[i]; var angle = -Math.PI / 2 + i * Math.PI * 2 / 5;
            p.X = 500 + Math.Cos(angle) * 300; p.Y = 350 + Math.Sin(angle) * 270;
            p.EggX = p.X - Math.Cos(angle) * 34; p.EggY = p.Y - Math.Sin(angle) * 34;
            p.SafeX = p.X; p.SafeY = p.Y; p.Ability = powers[i]; p.Input = new();
        }
        State.Status = "playing"; Event("Five enter. One remains.");
    }
    public void Tick(double dt)
    {
        var s = State;
        if (s.Status != "playing") return;
        dt = Math.Min(dt, .05); s.Time += dt;
        if (s.Time < 3) return;
        s.Radius = Math.Max(45, 510 - Math.Max(0, s.Time - 25) * 2.7);
        s.Effects.RemoveAll(e => e.Until <= s.Time);
        foreach (var p in s.Players)
        {
            if (!p.Alive) continue;
            p.Cooldown = Math.Max(0, p.Cooldown - dt); p.ShotCooldown = Math.Max(0, p.ShotCooldown - dt); p.CycleCooldown = Math.Max(0, p.CycleCooldown - dt);
            if (p.TeleportAt > 0 && s.Time >= p.TeleportAt) { if (World.CanStand(p.TeleportX, p.TeleportY)) { p.X = p.TeleportX; p.Y = p.TeleportY; } p.TeleportAt = 0; }
            if (Environment.TickCount64 - p.LastInput > 300) p.Input = new();
            var input = p.Input;
            var activate = input.Ability && !p.AbilityHeld; var cycle = input.Cycle && !p.CycleHeld;
            p.AbilityHeld = input.Ability; p.CycleHeld = input.Cycle;
            if (p.FrozenUntil <= s.Time)
            {
                var length = Math.Sqrt(input.Dx * input.Dx + input.Dy * input.Dy);
                if (length > 0)
                {
                    var speed = 130 * (p.SpeedUntil > s.Time ? 3.5 : 1) * dt;
                    var x = p.X + input.Dx / Math.Max(1, length) * speed; var y = p.Y + input.Dy / Math.Max(1, length) * speed;
                    if (World.CanStand(x, p.Y)) p.X = x;
                    if (World.CanStand(p.X, y)) p.Y = y;
                }
                if (input.Hatch && !p.Hatched && World.Distance(p.X, p.Y, p.EggX, p.EggY) < 85) { p.Hatched = true; Event($"{p.Name} awakened a power."); s.Effects.Add(new(p.X, p.Y, 80, s.Time + .7, "hatch", p.Id)); }
                if (cycle && p.Copies.Count > 0 && p.CycleCooldown <= 0) { p.SelectedCopy = p.SelectedCopy >= p.Copies.Count - 1 ? -1 : p.SelectedCopy + 1; p.CycleCooldown = .3; }
                if (activate && p.Hatched && p.Cooldown <= 0) Activate(p, input);
                if (p.Hatched && p.Ability == 9 && p.Hp <= 50 && p.Cooldown <= 0) Activate(p, input);
                if (input.Attack && p.ShotCooldown <= 0)
                {
                    var angle = Math.Atan2(input.AimY - p.Y, input.AimX - p.X);
                    s.Projectiles.Add(new() { Id = projectileId++, Owner = p.Id, X = p.X + Math.Cos(angle) * 20, Y = p.Y + Math.Sin(angle) * 20, Vx = Math.Cos(angle) * 420, Vy = Math.Sin(angle) * 420 });
                    p.ShotCooldown = .4; p.InvisibleUntil = 0;
                }
            }
            if (Math.Floor(s.Time / 4) > Math.Floor((s.Time - dt) / 4) && World.Distance(p.X, p.Y, 500, 350) < s.Radius - 30) { p.SafeX = p.X; p.SafeY = p.Y; }
            if (World.Distance(p.X, p.Y, 500, 350) > s.Radius) Damage(p, 17 * dt, null);
        }
        s.Projectiles.RemoveAll(b =>
        {
            var frozen = s.Effects.Any(e => e.Kind == "time" && e.Owner != b.Owner && World.Distance(e.X, e.Y, b.X, b.Y) < e.Radius);
            if (!frozen) { b.X += b.Vx * dt; b.Y += b.Vy * dt; b.Life -= dt; }
            if (b.Life <= 0 || b.X < 0 || b.X > 1000 || b.Y < 0 || b.Y > 700 || World.Obstacles.Any(o => b.X > o.X && b.X < o.X + o.W && b.Y > o.Y && b.Y < o.Y + o.H)) return true;
            foreach (var p in s.Players)
            {
                if (!p.Alive || p.Id == b.Owner) continue;
                var d = World.Distance(p.X, p.Y, b.X, b.Y);
                if (p.ShieldUntil > s.Time && d < 110) return true;
                if (d < 17) { Damage(p, 16, b.Owner); return true; }
            }
            return false;
        });
        var alive = s.Players.Where(p => p.Alive).ToList();
        if (alive.Count <= 1) { s.Status = "finished"; s.Winner = alive.FirstOrDefault()?.Id; Event(alive.Count == 1 ? $"{alive[0].Name} is the last light." : "The grove claims everyone."); }
    }
    public void Activate(Player p, Input input)
    {
        var s = State;
        var power = p.Ability == 4 && p.SelectedCopy >= 0 && p.SelectedCopy < p.Copies.Count ? p.Copies[p.SelectedCopy] : p.Ability;
        if (power < 0 || power >= Cooldowns.Length) return;
        p.Cooldown = p.Ability == 4 && power != 4 ? 10 + Cooldowns[power] * 1.2 : Cooldowns[power];
        switch (power)
        {
            case 0: p.SpeedUntil = s.Time + 1; break;
            case 1:
                var dx = input.AimX - p.X; var dy = input.AimY - p.Y; var d = Math.Max(1, Math.Sqrt(dx * dx + dy * dy)); var r = Math.Min(220, d);
                var x = p.X + dx / d * r; var y = p.Y + dy / d * r;
                if (!World.CanStand(x, y)) { p.Cooldown = 0; return; }
                p.TeleportAt = s.Time + 1; p.TeleportX = x; p.TeleportY = y; break;
            case 2: p.InvisibleUntil = s.Time + 7; break;
            case 3: case 6:
                foreach (var o in s.Players.Where(o => o.Id != p.Id && o.Alive && World.Distance(o, p) < 150)) o.FrozenUntil = s.Time + 3;
                s.Effects.Add(new(p.X, p.Y, 150, s.Time + 3, power == 3 ? "ice" : "time", p.Id)); break;
            case 4:
                var nearest = s.Players.Where(o => o.Id != p.Id && o.Alive && o.Hatched && o.Ability != 4 && !p.Copies.Contains(o.Ability) && World.Distance(p, o) <= 260).OrderBy(o => World.Distance(p, o)).FirstOrDefault();
                if (nearest is not null) { p.Copies.Add(nearest.Ability); p.SelectedCopy = p.Copies.Count - 1; Event($"{p.Name} copied a power."); } else p.Cooldown = 0;
                break;
            case 5: p.RevealUntil = s.Time + 6; break;
            case 7: p.Hp = Math.Min(100, p.Hp + 15); break;
            case 8: p.Hp = Math.Min(100, p.Hp + 50); break;
            case 9: p.ShieldUntil = s.Time + 3; break;
        }
        s.Effects.Add(new(p.X, p.Y, 40, s.Time + .5, "cast", p.Id));
    }
    public void Damage(Player p, double amount, string? attacker)
    {
        var s = State;
        if (!p.Alive || p.InvulnerableUntil > s.Time) return;
        p.Hp -= amount;
        if (p.Hp > 0) return;
        if (p.Hatched && p.Ability == 7 && p.Revives < 3) { p.Revives++; p.Hp = 100; p.InvulnerableUntil = s.Time + 1.5; Event($"{p.Name} rises from the ashes ({3 - p.Revives} revives left)."); return; }
        if (p.Hatched && p.Ability == 6 && !p.RewindUsed) { p.RewindUsed = true; p.Hp = 40; p.X = p.SafeX; p.Y = p.SafeY; p.InvulnerableUntil = s.Time + 1; Event($"{p.Name} rewound time."); return; }
        p.Hp = 0; p.Alive = false; Event($"{p.Name} was eliminated.");
        var killer = s.Players.Find(o => o.Id == attacker); if (killer is not null) killer.Kills++;
    }
    public object Snapshot(Player viewer) => new
    {
        State.Time, State.Status, Players = State.Players.Select(p => p.ForViewer(viewer, State.Time)).ToList(), State.Projectiles,
        Effects = State.Effects.Where(e => e.Owner == viewer.Id || !State.Players.Any(p => p.Id == e.Owner && p.InvisibleUntil > State.Time && !(viewer.RevealUntil > State.Time && World.Distance(p, viewer) <= 260))).ToList(),
        State.Winner, State.Radius, State.Events
    };
}
