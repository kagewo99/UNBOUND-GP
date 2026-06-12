using UnboundGP.Core;
using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// アーケード寄りの簡易マシン物理。MachineStats の数値が体感に直結するよう、
    /// 「旋回限界G → 許容ヨーレート」という1本の式に集約している。
    ///
    /// - グリップ限界以内でしか曲がれない (限界超過の要求は単に曲がらない=アンダー)
    /// - ダウンフォースは速度の2乗で効き、ファンカーは速度ゼロから効く
    /// - 横Gを毎ステップ DriverCondition に報告し、人間ドライバーなら意識が削れる
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        public MachineStats Stats { get; private set; }
        public DriverCondition Condition { get; private set; }

        /// <summary>カウントダウン中は false。RaceManager が制御する。</summary>
        public bool InputEnabled = false;

        public float CurrentSpeedMs { get; private set; }
        public float CurrentSpeedKmh => CurrentSpeedMs * 3.6f;
        public float CurrentLateralG { get; private set; }

        const float WheelBase = 3.4f;
        const float MaxSteerAngleRad = 28f * Mathf.Deg2Rad;

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
            rb.drag = 0.05f;
            rb.angularDrag = 4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.centerOfMass = new Vector3(0f, -0.25f, 0f); // 低重心で横転を抑制
        }

        void FixedUpdate()
        {
            if (rb == null || input == null) return;
            float dt = Time.fixedDeltaTime;

            grounded = Physics.Raycast(transform.position + Vector3.up * 0.4f, Vector3.down, 1.1f);
            float speed = Vector3.Dot(rb.velocity, transform.forward);
            CurrentSpeedMs = Mathf.Abs(speed);

            // ---- ドライバーの状態が入力を歪める (Human GP の核心) ----
            float control = Condition != null ? Condition.ControlFactor : 1f;
            float throttle = InputEnabled ? Mathf.Clamp01(input.Throttle) * control : 0f;
            float brake = InputEnabled ? Mathf.Clamp01(input.Brake) : 1f;
            float steer = InputEnabled ? Mathf.Clamp(input.Steer, -1f, 1f) * Mathf.Lerp(0.2f, 1f, control) : 0f;

            if (Condition != null && Condition.IsBlackedOut)
            {
                // 失神中: アクセルから足が落ち、ステアリングは固まる。マシンは慣性のまま壁へ向かう。
                throttle = 0f;
                brake = 0.15f;
                steer = 0f;
            }

            if (grounded)
            {
                // ---- 駆動 ----
                float topMs = Stats.EffectiveTopSpeedMs;
                if (speed < topMs)
                {
                    float falloff = 1f - Mathf.Pow(Mathf.Clamp01(speed / topMs), 3f);
                    rb.AddForce(transform.forward * (Stats.weightKg * Stats.acceleration * throttle * falloff));
                }

                // ---- 制動 ----
                if (brake > 0.01f && speed > 0.5f)
                {
                    rb.AddForce(-transform.forward * (Stats.weightKg * Stats.BrakeDecel(speed) * brake));
                }

                // ---- 旋回: グリップ限界がヨーレートの上限を決める ----
                float absSpeed = Mathf.Max(CurrentSpeedMs, 0.1f);
                float maxLatAccel = Stats.MaxCorneringG(absSpeed) * 9.81f;
                float gripYaw = maxLatAccel / Mathf.Max(absSpeed, 6f);
                // 操舵角による幾何的上限: yaw = v / R, R = ホイールベース / tan(舵角)
                float geomYaw = absSpeed * Mathf.Tan(MaxSteerAngleRad) / WheelBase;
                float yawRate = steer * Mathf.Min(gripYaw, geomYaw);

                rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yawRate * Mathf.Rad2Deg * dt, 0f));
                CurrentLateralG = Mathf.Abs(speed * yawRate) / 9.81f;

                // ---- 横滑りの収束 (タイヤが横方向の速度を食う) ----
                Vector3 forwardVel = transform.forward * speed;
                Vector3 verticalVel = Vector3.up * rb.velocity.y;
                Vector3 lateralVel = rb.velocity - forwardVel - verticalVel;
                rb.velocity = forwardVel + verticalVel + lateralVel * Mathf.Clamp01(1f - 6f * dt);

                // ---- ダウンフォースで路面に押し付ける (橋の登りでも飛ばない) ----
                float r = absSpeed / MachineStats.ReferenceSpeedMs;
                float stickG = Stats.downforceFactor * r * r * 0.6f;
                rb.AddForce(Vector3.down * (Stats.weightKg * 9.81f * stickG));
            }
            else
            {
                CurrentLateralG = 0f;
            }

            Condition?.ReportG(CurrentLateralG, dt);
        }

        /// <summary>コース上の指定位置へ復帰させる (スタック/失神事故からのリカバリ)。</summary>
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(position + Vector3.up * 0.6f, rotation);
        }
    }
}
