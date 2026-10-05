namespace Lastlight;

public sealed class Input
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public double Dx { get; set; }
    public double Dy { get; set; }
    public double AimX { get; set; } = 500;
    public double AimY { get; set; } = 350;
    public bool Attack { get; set; }
    public bool Ability { get; set; }
    public bool Hatch { get; set; }
    public bool Cycle { get; set; }
    public bool IsValid() => double.IsFinite(Dx) && double.IsFinite(Dy) && double.IsFinite(AimX) && double.IsFinite(AimY);
    public void Clamp() { Dx = Math.Clamp(Dx, -1, 1); Dy = Math.Clamp(Dy, -1, 1); AimX = Math.Clamp(AimX, 0, 1000); AimY = Math.Clamp(AimY, 0, 700); }
}

public sealed class Player
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Wanderer";
    public double X { get; set; }
    public double Y { get; set; }
    public double Hp { get; set; } = 100;
    public int Ability { get; set; } = -1;
    public bool Hatched { get; set; }
    public bool Alive { get; set; } = true;
    public double Cooldown { get; set; }
    public double ShotCooldown { get; set; }
    public double SpeedUntil { get; set; }
    public double InvisibleUntil { get; set; }
    public double FrozenUntil { get; set; }
    public double RevealUntil { get; set; }
    public double ShieldUntil { get; set; }
    public double InvulnerableUntil { get; set; }
    public int Revives { get; set; }
    public bool RewindUsed { get; set; }
    public double SafeX { get; set; }
    public double SafeY { get; set; }
    public double TeleportAt { get; set; }
    public double TeleportX { get; set; }
    public double TeleportY { get; set; }
    public List<int> Copies { get; set; } = [];
    public int SelectedCopy { get; set; } = -1;
    public double CycleCooldown { get; set; }
    public int Kills { get; set; }
    public bool Bot { get; set; }
    public double EggX { get; set; }
    public double EggY { get; set; }
    public bool Visible { get; set; } = true;
    [System.Text.Json.Serialization.JsonIgnore] public Input Input { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore] public bool AbilityHeld { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool CycleHeld { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public long LastInput { get; set; } = Environment.TickCount64;
    public Player ForViewer(Player viewer, double time)
    {
        var clone = (Player)MemberwiseClone();
        clone.Copies = Id == viewer.Id ? [..Copies] : [];
        if (!Hatched) clone.Ability = -1;
        clone.SafeX = 0; clone.SafeY = 0;
        if (Id != viewer.Id && InvisibleUntil > time && !(viewer.RevealUntil > time && World.Distance(X, Y, viewer.X, viewer.Y) <= 260))
        {
            clone.Visible = false; clone.X = -1000; clone.Y = -1000;
            clone.TeleportX = -1000; clone.TeleportY = -1000;
        }
        return clone;
    }
}
public sealed class Projectile
{
    public int Id { get; set; }
    public string Owner { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Vx { get; set; }
    public double Vy { get; set; }
    public double Life { get; set; } = 1.6;
}
public sealed record Effect(double X, double Y, double Radius, double Until, string Kind, string Owner);
public sealed class GameState
{
    public double Time { get; set; }
    public string Status { get; set; } = "waiting";
    public List<Player> Players { get; set; } = [];
    public List<Projectile> Projectiles { get; set; } = [];
    public List<Effect> Effects { get; set; } = [];
    public string? Winner { get; set; }
    public double Radius { get; set; } = 510;
    public List<string> Events { get; set; } = ["The grove is waiting."];
}
public static class World
{
    public static readonly (double X, double Y, double W, double H)[] Obstacles = [(290,180,65,95), (650,430,65,95), (635,170,100,40), (255,480,100,40), (465,215,70,36), (465,450,70,36), (170,340,45,65), (785,295,45,65)];
    public static double Distance(double x, double y, double a, double b) => Math.Sqrt((x - a) * (x - a) + (y - b) * (y - b));
    public static double Distance(Player a, Player b) => Distance(a.X, a.Y, b.X, b.Y);
    public static bool CanStand(double x, double y) => x >= 22 && x <= 978 && y >= 22 && y <= 678 && !Obstacles.Any(o => x > o.X - 14 && x < o.X + o.W + 14 && y > o.Y - 14 && y < o.Y + o.H + 14);
}
