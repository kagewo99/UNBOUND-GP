using UnboundGP.Core;
using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// マシン挙動の“純粋な物理計算”。Unity の Rigidbody や Transform に一切依存しないため、
    /// オフラインのラップシミュレーションと完全に同一の式を共有できる(挙動の単一の真実)。
    ///
    /// モデルはスリップアングルに基づく二輪(バイシクル)モデル:
    /// - 前後アクスルそれぞれのスリップ角からタイヤ横力を求める(飽和カーブ=接地感)
    /// - 加減速で前後の荷重が移動し、グリップ配分が変わる(トレイルブレーキ/パワーオーバー)
    /// - 駆動はリア(RWD)。フリクションサークルで縦と横のグリップを取り合う
    ///   → アクセルを開けるとリアが流れ、戻すと収まる「当てられるオーバーステア」が出る
    /// - 低速ではキネマティック操舵へブレンドし、停止付近でも素直に取り回せる
    ///
    /// MachineStats.MaxCorneringG が“タイヤ摩擦係数(g単位, ダウンフォース込み)”として効くので、
    /// ファンカーやアクティブエアロの数値がそのまま接地限界に反映される。
    ///
    /// 内部座標系は制御工学の標準形(x=前方, y=左, ヨーr=反時計回りが正)。
    /// Unity 座標 (右手前方+Z/右+X/ヨー右回り) との変換は CarController が境界で行う。
    /// </summary>
    [System.Serializable]
    public class VehicleModel
    {
        // ---- 車両諸元(プロトタイプ既定値) ----
        public const float WheelBase = 3.4f;     // L
        public const float CogToFront = 1.85f;   // lf (前寄りに置くと後輪荷重が増える=トラクション寄り)
        public const float CogToRear = 1.55f;    // lr
        public const float CogHeight = 0.30f;    // h 低重心
        public const float MaxSteerDeg = 26f;
        public const float TireB = 18f;          // タイヤカーブの初期勾配(高いほど小さな滑りで食う=ダルつかない)
        public const float TireC = 1.15f;        // 形状(1付近で限界が穏やか=スナップしにくく御しやすい)
        const float G = 9.81f;

        // アーケード安定化:キーボード(舵が実質ON/OFF)でも破綻しないよう、
        // 横滑りとヨーの暴れを毎ステップ穏やかに減衰させる。限界域の挙動は残しつつ、
        // 「とんでもなく横に流れ続ける」のを防いで“狙った所へ行く”手応えにする。
        public const float GripAssist = 3.0f;    // 横速度の追加減衰 [1/s]
        public const float YawDamp = 1.2f;       // ヨーレートの追加減衰 [1/s]

        // ---- 状態(内部標準座標系) ----
        public float forwardSpeed;   // u  [m/s] 前方
        public float lateralSpeed;   // v  [m/s] 左が正
        public float yawRate;        // r  [rad/s] 反時計回りが正

        float lastLongAccel;         // 荷重移動の推定に使う前ステップの縦加速度

        /// <summary>1ステップの計算結果(主にテレメトリ表示用)。</summary>
        public struct Output
        {
            public float speed;          // 対地速度の大きさ [m/s]
            public float lateralG;       // 横G(絶対値)
            public float lateralAccel;   // 横加速度(符号付き, 左が正)[m/s^2] 一人称の頭振り用
            public float slipAngleRad;   // 車体スリップ角(挙動の乱れ表示用)
            public float rearGripUsage;  // リアの縦グリップ使用率 0..1(オーバーステア予兆)
        }

        /// <summary>状態を初期化する(リセット時)。</summary>
        public void Reset(float forward = 0f)
        {
            forwardSpeed = forward;
            lateralSpeed = 0f;
            yawRate = 0f;
            lastLongAccel = 0f;
        }

        /// <summary>
        /// 物理を1ステップ進める。
        /// steerInput は右が正(+1)。throttle/brake は 0..1。
        /// </summary>
        public Output Step(float dt, float throttle, float brake, float steerInput, MachineStats stats)
        {
            float m = Mathf.Max(stats.weightKg, 1f);
            float u = forwardSpeed;
            float v = lateralSpeed;
            float r = yawRate;
            float absU = Mathf.Abs(u);
            float uEff = Mathf.Max(absU, 0.5f); // スリップ角の分母がゼロ割れしないように

            // タイヤ摩擦係数(g単位)。ダウンフォースは速度依存で MaxCorneringG に含まれる。
            float mu = stats.MaxCorneringG(uEff);

            // 速度感応ステア:高速ほど舵角を絞って神経質さを抑える
            float steerScale = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(absU / 90f));
            // 右入力(+)を右旋回(内部CCW負)へ:delta は左が正なので符号反転
            float delta = -steerInput * (MaxSteerDeg * Mathf.Deg2Rad) * steerScale;

            // ---- スリップ角 ----
            float alphaF = Mathf.Atan2(v + CogToFront * r, uEff) - delta;
            float alphaR = Mathf.Atan2(v - CogToRear * r, uEff);

            // ---- 荷重移動つき垂直荷重 ----
            float staticNf = m * G * CogToRear / WheelBase;
            float staticNr = m * G * CogToFront / WheelBase;
            float transfer = m * lastLongAccel * CogHeight / WheelBase;
            float Nf = Mathf.Max(staticNf - transfer, 0.05f * m * G);
            float Nr = Mathf.Max(staticNr + transfer, 0.05f * m * G);

            float FyfMax = mu * Nf;
            float FyrMax = mu * Nr;

            // ---- タイヤ横力(スリップに抗する向き・飽和カーブ) ----
            float Fyf = -FyfMax * TireCurve(alphaF);
            float Fyr = -FyrMax * TireCurve(alphaR);

            // ---- 縦力(駆動はリア、制動は前後配分) ----
            // throttle は符号付き:正=前進、負=後退(プレイヤーのスタック脱出用)。
            float topMs = Mathf.Max(stats.EffectiveTopSpeedMs, 1f);
            float Fdrive;
            if (throttle >= 0f)
            {
                float falloff = Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Clamp01(absU / topMs), 2f));
                Fdrive = throttle * m * stats.acceleration * falloff;            // リア駆動(前進)
            }
            else
            {
                // 後退:駆動力は控えめ、後退最高速(約14m/s)で頭打ち
                const float reverseTop = 14f;
                float backSpeed = u < 0f ? -u : 0f;
                float revFalloff = Mathf.Max(0f, 1f - Mathf.Clamp01(backSpeed / reverseTop));
                Fdrive = throttle * m * stats.acceleration * 0.5f * revFalloff;  // throttle<0
            }
            // 制動力は低速で絞る:止まった車をブレーキ力で動かさない(偽G・微振動の根絶)
            float Fbrake = brake * m * stats.BrakeDecel(uEff) * Mathf.Clamp01(absU / 1.0f);
            float dir = u >= 0f ? 1f : -1f;
            float FxFront = -0.60f * Fbrake * dir;
            float FxRear = Fdrive - 0.40f * Fbrake * dir;

            // ---- フリクションサークル:縦に使った分だけ横グリップが減る ----
            float rearUse = Mathf.Clamp01(Mathf.Abs(FxRear) / Mathf.Max(FyrMax, 1f));
            float rearLat = FyrMax * Mathf.Sqrt(Mathf.Max(0f, 1f - rearUse * rearUse));
            Fyr = Mathf.Clamp(Fyr, -rearLat, rearLat);

            float frontUse = Mathf.Clamp01(Mathf.Abs(FxFront) / Mathf.Max(FyfMax, 1f));
            float frontLat = FyfMax * Mathf.Sqrt(Mathf.Max(0f, 1f - frontUse * frontUse));
            Fyf = Mathf.Clamp(Fyf, -frontLat, frontLat);

            // ---- 走行抵抗(空気抵抗+転がり) ----
            // 転がり抵抗は速度ゼロ付近で滑らかに消す(dir の符号反転による微振動=偽Gを防ぐ)
            float cdA = (m * stats.acceleration * 0.25f) / (topMs * topMs);
            float rollDir = Mathf.Clamp(u / 0.5f, -1f, 1f);
            float Fres = (cdA * u * absU) + (m * 0.015f * G * rollDir);

            // ---- 運動方程式(内部標準座標系) ----
            float cosD = Mathf.Cos(delta);
            float Fx = FxFront + FxRear - Fres;
            float Fy = Fyf * cosD + Fyr;

            float du = (Fx / m + v * r) * dt;          // m(u̇ − v r)=Fx
            float dv = (Fy / m - u * r) * dt;          // m(v̇ + u r)=Fy
            float Iz = m * CogToFront * CogToRear;      // ヨー慣性の近似
            float dr = ((CogToFront * Fyf * cosD - CogToRear * Fyr) / Iz) * dt;

            u += du;
            v += dv;
            r += dr;

            // ---- 低速はキネマティック操舵へブレンド(取り回し&ジッタ抑制) ----
            if (absU < 6f)
            {
                float kinR = u * Mathf.Tan(delta) / WheelBase; // 二輪幾何のヨー
                float blend = 1f - Mathf.Clamp01(absU / 6f);
                r = Mathf.Lerp(r, kinR, blend);
                v = Mathf.Lerp(v, 0f, blend * 0.5f);
            }
            else
            {
                // ---- アーケード安定化(走行中のみ) ----
                v *= Mathf.Max(0f, 1f - GripAssist * dt);
                r *= Mathf.Max(0f, 1f - YawDamp * dt);
            }

            // ---- 停止保持 ----
            // 駆動入力がほぼ無く、ほぼ止まっているなら 0 へ“穏やかに”寄せる(静止摩擦)。
            // 急に0にすると急減速G(偽G)に見えるため、約3m/s^2 でゆっくり詰める。
            if (Mathf.Abs(throttle) < 0.02f && Mathf.Abs(u) < 0.3f)
            {
                u = Mathf.MoveTowards(u, 0f, 3f * dt);
                v *= 0.2f;
                r *= 0.2f;
            }

            lastLongAccel = Fx / m;
            forwardSpeed = u;
            lateralSpeed = v;
            yawRate = r;

            float lateralAccel = Fy / m;
            return new Output
            {
                speed = Mathf.Sqrt(u * u + v * v),
                lateralG = Mathf.Abs(lateralAccel) / G,
                lateralAccel = lateralAccel,
                slipAngleRad = Mathf.Atan2(v, uEff),
                rearGripUsage = rearUse,
            };
        }

        /// <summary>
        /// 正規化タイヤカーブ。Pacejka を簡略化した sin(C·atan(B·α))。
        /// 0付近は線形、ピーク後はわずかに垂れて“限界を越えた感触”を出す。戻り値は -1..1 付近。
        /// </summary>
        static float TireCurve(float slipRad)
        {
            return Mathf.Sin(TireC * Mathf.Atan(TireB * slipRad));
        }
    }
}
