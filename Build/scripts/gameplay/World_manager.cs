using Assets.scripts.gameplay;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance;

    public GameObject[] carPrefabs; // 8 префабов машин
    public Transform[] startPositions; // 8 стартовых позиций

    private Dictionary<Guid, NetworkCar> cars = new();
    public NetworkCar LocalPlayer { get; private set; }
    private int buf_car_index = 0;

    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        //StartCoroutine(SendReady());
    }

    public void Set_bufIndex(int buf_car_index)
    {
        this.buf_car_index = buf_car_index;
    }

    public void ApplyWorldState(string json)
    {

        var packet = JsonConvert.DeserializeObject<WorldPacket>(json);
        if (packet == null || packet.type != "world") return;

        foreach (var c in packet.cars)
        {
            if (!cars.ContainsKey(c.id))
                SpawnCar(c.id);
            var car = cars[c.id];
            car.ApplyState(c.x, c.y, c.rot, c.vx, c.vy);
        }
    }

    public void DespawnCar(Guid id)
    {
        if (!cars.ContainsKey(id))
            return;

        Destroy(cars[id].gameObject);
        cars.Remove(id);

        if (LocalPlayer != null && LocalPlayer.id == id)
            LocalPlayer = null;
    }


    internal void SpawnCar(Guid id)
    {
        int carIndex = TCP_client_connector.Instance.CarIndex;
        bool isLocal = (id == TCP_client_connector.Instance.ClientId);
        if (!isLocal)
        {
            TCP_client_connector.Instance.Get_Car_index(id);
            carIndex = buf_car_index;
        }
        Transform pos = startPositions[carIndex];
        GameObject prefab = carPrefabs[carIndex];

        GameObject obj = Instantiate(prefab, pos.position, pos.rotation);
        Debug.Log($"[WORLD] Spawning car {id} at {pos.position} in {pos.rotation}");
        Debug.Log($"[WORLD] pos arr: {string.Join(", ", startPositions.Select(p => p.position.ToString()))}");
        Debug.Log($"[WORLD] prefab arr: {string.Join(", ", carPrefabs.Select(p => p.name))}");
        Debug.Log($"[WORLD] Start index: {carIndex}, Car index: {carIndex}");
        var nc = obj.AddComponent<NetworkCar>();

        nc.id = id;
        nc.isLocal = isLocal;

        cars[id] = nc;

        if (nc.isLocal)
        {
            obj.tag = "Player";
            LocalPlayer = nc;
            obj.AddComponent<Local_input_sender>();
            SendStartPositionToServer(obj.transform.position, obj.transform.rotation.eulerAngles.z);
        }
    }

    private void SendStartPositionToServer(Vector3 pos, float rot)
    {
        var packet = new
        {
            type = "set_start",
            id = TCP_client_connector.Instance.ClientId,
            x = pos.x,
            y = pos.y,
            rot = rot
        };

        TCP_client_connector.Instance.SendUDP(JsonConvert.SerializeObject(packet));
    }


    // DTO для world‑state
    class WorldPacket
    {
        public string type;
        public List<CarDto> cars;
    }

    class CarDto
    {
        public Guid id;
        public float x, y, rot;
        public float vx, vy;
    }
}
