using UnityEngine;
using TMPro;

public class Speed_UI : MonoBehaviour
{
    public TMP_Text speedText;

    void Update()
    {
        var local = WorldManager.Instance.LocalPlayer;

        if (local == null)
        {
            speedText.text = "0 km/h";
            return;
        }

        float speed = Mathf.Sqrt(local.vx * local.vx + local.vy * local.vy) * 3.6f;
        speedText.text = ((int)speed * 3).ToString() + " km/h";
    }
}
