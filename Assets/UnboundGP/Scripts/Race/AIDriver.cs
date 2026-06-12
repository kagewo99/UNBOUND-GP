using UnboundGP.Core;
using UnboundGP.Track;
using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// 走行ライン追従AI。重要なのは「自分のG限界を知っていて、その内側でしか攻めない」こと。
    ///
    /// - Human GP のライバルAI: 人間プロファイル(約5G)が上限 → マシン性能を使い切れない
    /// - Machine GP のAI: 限界が事実上無限 → マシンの理論限界 (ファンカーなら曲がり放題) で走る
    ///
    /// 同一クラス・同一コードで両者を表現することで、
    /// 「速さを決めていたのはルールではなく、コクピットの中身だった」を示す。
    /// </summary>
    public class AIDriver : MonoBehaviour, IDriverInput
    {
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Steer { get; private set; }

        CarController car;
        TrackPath path;
        DriverCondition condition;
        int nearestHint = -1;

        const float Gravity = 9.81f;

        public void Setup(CarController car, TrackPath path, DriverCondition condition)
        {
            this.car = car;
            this.path = path;
            this.condition = condition;
        }

        void FixedUpdate()
        {
            if (car == null || path == null) return;

            nearestHint = path.NearestIndex(transform.position, nearestHint);
            float s = path.DistanceAt(nearestHint);
            float speed = Mathf.Max(car.CurrentSpeedMs, 1f);

            // ---- ステアリング: 速度に応じた先読み点を追従 (pure pursuit) ----
            float lookAhead = Mathf.Clamp(speed * 0.55f, 12f, 70f);
            Vector3 target = path.PointAtDistance(s + lookAhead);
            Vector3 local = transform.InverseTransformPoint(target);
            local.y = 0f;
            Steer = Mathf.Clamp(local.x / Mathf.Max(local.magnitude, 0.1f) * 3f, -1f, 1f);

            // ---- 速度制御: 制動可能距離内で最も厳しいコーナーに合わせる ----
            float tolG = condition != null
                ? condition.Profile.maxSustainedG * condition.Profile.skill
                : 999f;

            float brakeDecel = car.Stats.BrakeDecel(speed);
            if (condition != null && condition.Profile.isHuman)
            {
                // 人間は縦Gにはやや強いが、それでも限界はある
                brakeDecel = Mathf.Min(brakeDecel, tolG * 1.5f * Gravity);
            }

            float allowedSpeed = car.Stats.EffectiveTopSpeedMs;
            for (float d = 12f; d <= 300f; d += 15f)
            {
                float curv = path.MaxCurvatureBetween(s + d - 9f, s + d + 9f);
                float vCorner = SolveCornerSpeed(curv, tolG);
                // その地点までに減速しきれる現在速度の上限
                float vNow = Mathf.Sqrt(vCorner * vCorner + 2f * brakeDecel * d);
                if (vNow < allowedSpeed) allowedSpeed = vNow;
            }
            // 現在地点の曲率も直接制限する
            float hereCurv = path.Curvature[nearestHint];
            allowedSpeed = Mathf.Min(allowedSpeed, SolveCornerSpeed(hereCurv, tolG));

            if (speed < allowedSpeed - 1.5f)
            {
                Throttle = 1f;
                Brake = 0f;
            }
            else if (speed > allowedSpeed + 1.5f)
            {
                Throttle = 0f;
                Brake = Mathf.Clamp01((speed - allowedSpeed) / 8f);
            }
            else
            {
                Throttle = 0.55f;
                Brake = 0f;
            }
        }

        /// <summary>
        /// 曲率 curv のコーナーを「マシン限界」と「ドライバーG限界」の両方を満たして
        /// 通過できる最大速度を解く。
        /// マシン限界: v^2·c = g·(mech + df·(v/v300)^2) — ダウンフォースが要求を上回れば全開可
        /// ドライバー限界: v^2·c = g·tol
        /// </summary>
        float SolveCornerSpeed(float curv, float tolG)
        {
            float top = car.Stats.EffectiveTopSpeedMs;
            if (curv < 1e-4f) return top;

            var st = car.Stats;
            float refV2 = MachineStats.ReferenceSpeedMs * MachineStats.ReferenceSpeedMs;
            float denom = curv - Gravity * st.downforceFactor / refV2;
            float vMachine = denom <= 1e-6f
                ? top // ダウンフォースが勝つ: このコーナーは物理的には全開で曲がれる
                : Mathf.Sqrt(Gravity * st.mechanicalGrip / denom);

            float vDriver = Mathf.Sqrt(Gravity * tolG / curv);

            return Mathf.Min(Mathf.Min(vMachine, vDriver), top);
        }
    }
}
