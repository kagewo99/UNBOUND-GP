using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// プレイヤー入力。キーボードとレーシングホイールの両方を読み、併用できるよう統合する。
    ///
    /// - キーボード: W/S(アクセル/ブレーキ) + A/D・←/→(操舵) + Space(ブレーキ) + E/Q/T(シフト)
    /// - ホイール:   WheelMapping で割り当てた軸(ステア/アクセル/ブレーキ)
    ///
    /// キーボードは Unity の Horizontal/Vertical 軸を使わず“明示キー”で読む。これらの軸は
    /// 接続中のジョイスティック(ホイール)の軸も合算するため、ホイールのドリフトで
    /// キーボード操作が汚染・上書きされるのを防ぐ。ホイール未割り当てでもキーボードは常に動く。
    /// </summary>
    public class PlayerInputDriver : MonoBehaviour, IDriverInput
    {
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Steer { get; private set; }

        WheelMapping wheel;

        // キーボード操舵の平滑化(明示キーは0/1なので、アナログ的なランプと自動センタリングを自前で持つ)
        float kSteerSmoothed;
        const float SteerTurnRate = 2.8f;    // フルロックまで約0.36秒
        const float SteerCenterRate = 4.5f;  // 手を離したときの戻りは速め

        // シフト操作のラッチ(Update で立て、FixedUpdate 側が Consume で消費する)
        bool shiftUpLatched, shiftDownLatched, toggleAutoLatched;

        void Awake()
        {
            wheel = WheelMapping.Load();
        }

        /// <summary>シフトアップ要求を取り出してクリア(1回分)。</summary>
        public bool ConsumeShiftUp() { bool v = shiftUpLatched; shiftUpLatched = false; return v; }
        public bool ConsumeShiftDown() { bool v = shiftDownLatched; shiftDownLatched = false; return v; }
        public bool ConsumeToggleAuto() { bool v = toggleAutoLatched; toggleAutoLatched = false; return v; }

        /// <summary>キャリブレーション直後など、外部から最新のマッピングを反映する。</summary>
        public void SetWheelMapping(WheelMapping mapping) => wheel = mapping;

        /// <summary>ジョイスティック/ホイールが1つでも接続されているか。</summary>
        static bool AnyJoystickConnected()
        {
            var names = Input.GetJoystickNames();
            for (int i = 0; i < names.Length; i++)
                if (!string.IsNullOrEmpty(names[i])) return true;
            return false;
        }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);

            // ---- キーボード(すべて明示キー。ジョイスティック軸を一切経由しない)----
            bool up = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            bool down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            float kThrottle = up ? 1f : 0f;
            float kBrake = down ? 1f : 0f;
            if (Input.GetKey(KeyCode.Space)) kBrake = 1f;

            // 操舵: A/D・←/→ を目標に、ランプで滑らかに寄せる(自動センタリング)
            float steerTarget = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) steerTarget -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) steerTarget += 1f;
            float rate = Mathf.Approximately(steerTarget, 0f) ? SteerCenterRate : SteerTurnRate;
            kSteerSmoothed = Mathf.MoveTowards(kSteerSmoothed, steerTarget, rate * dt);
            float kSteer = kSteerSmoothed;

            // ---- ホイール(接続されていて、かつ割り当て済みのときだけ読む)----
            // 未接続だと軸が0を返し、ペダル較正(離す=-1等)次第で ReadBrake が 0.5 等の
            // “幽霊入力”を返してしまう。接続判定で遮断し、キーボード操作を巻き込ませない。
            float wThrottle = 0f, wBrake = 0f, wSteer = 0f;
            if (wheel != null && AnyJoystickConnected())
            {
                wThrottle = wheel.ReadAccel();
                wBrake = wheel.ReadBrake();
                wSteer = wheel.ReadSteer();
            }

            // ---- 統合(強い方を採用)----
            Throttle = Mathf.Max(kThrottle, wThrottle);
            Brake = Mathf.Max(kBrake, wBrake);
            Steer = Mathf.Abs(wSteer) > Mathf.Abs(kSteer) ? wSteer : kSteer;

            // ---- シフト操作(キーボード or ホイールのパドル/ボタン) ----
            // シフトアップ: E / 右Shift / ホイールボタン(右パドル想定)
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightShift)
                || Input.GetKeyDown(KeyCode.JoystickButton5) || Input.GetKeyDown(KeyCode.JoystickButton1))
                shiftUpLatched = true;
            // シフトダウン: Q / 左Shift / ホイールボタン(左パドル想定)
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftShift)
                || Input.GetKeyDown(KeyCode.JoystickButton4) || Input.GetKeyDown(KeyCode.JoystickButton0))
                shiftDownLatched = true;
            // オート/マニュアル切替: T
            if (Input.GetKeyDown(KeyCode.T)) toggleAutoLatched = true;
        }
    }
}
