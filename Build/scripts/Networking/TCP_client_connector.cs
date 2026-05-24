using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;


public class TCP_client_connector : MonoBehaviour
{
    public static TCP_client_connector Instance;
    private ConcurrentQueue<string> udpPackets = new ConcurrentQueue<string>();
    private TcpClient client;
    private NetworkStream stream;
    private UdpClient udp;
    private IPEndPoint serverUdpEndPoint;
    private Thread udpListenThread;
    private bool udpRunning = false;
    public bool IsDisconnected { get; private set; } = false;

    public Guid ClientId { get; private set; }
    class WelcomePacket
    {
        public string type;
        public Guid id;
        public int startIndex;
    }
    class WinnerPacket
    {
        public string type;
        public string name;
    }
    class RaceStartPacket
    {
        public string type;
        public int lapsToWin;
    }
    class CountdownPacket
    {
        public string type;
        public int timeLeft;
    }
    class IndexPacket
    {
        public string type;
        public int carIndex;
    }
    class DespawnPacket
    {
        public string type;
        public Guid id;
    }


    public bool UdpReady { get; private set; } = false;
    public int StartIndex { get; private set; }
    public int CarIndex;
    private int udpPort;

    void Update()
    {
        while (udpPackets.TryDequeue(out var json))
        {
            HandleUdpPacket(json);
        }
    }


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    public bool Connect(string ip, int port)
    {
        try
        {
            client = new TcpClient();

            string localIp = GetLocalIPAddress();

            if (IsSameLocalNetwork(localIp, ip))
            {
                client.Connect(ip, port);
            }
            else
            {
                client.Connect(ip, port);
            }

            stream = client.GetStream();

            string welcome = Receive();


            if (string.IsNullOrEmpty(welcome))
            {
                Debug.LogError("[CLIENT] Welcome packet is NULL or empty!");
                return false;
            }

            var packet = JsonConvert.DeserializeObject<WelcomePacket>(welcome);

            if (packet == null)
            {
                Debug.LogError("[CLIENT] Failed to parse welcome packet!");
                return false;
            }


            ClientId = packet.id;
            StartIndex = packet.startIndex;
            CarIndex = packet.startIndex;

            udpPort = port + 1;

            StartUDP(ip, udpPort);

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[CLIENT] Connect error: " + ex.Message);
            return false;
        }
    }

    internal void Get_Car_index(Guid id)
    {
        var packet = new
        {
            type = "ind",
            id = id,
        };

        SendUDP(JsonConvert.SerializeObject(packet));
    }
    private void StartUDP(string ip, int udpPort)
    {

        udp = new UdpClient(0);
        udpRunning = true;

        string localIp = GetLocalIPAddress();

        if (IsSameLocalNetwork(localIp, ip))
        {
            serverUdpEndPoint = new IPEndPoint(IPAddress.Parse(ip), udpPort);
        }
        else
        {
            serverUdpEndPoint = new IPEndPoint(IPAddress.Parse(ip), udpPort);
        }

        var hello = new
        {
            type = "hello_udp",
            id = ClientId
        };

        new Thread(() =>
        {
            Thread.Sleep(500);
            if (IsDisconnected) return;
            var hello = new { type = "hello_udp", id = ClientId };
            SendUDP(JsonConvert.SerializeObject(hello));

            UdpReady = true;
        }).Start();




        udpListenThread = new Thread(UDPListenLoop);
        udpListenThread.IsBackground = true;
        udpListenThread.Start();
    }

    public void SendUDP(string msg)
    {
        if(IsDisconnected) return;
        if (udp == null) return;
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(msg);
            udp.Send(data, data.Length, serverUdpEndPoint);
        }
        catch { }
    }

    private void UDPListenLoop()
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);


        while (udpRunning)
        {
            try
            {
                byte[] data = udp.Receive(ref remote);
                string json = Encoding.UTF8.GetString(data);
                if(!udpRunning) break;
                udpPackets.Enqueue(json);
            }
            catch (Exception ex)
            {
                if (udpRunning)
                    Debug.LogError("[CLIENT] UDP error: " + ex.Message);
                else
                    break;
            }

        }
    }

    private void HandleUdpPacket(string json)
    {
        if (WorldManager.Instance == null)
        {
            Debug.LogWarning("[CLIENT] WorldManager not ready, skipping packet");
            return;
        }
        if (json.Contains("\"type\":\"winner\""))
        {
            var w = JsonConvert.DeserializeObject<WinnerPacket>(json);
            WinnerMessageBox.Instance.Show(w.name);
            return;
        }
        if (json.Contains("\"type\":\"race_start\""))
        {
            var rs = JsonConvert.DeserializeObject<RaceStartPacket>(json);
            Lap_menager.Instance.laps_change(rs.lapsToWin);
            UI_countdown.Instance.gameObject.SetActive(false);
            return;
        }
        if (json.Contains("\"type\":\"countdown\""))
        {
            var cd = JsonConvert.DeserializeObject<CountdownPacket>(json);
            UI_countdown.Instance.SetTime(cd.timeLeft);
            return;
        }
        if (json.Contains("\"type\":\"despawn\""))
        {
            var d = JsonConvert.DeserializeObject<DespawnPacket>(json);
            WorldManager.Instance.DespawnCar(d.id);
            return;
        }
        if (json.Contains("\"type\":\"index\""))
        {
            var d = JsonConvert.DeserializeObject<IndexPacket>(json);
            WorldManager.Instance.Set_bufIndex(d.carIndex);
            return;
        }



        WorldManager.Instance.ApplyWorldState(json);

    }


    public void SendName(string name)
    {
        if (stream == null) return;

        var packet = new
        {
            type = "set_name",
            name = name
        };

        string json = JsonConvert.SerializeObject(packet);
        Send(json);
    }

    public void Send(string msg)
    {
        if (stream == null) return;
        byte[] data = Encoding.UTF8.GetBytes(msg);
        stream.Write(data, 0, data.Length);
    }

    public string Receive()
    {
        byte[] buffer = new byte[1024];
        int bytes = stream.Read(buffer, 0, buffer.Length);

        Debug.Log("[CLIENT] TCP received " + bytes + " bytes");

        string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
        Debug.Log("[CLIENT] TCP message: " + msg);

        return msg;
    }

    public static string GetLocalIPAddress()
    {
        foreach (var ni in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
        {
            if (ni.AddressFamily == AddressFamily.InterNetwork)
                return ni.ToString();
        }
        return "127.0.0.1";
    }

    public static bool IsSameLocalNetwork(string ip1, string ip2)
    {
        try
        {
            var a1 = ip1.Split('.');
            var a2 = ip2.Split('.');

            // Сравниваем первые 3 октета — стандартная маска 255.255.255.0
            return a1[0] == a2[0] && a1[1] == a2[1] && a1[2] == a2[2];
        }
        catch
        {
            return false;
        }
    }
    public void Disconnect()
    {
        try
        {
            var packet = new
            {
                type = "disconnect",
                id = ClientId
            };
            SendUDP(JsonConvert.SerializeObject(packet));
        }
        catch { }
        IsDisconnected = true;
        udpRunning = false;
        try
        {
            udp?.Send(new byte[1], 1, serverUdpEndPoint);
        } catch { }
        try { 
            udp?.Close();
        }
        catch { }
        udp = null;
        try { stream?.Close(); } catch { }
        try { client?.Close(); } catch { }

        client = null;
        stream = null;

        Console.WriteLine("[CLIENT] Disconnected from server");
    }

    public void Stop()
    {
        udpRunning = false;

        try { udp?.Close(); } catch { }
        try { client?.Close(); } catch { }
    }

}
