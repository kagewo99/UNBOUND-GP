using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// プレイヤー入力。キーボードとレーシングホイールの両方を読み、併用できるよう統合する。
    ///
    /// - キーボード: WASD/矢印 + Space(ブレーキ)
    /// - ホイール: WheelMapping で割り当てた軸(ステア/アクセル/ブレーキ)
    ///
    /// ホイールが未割り当て(キャリブレーション前)でもキーボードがそのまま使えるよう、
    /// 各入力はキーボード値とホイール値の“強い方”を採用する。
    /// </summary>
    public class PlayerInputDriver : MonoBehaviour, IDriverInput
    {
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Steer { get; private set; }

        WheelMapping wheel;

        void Awake()
        {
            wheel = WheelMapping.Load();
        }

        /// <summary>キャリブレーション直後など、外部から最新のマッピングを反映する。</summary>
        public void SetWheelMapping(WheelMapping mapping) => wheel = mapping;

        void Update()
        {
            // ---- キーボード ----
            float v = Input.GetAxisRaw("Vertical");
            float kThrottle = Mathf.Max(0f, v);
            float kBrake = Mathf.Max(0f, -v);
            if (Input.GetKey(KeyCode.Space)) kBrake = 1f;
            float kSteer = Input.GetAxis("Horizontal");

            // ---- ホイール ----
            float wThrottle = 0f, wBrake = 0f, wSteer = 0f;
            if (wheel != null)
            {
                wThrottle = wheel.ReadAccel();
                wBrake = wheel.ReadBrake();
                wSteer = wheel.ReadSteer();
            }

            // ---- 統合(強い方を採用。ホイール優先のステアはキーボードより値が大きいとき) ----
            Throttle = Mathf.Max(kThrottle, wThrottle);
            Brake = Mathf.Max(kBrake, wBrake);
            Steer = Mathf.Abs(wSteer) >= Mathf.Abs(kSteer) ? wSteer : kSteer;
        }
    }
}
