using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Assets.scripts.gameplay
{
    class Local_input_sender : MonoBehaviour
    {
        void Update()
        {
            if (TCP_client_connector.Instance == null) return;
            if (TCP_client_connector.Instance.ClientId == Guid.Empty) return;
            if (!TCP_client_connector.Instance.UdpReady) return;
            if (UI_countdown.Instance.gameObject.activeSelf == true) return;
            if (TCP_client_connector.Instance.IsDisconnected) return;

            float t = 0f;
            if (Input.GetKey(KeyCode.W)) t += 1f;
            if (Input.GetKey(KeyCode.S)) t -= 1f;

            float s = 0f;
            if (Input.GetKey(KeyCode.A)) s -= 1f;
            if (Input.GetKey(KeyCode.D)) s += 1f;


            var packet = new
            {
                type = "input",
                throttle = t,
                steer = s,
                id = TCP_client_connector.Instance.ClientId
            };

            TCP_client_connector.Instance.SendUDP(JsonConvert.SerializeObject(packet));
        }

    }
}
