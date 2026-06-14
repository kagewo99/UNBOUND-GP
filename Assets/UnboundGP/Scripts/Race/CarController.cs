using UnboundGP.Core;
using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// VehicleModel(純粋物理)と Unity の Rigidbody をつなぐ薄い接着層。
    ///
    /// 役割:
    /// - 毎物理ステップ、Rigidbody の現在速度を内部標準座標系へ変換してモデルへ与え、
    ///   モデルが計算した速度・ヨーレートを書き戻す(衝突で削れた速度も自然に取り込む)
    /// - ドライバーの状態(失神・視野狭窄)で入力を歪め、横Gを DriverCondition へ報告する
    /// - ダウンフォースで路面に押し付け、橋の起伏でも飛ばない
    ///
    /// 物理そのものは VehicleModel 側にあり、オフラインのラップシムと同一式で動く。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        public MachineStats Stats { get; private set; }
        public DriverCondition Condition { get; private set; }
        public VehicleModel Model { get; private set; } = new VehicleModel();

        /// <summary>カウントダウン中は false。RaceManager が制御する。</summary>
        public bool InputEnabled = false;

        public float CurrentSpeedMs { get; private set; }
        public float CurrentSpeedKmh => CurrentSpeedMs * 3.6f;
        /// <summary>総合体感G。加速・制動・旋回・衝突をすべて含む“実際に身体が受けたG”。失神判定の根拠。</summary>
        public float CurrentG { get; private set; }
        /// <summary>横G(コーナリング成分の絶対値, HUD補助表示用)。</summary>
        public float CurrentLateralG { get; private set; }
        /// <summary>横G(符号付き, 右方向が正)。一人称カメラの頭振り演出に使う。</summary>
        public float LateralGSigned { get; private set; }
        /// <summary>縦G(符号付き, 加速が正・制動が負)。一人称カメラのピッチ演出に使う。</summary>
        public float LongitudinalGSigned { get; private set; }
        /// <summary>車体スリップ角[deg]。HUD やエフェクト(タイヤスモーク)のフック用。</summary>
        public float SlipAngleDeg { get; private set; }

        /// <summary>プレイヤー機のみ true。停止/低速で後退できる(スタック脱出用)。</summary>
        public bool AllowReverse = false;

        // 衝突などの瞬間的なGがゲームを壊さないよう、報告するGの上限。
        const float MaxReportG = 50f;

        Rigidbody rb;
        IDriverInput input;
        bool grounded;
        Vector3 prevVelocity;

        public void Setup(MachineStats stats, IDriverInput driverInput, DriverCondition condition)
        {
            Stats = stats;
            input = driverInput;
            Condition = condition;

            rb = GetComponent<Rigidbody>();
            rb.mass = stats.weightKg;
            rb.drag = 0f;          // 抵抗はモデル側で扱う
            rb.angularDrag = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.centerOfMass = new Vector3(0f, -0.2f, 0f);
            // ヨーと横転以外の余計な回転は抑える(プロトタイプの安定性優先)
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        void FixedUpdate()
        {
            if (rb == null || input == null) return;
            float dt = Time.fixedDeltaTime;

            grounded = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, 1.2f);

            // ---- Rigidbody → モデル状態へ取り込み(衝突結果を反映) ----
            // 平面の前方・右方向(車体の傾きを無視して水平面で扱う)
            Vector3 fwd = Flatten(transform.forward);
            Vector3 right = Flatten(transform.right);
            Vector3 vel = rb.velocity;
            float vyWorld = vel.y;

            float u = Vector3.Dot(vel, fwd);                 // 前方(後退時は負)
            float rightComp = Vector3.Dot(vel, right);       // 右成分
            Model.forwardSpeed = u;
            Model.lateralSpeed = -rightComp;                 // 内部は左が正
            Model.yawRate = -rb.angularVelocity.y;           // 内部は反時計回りが正

            CurrentSpeedMs = Mathf.Abs(u);

            // ---- 実際に身体が受けた加速度を“前フレームの実速度との差”から求める ----
            // 駆動・制動・旋回・衝突のすべてがここに現れる。重力(縦)は除外して水平成分のみ。
            Vector3 accelVec = (vel - prevVelocity) / Mathf.Max(dt, 1e-4f);
            prevVelocity = vel;
            Vector3 accelH = new Vector3(accelVec.x, 0f, accelVec.z);
            float instG = Mathf.Min(accelH.magnitude / 9.81f, MaxReportG);
            // 表示・判定は軽く平滑化(衝突の瞬間スパイクは通す)
            CurrentG = Mathf.Lerp(CurrentG, instG, 1f - Mathf.Exp(-12f * dt));
            LongitudinalGSigned = Vector3.Dot(accelH, fwd) / 9.81f;   // 加速+ / 制動-
            LateralGSigned = Vector3.Dot(accelH, right) / 9.81f;       // 右+ / 左-
            CurrentLateralG = Mathf.Abs(LateralGSigned);

            // ---- 入力(失神・後退・カウントダウンを織り込む) ----
            float control = Condition != null ? Condition.ControlFactor : 1f;
            float steer = InputEnabled ? Mathf.Clamp(input.Steer, -1f, 1f) * Mathf.Lerp(0.25f, 1f, control) : 0f;
            float throttle, brake;
            if (!InputEnabled)
            {
                throttle = 0f;
                brake = 1f;     // カウントダウン中は停止
            }
            else
            {
                float upI = Mathf.Clamp01(input.Throttle);
                float downI = Mathf.Clamp01(input.Brake);
                if (AllowReverse && downI > 0.01f && u < 2f)
                {
                    // 停止/低速でブレーキ入力 → 後退(スタック脱出)
                    throttle = -downI;
                    brake = 0f;
                }
                else
                {
                    throttle = upI * control;   // 失神でアクセルが鈍る
                    brake = downI;
                }
            }

            if (Condition != null && Condition.IsBlackedOut)
            {
                // 失神中:アクセルから足が落ち、ステアは固まる。マシンは慣性のまま壁へ向かう。
                throttle = 0f;
                brake = 0.15f;
                steer = 0f;
            }

            if (grounded)
            {
                // ---- 物理1ステップ ----
                var outp = Model.Step(dt, throttle, brake, steer, Stats);

                // ---- モデル状態 → Rigidbody へ書き戻し ----
                Vector3 planar = fwd * Model.forwardSpeed + right * (-Model.lateralSpeed);
                rb.velocity = new Vector3(planar.x, vyWorld, planar.z);
                rb.angularVelocity = new Vector3(0f, -Model.yawRate, 0f);

                // ---- ダウンフォースで接地(橋の登りでも飛ばない) ----
                float rr = CurrentSpeedMs / MachineStats.ReferenceSpeedMs;
                float stickG = Stats.downforceFactor * rr * rr * 0.6f;
                rb.AddForce(Vector3.down * (Stats.weightKg * 9.81f * stickG));

                SlipAngleDeg = outp.slipAngleRad * Mathf.Rad2Deg;
            }

            Condition?.ReportG(CurrentG, dt);
        }

        static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        /// <summary>コース上の指定位置へ復帰させる(スタック/失神事故からのリカバリ)。</summary>
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Model.Reset();
            prevVelocity = Vector3.zero;   // 復帰時にGスパイクを出さない
            CurrentG = 0f;
            transform.SetPositionAndRotation(position + Vector3.up * 0.6f, rotation);
        }
    }
}
