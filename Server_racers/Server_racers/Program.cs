using System.Collections.Concurrent;
using System.Text.Json.Serialization;

public class ServerInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Server";
    public string Ip { get; set; } = "";
    public int Port { get; set; }
    public string Track { get; set; } = "track_1";
    public int Laps { get; set; } = 3;
    public int MaxPlayers { get; set; } = 8;
    public int CurrentPlayers { get; set; } = 0;
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
}

public class RegisterRequest
{
    public string Name { get; set; } = "Server";
    public string Ip { get; set; } = "";
    public int Port { get; set; }
    public string Track { get; set; } = "track_1";
    public int Laps { get; set; } = 3;
    public int MaxPlayers { get; set; } = 8;
}

public class HeartbeatRequest
{
    public Guid Id { get; set; }
    public int CurrentPlayers { get; set; }
}

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://26.175.171.220:6004");

        var app = builder.Build();

        var servers = new ConcurrentDictionary<Guid, ServerInfo>();

        _ = Task.Run(async () =>
        {
            while (true)
            {
                var now = DateTime.UtcNow;
                foreach (var kv in servers)
                {
                    if ((now - kv.Value.LastHeartbeat).TotalSeconds > 30)
                        servers.TryRemove(kv.Key, out _);
                }
                await Task.Delay(5000);
            }
        });

        app.MapPost("/register", (RegisterRequest req) =>
        {
            var info = new ServerInfo
            {
                Name = req.Name,
                Ip = string.IsNullOrWhiteSpace(req.Ip) ? "auto" : req.Ip,
                Port = req.Port,
                Track = req.Track,
                Laps = req.Laps,
                MaxPlayers = req.MaxPlayers,
                LastHeartbeat = DateTime.UtcNow
            };

            servers[info.Id] = info;
            return Results.Ok(info);
        });

        app.MapPost("/heartbeat", (HeartbeatRequest req) =>
        {
            if (!servers.TryGetValue(req.Id, out var info))
                return Results.NotFound();
            if (req.CurrentPlayers == -1)
            {
                servers.TryRemove(req.Id, out _);
                return Results.Ok();
            }

            info.LastHeartbeat = DateTime.UtcNow;
            info.CurrentPlayers = req.CurrentPlayers;
            return Results.Ok();
        });

        app.MapGet("/servers", () =>
        {
            var now = DateTime.UtcNow;
            var list = servers.Values
                .Where(s => (now - s.LastHeartbeat).TotalSeconds <= 30)
                .ToList();

            return Results.Ok(list);
        });

        app.Run();
    }
}
