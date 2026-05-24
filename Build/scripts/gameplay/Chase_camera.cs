using UnityEngine;

public class Chase_camera : MonoBehaviour
{
    public float smooth = 5f;
    public float distanceBehind = 10f;
    public float rotationOffset = -180f;

    private Transform target;

    void Start()
    {
        // Периодически пытаемся найти машину игрока
        InvokeRepeating(nameof(FindPlayerCar), 0f, 1f);
    }

    void FindPlayerCar()
    {
        if (target != null) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;
    }


    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPos = target.position - target.up * distanceBehind;
        targetPos.z = transform.position.z;

        transform.position = Vector3.Lerp(transform.position, targetPos, smooth * Time.deltaTime);

        Quaternion targetRot = Quaternion.Euler(0, 0, target.eulerAngles.z + rotationOffset);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, smooth * Time.deltaTime);
    }
}
