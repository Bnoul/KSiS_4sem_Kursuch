using Assets.scripts.gameplay;
using Newtonsoft.Json;
using System;
using UnityEngine;

public class NetworkCar : MonoBehaviour
{
    public Guid id;
    public bool isLocal;

    public float x, y, rot;
    public float vx, vy;

    private Vector3 targetPos;
    public LayerMask wallMask;
    private Vector3 lastSafePos;
    private Quaternion targetRot;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = gameObject.AddComponent<Rigidbody2D>();
        //rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0;
        rb.mass = 5;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var col = gameObject.AddComponent<CircleCollider2D>();
        col.radius = 0.35f;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!isLocal) return;

        var packet = new
        {
            type = "collision",
            id = id,
            safeX = lastSafePos.x,
            safeY = lastSafePos.y
        };

        TCP_client_connector.Instance.SendUDP(JsonConvert.SerializeObject(packet));
    }


    void FixedUpdate()
    {
        lastSafePos = transform.position;

        Vector3 newPos = Vector3.Lerp(transform.position, targetPos, 0.25f);
        if (!Physics2D.CircleCast(newPos, 0.35f, Vector2.zero, 0f, wallMask))
        {
            transform.position = newPos;
        }
        else
        {
            transform.position = lastSafePos;
        }

        transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, 0.25f);

    }


    public void ApplyState(float nx, float ny, float nrot, float nvx, float nvy)
    {
        x = nx;
        y = ny;
        rot = nrot;
        vx = nvx;
        vy = nvy;

        targetPos = new Vector3(x, y, -2);
        targetRot = Quaternion.Euler(0, 0, rot);
    }
}
