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
        public float CurrentLateralG { get; private set; }
        /// <summary>車体スリップ角[deg]。HUD やエフェクト(タイヤスモーク)のフック用。</summary>
        public float SlipAngleDeg { get; private set; }

        Rigidbody rb;
        IDriverInput input;
        bool grounded;

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

            float u = Vector3.Dot(vel, fwd);                 // 前方
            float rightComp = Vector3.Dot(vel, right);       // 右成分
            Model.forwardSpeed = u;
            Model.lateralSpeed = -rightComp;                 // 内部は左が正
            Model.yawRate = -rb.angularVelocity.y;           // 内部は反時計回りが正

            CurrentSpeedMs = Mathf.Abs(u);

            // ---- ドライバーの状態が入力を歪める(Human GP の核心) ----
            float control = Condition != null ? Condition.ControlFactor : 1f;
            float throttle = InputEnabled ? Mathf.Clamp01(input.Throttle) * control : 0f;
            float brake = InputEnabled ? Mathf.Clamp01(input.Brake) : 1f;
            float steer = InputEnabled ? Mathf.Clamp(input.Steer, -1f, 1f) * Mathf.Lerp(0.25f, 1f, control) : 0f;

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

                CurrentLateralG = outp.lateralG;
                SlipAngleDeg = outp.slipAngleRad * Mathf.Rad2Deg;
            }
            else
            {
                // 空中:操舵を切り、物理に任せて自然落下させる
                CurrentLateralG = 0f;
            }

            Condition?.ReportG(CurrentLateralG, dt);
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
            transform.SetPositionAndRotation(position + Vector3.up * 0.6f, rotation);
        }
    }
}
