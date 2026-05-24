using Assets.scripts.Networking;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;// тут лежит твой Server_packet

public class Server_log
{
    private TcpListener listener;
    private readonly List<TcpClient> clients = new List<TcpClient>();
    private readonly Dictionary<TcpClient, string> playerNames = new();
    private readonly Dictionary<TcpClient, Guid> clientToId = new();
    private readonly Dictionary<Guid, CarState> cars = new();
    private readonly Dictionary<Guid, Player_info> players = new();
    private int nextStartIndex = 0;


    private UdpClient udp;
    private int udpPort;
    private readonly Dictionary<Guid, IPEndPoint> carUdpEndpoints = new();

    private bool running = false;

    private readonly string masterServerUrl = "http://26.175.171.220:6004";
    private Guid serverId;
    public bool MasterConnected { get; private set; } = false;
    internal readonly int port;
    private readonly string track;
    private readonly int laps;
    private readonly int maxPlayers;
    private readonly string name;
    private bool countdownStarted = false;
    private DateTime countdownEndTime;


    public Server_log(int port, string track, int laps, int maxPlayers, string name)
    {
        this.port = port;
        this.track = track;
        this.laps = laps;
        this.maxPlayers = maxPlayers;
        this.name = name;
    }

    // состояние машины на сервере
    class CarState
    {
        public bool initialized = false;
        public float x, y, rot;
        public float vx, vy;
        public float throttle, steer;
        public int lap;
        public string name;

    }

    class InputPacket
    {
        public string type;
        public Guid id;
        public float throttle;
        public float steer;
    }

    class HelloPacket
    {
        public string type;
        public Guid id;
    }
    class StartPacket
    {
        public string type;
        public Guid id;
        public float x, y, rot;
    }
    class WinPacket
    {
        public string type;
        public Guid id;
    }
    class Discon_Packet
    {
        public string type;
        public Guid id;
    }
    class Index_Packet
    {
        public string type;
        public Guid id;
    }
    class Player_info
    {
        public Guid id;
        public int carIndex;
    }
    class WorldCarDto
    {
        public Guid id;
        public float x, y, rot;
        public float vx, vy;

    }

    class WorldPacket
    {
        public string type = "world";
        public List<WorldCarDto> cars;
    }
    class CollisionPacket
    {
        public string type;
        public Guid id;
        public float safeX;
        public float safeY;
    }


    class ServerInfoResponse
    {
        public Guid Id { get; set; }
    }

    // Запуск сервера в отдельном потоке
    public void Run()
    {
        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            running = true;

            Debug.Log($"[TCP SERVER] Started on port {port}");

            // UDP на порту +1
            udpPort = port + 1;
            udp = new UdpClient(udpPort);
            Debug.Log($"[UDP SERVER] Started on port {udpPort}");

            RegisterOnMaster();
            StartHeartbeat();
            StartGameLoop();
            StartUdpLoop();
            BroadcastRaceStart();

            AcceptLoop();
        }
        catch (Exception ex)
        {
            Debug.LogError("[TCP SERVER] Fatal error: " + ex.Message);
        }
    }

    // Приём TCP‑клиентов
    private void AcceptLoop()
    {
        while (running)
        {
            TcpClient client = null;
            try
            {
                client = listener.AcceptTcpClient();
            }
            catch (SocketException)
            {
                if (!running) break;
                continue;
            }
            catch (Exception ex)
            {
                Debug.LogError("[TCP SERVER] Accept error: " + ex.Message);
                if (!running) break;
                continue;
            }

            if (!running)
            {
                try { client?.Close(); } catch { }
                break;
            }
            if (clients.Count >= maxPlayers)
            {
                Console.WriteLine("[SERVER] Rejecting connection: race already starting");
                client.Close();
                continue;
            }
            lock (clients) clients.Add(client);
            if (!countdownStarted)
            {
                countdownStarted = true;
                countdownEndTime = DateTime.Now.AddSeconds(60);

                Console.WriteLine("[SERVER] First player joined. Countdown started: 60 seconds.");

                new Thread(() =>
                {
                    int total = 5;

                    while (total > 0)
                    {
                        BroadcastCountdown(total);
                        Thread.Sleep(1000);
                        total--;
                    }

                    Console.WriteLine("[SERVER] Countdown finished. Starting race.");
                    BroadcastRaceStart();

                }).Start();
            }
            Console.WriteLine("[TCP SERVER] Client connected");
            var t = new Thread(() => ClientLoop(client));
            t.IsBackground = true;
            t.Start();
        }
    }

    private void BroadcastCountdown(int secondsLeft)
    {
        var packet = new
        {
            type = "countdown",
            timeLeft = secondsLeft
        };

        string json = JsonConvert.SerializeObject(packet);
        byte[] data = Encoding.UTF8.GetBytes(json);

        lock (carUdpEndpoints)
        {
            foreach (var ep in carUdpEndpoints.Values)
                udp.Send(data, data.Length, ep);
        }
    }

    // Обработка одного TCP‑клиента
    private void ClientLoop(TcpClient client)
    {
        var stream = client.GetStream();
        byte[] buffer = new byte[1024];

        // создаём ID игрока и машину
        var id = Guid.NewGuid();
        lock (clientToId) clientToId[client] = id;
        lock (cars)
        {
            cars[id] = new CarState();
        }

        int startIndex = nextStartIndex;
        nextStartIndex = (nextStartIndex + 1) % 8;
        players.Add(id, new Player_info { id = id, carIndex = startIndex });
        var welcome = new
        {
            type = "welcome",
            id = id,
            startIndex = startIndex
        };
        string welcomeJson = JsonConvert.SerializeObject(welcome);
        byte[] welcomeData = Encoding.UTF8.GetBytes(welcomeJson);
        stream.Write(welcomeData, 0, welcomeData.Length);

        while (running)
        {
            if (!client.Connected)
                break;
            if (!stream.DataAvailable)
            {
                Thread.Sleep(10);
                continue;
            }


            int bytes = stream.Read(buffer, 0, buffer.Length);
            if (bytes <= 0) break;

            string msg = Encoding.UTF8.GetString(buffer, 0, bytes);

            try
            {
                var packet = JsonConvert.DeserializeObject<Server_packet>(msg);

                if (packet != null && packet.type == "set_name")
                {
                    string name = packet.name;
                    Console.WriteLine($"[SERVER] Player set name: {name}");
                    lock (clients)
                        playerNames[client] = name;
                }
            }
            catch
            {
                Console.WriteLine("[SERVER] Invalid TCP packet");
            }

            Console.WriteLine("[TCP SERVER] Received: " + msg);

            byte[] response = Encoding.UTF8.GetBytes("OK");
            stream.Write(response, 0, response.Length);
        }

        lock (clients) clients.Remove(client);
        lock (clientToId)
        {
            if (clientToId.TryGetValue(client, out var pid))
            {
                clientToId.Remove(client);
                lock (cars) cars.Remove(pid);
                lock (carUdpEndpoints) carUdpEndpoints.Remove(pid);
            }
        }

        Console.WriteLine("[TCP SERVER] Client disconnected");
    }

    // UDP: приём input от клиентов
    private void StartUdpLoop()
    {
        var t = new Thread(() =>
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);

            while (running)
            {
                try
                {
                    byte[] data = udp.Receive(ref remote);
                    string msg = Encoding.UTF8.GetString(data);
                    try
                    {
                        if (msg.Contains("\"type\":\"set_start\""))
                        {
                            var sp = JsonConvert.DeserializeObject<StartPacket>(msg);

                            lock (cars)
                            {
                                if (cars.TryGetValue(sp.id, out var car))
                                {
                                    car.x = sp.x;
                                    car.y = sp.y;
                                    car.rot = sp.rot;

                                    car.vx = 0;
                                    car.vy = 0;
                                    car.initialized = true;
                                }
                            }

                            Console.WriteLine("[UDP SERVER] Start position set for " + sp.id);
                            continue;
                        }
                        if (msg.Contains("\"type\":\"disconnect\""))
                        {
                            var sp = JsonConvert.DeserializeObject<Discon_Packet>(msg);
                            lock(carUdpEndpoints)
                                carUdpEndpoints.Remove(sp.id);
                            RemovePlayer(sp.id);
                            BroadcastDespawn(sp.id);
                            continue;
                        }
                        if (msg.Contains("\"type\":\"index\""))
                        {
                            var r = JsonConvert.DeserializeObject<Index_Packet>(msg);
                            Give_index(r.id);
                            continue;
                        }

                        if (msg.Contains("\"type\":\"collision\""))
                        {
                            var cp = JsonConvert.DeserializeObject<CollisionPacket>(msg);

                            if (cars.TryGetValue(cp.id, out var c))
                            {
                                // ставим машину в lastSafePos
                                c.x = cp.safeX;
                                c.y = cp.safeY;

                                // обнуляем скорость
                                c.vx = 0;
                                c.vy = 0;

                                Console.WriteLine("[SERVER] Collision: reset car " + cp.id);
                            }

                            continue;
                        }

                        if (msg.Contains("\"type\":\"win\""))
                        {
                            var wp = JsonConvert.DeserializeObject<WinPacket>(msg);

                            Console.WriteLine("[SERVER] Player won: " + wp.id);

                            AnnounceWinner(wp.id);
                            continue;
                        }
                        if (msg.Contains("\"type\":\"hello_udp\""))
                        {
                            var hello = JsonConvert.DeserializeObject<HelloPacket>(msg);

                            lock (carUdpEndpoints)
                                carUdpEndpoints[hello.id] = remote;

                            continue;
                        }

                        var packet = JsonConvert.DeserializeObject<InputPacket>(msg);
                        if (packet != null && packet.type == "input")
                        {
                            if(!cars.ContainsKey(packet.id))
                            {
                                continue;
                            }
                            lock (cars)
                            {
                                if (cars.TryGetValue(packet.id, out var car))
                                {
                                    car.throttle = packet.throttle;
                                    car.steer = packet.steer;
                                }
                            }

                            lock (carUdpEndpoints)
                            {
                                carUdpEndpoints[packet.id] = remote;
                            }
                        }
                    }
                    catch
                    {
                        Debug.LogError("[UDP SERVER] Invalid packet: " + msg);
                    }
                }
                catch (SocketException)
                {
                    if (!running) break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[UDP SERVER] Error: " + ex.Message);
                }
            }
        });

        t.IsBackground = true;
        t.Start();
    }

    private void Give_index(Guid id)
    {
        if (players.TryGetValue(id, out var p))
        {
            var packet = new
            {
                type = "index",
                carIndex = p.carIndex
            };
            string json = JsonConvert.SerializeObject(packet);
            byte[] data = Encoding.UTF8.GetBytes(json);
            lock (carUdpEndpoints)
            {
                if (carUdpEndpoints.TryGetValue(id, out var ep))
                {
                    udp.Send(data, data.Length, ep);
                }
            }
        }
    }

    private void RemovePlayer(Guid id)
    {
        lock (cars)
        {
            cars.Remove(id);
        }
        lock (players)
        {
            players.Remove(id);
        }
        lock (carUdpEndpoints)
        {
            carUdpEndpoints.Remove(id);
        }
        lock (clientToId)
        {
            var tcp = clientToId.FirstOrDefault(kv => kv.Value == id).Key;
            if (tcp != null)
            {
                try { tcp.GetStream().Close(); } catch { }
                try { tcp.Close(); } catch { }
            }
        }
        Update_on_pla();
        Console.WriteLine("[SERVER] Removed player " + id);
    }

    private void BroadcastDespawn(Guid id)
    {
        var packet = new
        {
            type = "despawn",
            id = id
        };

        string json = JsonConvert.SerializeObject(packet);
        byte[] data = Encoding.UTF8.GetBytes(json);

        lock (carUdpEndpoints)
        {
            foreach (var ep in carUdpEndpoints.Values)
                udp.Send(data, data.Length, ep);
        }
    }

    private void AnnounceWinner(Guid id)
{
    string winnerName = "Unknown";

    lock (playerNames)
    {
        foreach (var kv in playerNames)
        {
            if (clientToId.TryGetValue(kv.Key, out var cid) && cid == id)
            {
                winnerName = kv.Value;
                break;
            }
        }
    }

    var packet = new
    {
        type = "winner",
        name = winnerName
    };

    string json = JsonConvert.SerializeObject(packet);
    byte[] data = Encoding.UTF8.GetBytes(json);

    lock (carUdpEndpoints)
    {
        foreach (var ep in carUdpEndpoints.Values)
            udp.Send(data, data.Length, ep);
    }

    Console.WriteLine("[SERVER] Winner: " + winnerName);

    // запускаем таймер на выключение сервера
    new Thread(() =>
    {
        Thread.Sleep(60000);
        Console.WriteLine("[SERVER] Auto-shutdown after 1 minute");
        Stop();
    }).Start();
}

    private void BroadcastRaceStart()
    {
        var packet = new
        {
            type = "race_start",
            lapsToWin = this.laps
        };

        string json = JsonConvert.SerializeObject(packet);
        byte[] data = Encoding.UTF8.GetBytes(json);

        lock (carUdpEndpoints)
        {
            foreach (var ep in carUdpEndpoints.Values)
                udp.Send(data, data.Length, ep);
        }
    }


    // Игровой цикл: физика + рассылка мира
    private void StartGameLoop()
    {
        var t = new Thread(() =>
        {
            const float dt = 0.02f; // 50 FPS

            while (running)
            {
                lock (cars)
                {
                    foreach (var kv in cars)
                        SimulateCar(kv.Value, dt);
                    HandleCollisions();
                }

                BroadcastWorldState();

                Thread.Sleep(20);
            }
        });

        t.IsBackground = true;
        t.Start();
    }

    private void HandleCollisions()
    {
        var list = cars.Values.ToList();

        for (int i = 0; i < list.Count; i++)
        {
            for (int j = i + 1; j < list.Count; j++)
            {
                var a = list[i];
                var b = list[j];

                float dx = a.x - b.x;
                float dy = a.y - b.y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                float minDist = 0.7f; // радиус машин (0.35 + 0.35)

                if (dist < minDist)
                {
                    ResolveCollision(a, b, dx, dy, dist, minDist);
                }
            }
        }
    }

    private void ResolveCollision(CarState a, CarState b, float dx, float dy, float dist, float minDist)
    {
        if (dist < 0.001f)
            dist = 0.001f;

        float overlap = minDist - dist;

        float nx = dx / dist;
        float ny = dy / dist;

        // Раздвигаем машины поровну
        a.x += nx * (overlap / 2f);
        a.y += ny * (overlap / 2f);

        b.x -= nx * (overlap / 2f);
        b.y -= ny * (overlap / 2f);

        // Гасим скорость
        a.vx *= 0.3f;
        a.vy *= 0.3f;

        b.vx *= 0.3f;
        b.vy *= 0.3f;
    }

    private void SimulateCar(CarState c, float dt)
    {
        if (!c.initialized)
            return;
        float accel = 40f / 3f;
        float brake = 160f / 3f;
        float sideFriction = 5f / 3f;
        float steerBase = 150f ;
        float maxSpeed = 30.5f;
        float rad = (c.rot + 90f) * (float)Math.PI / 180f;
        float forwardX = (float)Math.Cos(rad);
        float forwardY = (float)Math.Sin(rad);

        float forwardSpeed = c.vx * forwardX + c.vy * forwardY;
        float speed = (float)Math.Sqrt(c.vx * c.vx + c.vy * c.vy);

        if (c.throttle > 0f)
        {
            // газ вперёд
            c.vx += -forwardX * accel * dt;
            c.vy += -forwardY * accel * dt;
        }
        else if (c.throttle < 0f)
        {
            if (forwardSpeed > 0.2f)
            {
                // тормоз
                c.vx += -forwardX * brake * dt;
                c.vy += -forwardY * brake * dt;
            }
            else
            {
                // задний ход
                c.vx += forwardX * accel * dt;
                c.vy += forwardY * accel * dt;
            }
        }


        float speedFactor = Math.Clamp(speed / 10f, 0f, 1f);
        float steerFactor = Lerp(2.0f, 0.4f, speedFactor);

        if (Math.Abs(c.steer) > 0.01f && Math.Abs(forwardSpeed) > 0.05f)
        {
            float direction = Math.Sign(forwardSpeed);
            float turn = c.steer * steerBase * steerFactor * direction * dt;
            c.rot += turn;

            float newRad = (c.rot + 90f) * (float)Math.PI / 180f;
            float newForwardX = (float)Math.Cos(newRad);
            float newForwardY = (float)Math.Sin(newRad);

            c.vx = newForwardX * forwardSpeed;
            c.vy = newForwardY * forwardSpeed;
        }

        float rightX = -forwardY;
        float rightY = forwardX;

        float sideSpeed = c.vx * rightX + c.vy * rightY;

        c.vx += -rightX * sideSpeed * sideFriction * dt;
        c.vy += -rightY * sideSpeed * sideFriction * dt;

        c.x += c.vx * dt;
        c.y += c.vy * dt;
        float currentSpeed = (float)Math.Sqrt(c.vx * c.vx + c.vy * c.vy);

        if (currentSpeed > maxSpeed)
        {
            float k = maxSpeed / currentSpeed;
            c.vx *= k;
            c.vy *= k;
        }

    }

    private float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }


    // Рассылка состояния мира всем по UDP
    private void BroadcastWorldState()
    {
        WorldPacket world;

        lock (cars)
        {
            world = new WorldPacket
            {
                cars = new List<WorldCarDto>()
            };

            foreach (var kv in cars)
            {
                world.cars.Add(new WorldCarDto
                {
                    id = kv.Key,
                    x = kv.Value.x,
                    y = kv.Value.y,
                    rot = kv.Value.rot,
                    vx = kv.Value.vx,
                    vy = kv.Value.vy
                });

            }
        }

        string json = JsonConvert.SerializeObject(world);
        byte[] data = Encoding.UTF8.GetBytes(json);

        List<IPEndPoint> targets;
        lock (carUdpEndpoints)
        {
            targets = new List<IPEndPoint>(carUdpEndpoints.Values);
        }

        foreach (var ep in targets)
        {
            try
            {
                udp.Send(data, data.Length, ep);
            }
            catch { }
        }
    }

    // Регистрация на мастер‑сервере
    private async void RegisterOnMaster()
    {
        try
        {
            var http = new HttpClient();

            var req = new
            {
                Name = name,
                Ip = GetLocalIP(),
                Port = port,
                Track = track,
                Laps = laps,
                MaxPlayers = maxPlayers
            };

            string json = JsonConvert.SerializeObject(req);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await http.PostAsync(masterServerUrl + "/register", content);
            string respText = await resp.Content.ReadAsStringAsync();

            var info = JsonConvert.DeserializeObject<ServerInfoResponse>(respText);
            serverId = info.Id;
            MasterConnected = true;

            Debug.Log("[MASTER] Registered with ID: " + serverId);
        }
        catch (Exception ex)
        {
            Debug.Log("[MASTER] Register failed: " + ex.Message);
            MasterConnected = false;
        }
    }

    // Heartbeat на мастер‑сервер
    private void StartHeartbeat()
    {
        var t = new Thread(async () =>
        {
            var http = new HttpClient();

            while (running)
            {
                try
                {
                    var req = new
                    {
                        Id = serverId,
                        CurrentPlayers = GetCurrentPlayers()
                    };

                    string json = JsonConvert.SerializeObject(req);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    await http.PostAsync(masterServerUrl + "/heartbeat", content);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[MASTER] Heartbeat failed: " + ex.Message);
                }

                Thread.Sleep(5000);
            }
        });

        t.IsBackground = true;
        t.Start();
    }

    private void Update_on_pla()
    {
        var http = new HttpClient();
        var req = new
        {
            Id = serverId,
            CurrentPlayers = GetCurrentPlayers()
        };

        string json = JsonConvert.SerializeObject(req);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        http.PostAsync(masterServerUrl + "/heartbeat", content);
    }

    private int GetCurrentPlayers()
    {
        lock (clients) return clientToId.Count;
    }

    private string GetLocalIP()
    {
        foreach (var ni in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
        {
            if (ni.AddressFamily == AddressFamily.InterNetwork)
                return ni.ToString();
        }
        return "127.0.0.1";
    }

    public void Stop()
    {
        running = false;

        try
        {
            SendShutdownSignal();
        }
        catch { }

        try { listener?.Stop(); } catch { }
        try { udp?.Close(); } catch { }

        lock (clients)
        {
            foreach (var c in clients)
            {
                try { c.Close(); } catch { }
            }
            clients.Clear();
        }

        lock (cars) cars.Clear();
        lock (clientToId) clientToId.Clear();
        lock (carUdpEndpoints) carUdpEndpoints.Clear();
    }

    private async void SendShutdownSignal()
    {
        var http = new HttpClient();

        var req = new
        {
            Id = serverId,
            CurrentPlayers = -1
        };

        string json = JsonConvert.SerializeObject(req);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        await http.PostAsync(masterServerUrl + "/heartbeat", content);
    }
}
