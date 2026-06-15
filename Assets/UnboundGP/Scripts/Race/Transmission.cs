using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// 変速機(ギアボックス)。F1 を範とした 8 速 + エンジン回転数(RPM)のシミュレーション。
    ///
    /// - 車速とギア比から RPM を算出し、RPM に応じた出力トルク係数を返す
    /// - レッドライン付近でトルクが垂れるため、上のギアへ繋ぐ意味が生まれる
    /// - マニュアル(パドル/キー)とオート、両対応
    /// - CVT 技術(無段変速)装備時は段が無く、常に出力ピーク付近に張り付く
    ///
    /// VehicleModel に「駆動トルク係数」を渡して加速の質感に反映し、RPM/ギアは HUD のタコメータへ。
    /// ギア比は車の最高速に合わせて初期化時に自動調整するので、研究で速くなっても破綻しない。
    /// </summary>
    [System.Serializable]
    public class Transmission
    {
        // F1 風の段間比(高速側ほど僅差)。最終減速比は最高速に合わせて自動算出する。
        public static readonly float[] GearRatios = { 2.90f, 2.30f, 1.90f, 1.62f, 1.40f, 1.22f, 1.08f, 0.96f };
        public const float WheelRadius = 0.33f;
        public const float IdleRPM = 3500f;
        public const float RedlineRPM = 13000f;
        public const float MaxRPM = 13500f;          // タコメータ上限
        const float ShiftUpRPM = 12300f;
        const float ShiftDownRPM = 7200f;
        const float ShiftCutSeconds = 0.07f;         // 変速中のトルクカット時間

        public bool IsCVT { get; private set; }
        public bool AutoShift = true;
        public int Gear { get; private set; } = 1;    // 1..8
        public float Rpm { get; private set; } = IdleRPM;
        /// <summary>直近の変速演出用フラッシュ(0→1で減衰)。HUD用。</summary>
        public float ShiftFlash { get; private set; }

        float finalDrive = 4.5f;
        float shiftCutTimer;
        float shiftCooldown;

        public int GearCount => GearRatios.Length;

        public string GearLabel
        {
            get
            {
                if (IsCVT) return "CVT";
                return Gear.ToString();
            }
        }

        /// <summary>最高速[m/s]に合わせてギア比を調整し、CVT 有無を設定する。</summary>
        public void Configure(float topSpeedMs, bool isCVT)
        {
            IsCVT = isCVT;
            Gear = 1;
            Rpm = IdleRPM;
            // 最高速・最上段でレッドラインの97%になるよう最終減速比を決める
            float topRatio = GearRatios[GearRatios.Length - 1];
            float wheelRps = Mathf.Max(topSpeedMs, 30f) / (2f * Mathf.PI * WheelRadius);
            finalDrive = (RedlineRPM * 0.97f) / Mathf.Max(wheelRps * topRatio * 60f, 1f);
        }

        float RpmFromSpeed(float speedMs, int gear)
        {
            float wheelRps = Mathf.Abs(speedMs) / (2f * Mathf.PI * WheelRadius);
            return Mathf.Max(IdleRPM * 0.4f, wheelRps * GearRatios[gear - 1] * finalDrive * 60f);
        }

        /// <summary>
        /// RPM に対する出力トルク係数。通常域では 0.8〜1.05 を保ち(=ここまで詰めた加速を壊さない)、
        /// レッドライン超で急落させて“上のギアへ繋ぐ必要”を作る。
        /// </summary>
        static float TorqueCurve(float rpm)
        {
            float n = rpm / RedlineRPM;
            float t;
            if (n < 0.18f) t = Mathf.Lerp(0.55f, 0.85f, n / 0.18f);     // 低回転は細い
            else if (n < 0.9f) t = Mathf.Lerp(0.85f, 1.05f, (n - 0.18f) / 0.72f); // 中高回転で太る
            else t = Mathf.Lerp(1.05f, 0.85f, (n - 0.9f) / 0.1f);       // ピーク後やや垂れる
            if (n > 1.0f) t *= Mathf.Clamp01((1.12f - n) / 0.12f);      // レッド超で急落
            return Mathf.Clamp(t, 0.05f, 1.05f);
        }

        /// <summary>
        /// 物理1ステップ。signedSpeedMs は前進が正。戻り値は駆動トルク係数(VehicleModelに渡す)。
        /// </summary>
        public float Tick(float signedSpeedMs, float throttle, float dt)
        {
            ShiftFlash = Mathf.Max(0f, ShiftFlash - dt * 3f);
            shiftCooldown = Mathf.Max(0f, shiftCooldown - dt);

            if (IsCVT)
            {
                // 無段変速:常に出力ピーク回転へ寄せる。段差なし。
                float target = Mathf.Lerp(RedlineRPM * 0.55f, RedlineRPM * 0.92f, Mathf.Clamp01(throttle));
                Rpm = Mathf.Lerp(Rpm, Mathf.Max(target, RpmFromSpeed(signedSpeedMs, GearCount) * 0.4f), 6f * dt);
                return 1.02f;
            }

            Rpm = RpmFromSpeed(signedSpeedMs, Gear);

            // オート変速
            if (AutoShift && shiftCooldown <= 0f)
            {
                if (Rpm > ShiftUpRPM && Gear < GearCount && throttle > 0.1f) DoShift(+1);
                else if (Rpm < ShiftDownRPM && Gear > 1) DoShift(-1);
            }
            // マニュアルでもレッド張り付き防止の保険アップシフト
            else if (!AutoShift && Rpm > RedlineRPM * 1.02f && Gear < GearCount && shiftCooldown <= 0f)
            {
                DoShift(+1);
            }

            // 変速中はトルクカット(駆動が一瞬抜ける=変速の手応え)
            if (shiftCutTimer > 0f)
            {
                shiftCutTimer -= dt;
                return 0.05f;
            }
            return TorqueCurve(Rpm) * Mathf.Clamp01(throttle <= 0f ? 1f : 1f);
        }

        public void ShiftUp() { if (!IsCVT) DoShift(+1); }
        public void ShiftDown() { if (!IsCVT) DoShift(-1); }

        void DoShift(int dir)
        {
            int next = Mathf.Clamp(Gear + dir, 1, GearCount);
            if (next == Gear) return;
            Gear = next;
            shiftCutTimer = ShiftCutSeconds;
            shiftCooldown = 0.25f;
            ShiftFlash = 1f;
        }

        /// <summary>オフスロットル時のエンジンブレーキ量(0..1)。RPMが高いほど強い。</summary>
        public float EngineBrake(float throttle)
        {
            if (IsCVT || throttle > 0.05f) return 0f;
            return Mathf.Clamp01(Rpm / RedlineRPM) * 0.18f;
        }
    }
}
