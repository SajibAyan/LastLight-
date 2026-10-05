namespace Lastlight;

public static class GameTests
{
    private static int assertions;
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException($"FAIL: {message}");
        assertions++; Console.WriteLine($"PASS: {message}");
    }
    private static Game Fresh(int power)
    {
        var g = new Game();
        for (var i = 0; i < 5; i++) g.State.Players.Add(new() { Name = $"Player {i}" });
        g.Start(); g.State.Time = 10;
        for (var i = 0; i < 5; i++) { var p = g.State.Players[i]; p.Hatched = true; p.Ability = 8; p.X = 800 + (i % 2) * 40; p.Y = 540 + (i % 2) * 40; }
        g.State.Players[0].X = 100; g.State.Players[0].Y = 100; g.State.Players[0].Ability = power;
        g.State.Players[1].X = 140; g.State.Players[1].Y = 100;
        return g;
    }
    private static void Advance(Game game, double seconds)
    {
        for (var i = 0; i < Math.Ceiling(seconds / .04); i++) { foreach (var p in game.State.Players) p.LastInput = Environment.TickCount64; game.Tick(.04); }
    }
    public static void Run()
    {
        for (var match = 0; match < 100; match++)
        {
            var unique = new Game();
            for (var i = 0; i < 5; i++) unique.State.Players.Add(new() { Name = $"P{i}" });
            unique.Start();
            if (unique.State.Players.Select(p => p.Ability).Distinct().Count() != 5) throw new Exception("Duplicate starting abilities");
            if (unique.State.Players.Select(p => (p.X, p.Y)).Distinct().Count() != 5) throw new Exception("Duplicate spawn points");
            if (unique.State.Players.Any(p => !World.CanStand(p.X, p.Y))) throw new Exception("Blocked spawn point");
        }
        Check(true, "100 matches have five unique powers and five valid distinct spawn points");
        var waiting = new Game(); for (var i = 0; i < 4; i++) waiting.State.Players.Add(new()); waiting.Start(); Check(waiting.State.Status == "waiting", "Four players cannot start a match");
        var g = Fresh(0); var p = g.State.Players[0]; p.Input = new() { Dx = 1, Ability = true }; g.Tick(.04); Check(p.SpeedUntil > g.State.Time && p.Cooldown == 9, "Max Speed activates with nine-second cooldown"); var x = p.X; Advance(g, .08); Check(p.X - x > 30, "Max Speed actually increases movement");
        g = Fresh(1); p = g.State.Players[0]; g.Activate(p, new() { AimX = 200, AimY = 100 }); Check(p.X == 100 && p.TeleportAt > g.State.Time, "Teleport waits and marks its destination"); Advance(g, 1.08); Check(Math.Abs(p.X - 200) < .1, "Teleport lands after its one-second delay");
        g = Fresh(1); p = g.State.Players[0]; g.Activate(p, new() { AimX = 300, AimY = 200 }); Check(p.Cooldown == 0 && p.TeleportAt == 0, "Teleport cannot land inside a ruin");
        g = Fresh(2); p = g.State.Players[0]; g.Activate(p, new()); var viewer = g.State.Players[1]; Check(!p.ForViewer(viewer, g.State.Time).Visible && p.ForViewer(viewer, g.State.Time).X == -1000, "Invisibility redacts coordinates from opponents"); viewer.RevealUntil = 16; Check(p.ForViewer(viewer, g.State.Time).Visible, "All-Seeing Eye reveals nearby invisible opponents"); p.Input.Attack = true; g.Tick(.04); Check(p.InvisibleUntil == 0, "Attacking breaks invisibility");
        g = Fresh(3); p = g.State.Players[0]; var target = g.State.Players[1]; g.Activate(p, new()); target.Input = new() { Dx = 1, Attack = true, Ability = true }; x = target.X; Advance(g, .2); Check(target.X == x && target.ShotCooldown == 0 && target.Cooldown == 0, "Absolute Zero blocks movement, attacks, and casting");
        g = Fresh(4); p = g.State.Players[0]; target = g.State.Players[1]; target.Ability = 7; g.Activate(p, new()); Check(p.Copies.SequenceEqual([7]), "Copycat stores the nearest unique active power"); p.Hp = 40; p.Cooldown = 0; g.Activate(p, new()); Check(p.Hp == 55 && Math.Abs(p.Cooldown - 20.8) < .01, "Copied Phoenix heals with the cooldown penalty"); g.Damage(p, 200, null); Check(!p.Alive, "Copycat does not inherit Phoenix revives");
        g = Fresh(5); p = g.State.Players[0]; g.Activate(p, new()); Check(p.RevealUntil == 16, "All-Seeing Eye lasts six seconds");
        g = Fresh(6); p = g.State.Players[0]; g.Activate(p, new()); g.State.Projectiles.Add(new() { Owner = g.State.Players[2].Id, X = 100, Y = 200, Vx = 300 }); g.Tick(.04); Check(g.State.Projectiles[0].X == 100, "Time Stop freezes nearby enemy projectiles"); g.Damage(p, 200, null); Check(p.Alive && p.Hp == 40 && p.RewindUsed, "Time Stop rewinds lethal damage once"); p.InvulnerableUntil = 0; g.Damage(p, 200, null); Check(!p.Alive, "Time Stop cannot rewind a second time");
        g = Fresh(7); p = g.State.Players[0]; p.Hp = 80; g.Activate(p, new()); Check(p.Hp == 95, "Phoenix restores fifteen health"); for (var i = 0; i < 3; i++) { p.InvulnerableUntil = 0; g.Damage(p, 200, null); Check(p.Alive && p.Hp == 100, $"Phoenix revive {i + 1} restores full health"); } p.InvulnerableUntil = 0; g.Damage(p, 200, null); Check(!p.Alive && p.Revives == 3, "Phoenix is eliminated after three revives");
        g = Fresh(8); p = g.State.Players[0]; p.Hp = 20; g.Activate(p, new()); Check(p.Hp == 70, "Blessing restores fifty health"); p.Hp = 90; g.Activate(p, new()); Check(p.Hp == 100, "Healing cannot exceed maximum health");
        g = Fresh(9); p = g.State.Players[0]; p.Hp = 49; g.Tick(.04); Check(p.ShieldUntil > g.State.Time, "Force Field automatically activates below fifty health"); g.State.Projectiles.Add(new() { Owner = g.State.Players[2].Id, X = 100, Y = 180, Vx = 0 }); g.Tick(.04); Check(g.State.Projectiles.Count == 0 && p.Hp == 49, "Force Field intercepts enemy projectiles");
        g = Fresh(0); p = g.State.Players[0]; p.Input = new() { Dx = 1, Dy = 1 }; x = p.X; var y = p.Y; g.Tick(.04); Check(Math.Abs(World.Distance(x, y, p.X, p.Y) - 5.2) < .001, "Diagonal movement cannot exceed server speed");
        g = Fresh(0); p = g.State.Players[0]; p.Hatched = false; p.EggX = p.X; p.EggY = p.Y; p.Input.Hatch = true; g.Tick(.04); Check(p.Hatched, "Nearby egg interaction unlocks the assigned ability");
        g = Fresh(0); p = g.State.Players[0]; p.Hatched = false; Check(p.ForViewer(p, g.State.Time).Ability == -1, "Unhatched abilities stay secret in network snapshots");
        g = Fresh(0); p = g.State.Players[0]; p.Hatched = false; p.Input.Ability = true; g.Tick(.04); Check(p.Cooldown == 0, "Unhatched players cannot activate powers");
        g = Fresh(0); g.State.Time = 150; p = g.State.Players[0]; var hp = p.Hp; g.Tick(.04); Check(p.Hp < hp && g.State.Radius < 200, "Shrinking safe zone damages outside players");
        g = Fresh(0); for (var i = 1; i < 5; i++) { g.State.Players[i].Hatched = false; g.Damage(g.State.Players[i], 1000, g.State.Players[0].Id); } g.Tick(.04); Check(g.State.Status == "finished" && g.State.Winner == g.State.Players[0].Id, "The last surviving player wins"); Check(g.State.Players[0].Kills == 4, "Eliminations are credited authoritatively");
        Check(!new Input { Dx = double.NaN }.IsValid(), "Non-finite movement is rejected"); var input = new Input { Dx = 999, AimX = -999 }; input.Clamp(); Check(input.Dx == 1 && input.AimX == 0, "Inputs are clamped to valid movement and arena bounds");
        Console.WriteLine($"\n{assertions} C# game-rule assertions passed.");
    }
}
