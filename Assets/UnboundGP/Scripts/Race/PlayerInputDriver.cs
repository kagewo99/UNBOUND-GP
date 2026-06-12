using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>キーボード/パッド入力をドライバー入力に変換する。WASD/矢印 + Space(ブレーキ)。</summary>
    public class PlayerInputDriver : MonoBehaviour, IDriverInput
    {
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Steer { get; private set; }

        void Update()
        {
            float v = Input.GetAxisRaw("Vertical");
            Throttle = Mathf.Max(0f, v);
            Brake = Mathf.Max(0f, -v);
            if (Input.GetKey(KeyCode.Space)) Brake = 1f;
            Steer = Input.GetAxis("Horizontal");
        }
    }
}
