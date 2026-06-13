using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// 一人称(ヘルメット/コックピット)視点カメラ。Human GP 専用。
    ///
    /// 本作のテーマ「マシンに規制はないが、人体にはGの限界がある」を“自分の目”で
    /// 受け止めさせるための視点。横Gで頭が振られ、加減速で前後に揺れ、速度で視界が流れる。
    /// そしてブラックアウトが近づくと頭がうなだれて視線が落ちる ──
    /// HUD の視野狭窄(ビネット)と重なり、「自分の意識が閉じていく」恐怖を最大化する。
    ///
    /// Machine GP では使わない(人間が乗っていないので、外から眺める ChaseCamera が正しい)。
    /// </summary>
    public class CockpitCamera : MonoBehaviour, IRaceCamera
    {
        CarController target;
        Camera cam;

        // ヘルメット位置(車体ローカル)。コックピット箱(上端≈y1.0/前端≈z0)の
        // すぐ上・やや前に目を置き、ノーズ越しに前方を見渡す。
        static readonly Vector3 HeadLocal = new Vector3(0f, 1.15f, 0.1f);
        // 注視点(車体ローカル)。前方やや下を見る。
        static readonly Vector3 LookLocal = new Vector3(0f, 0.7f, 14f);

        // Gによる頭の振れ量(視覚的に誇張)
        const float LateralLean = 0.10f;   // 横Gあたりの頭の横移動 [m/G]
        const float LateralRoll = 1.3f;    // 横Gあたりのカメラロール [deg/G]
        const float PitchPerG = 0.7f;      // 縦Gあたりのピッチ [deg/G]

        Vector3 smoothHeadOffset;
        float smoothRoll, smoothPitch;

        public void SetTarget(CarController car)
        {
            target = car;
            cam = GetComponent<Camera>();
            smoothHeadOffset = Vector3.zero;
            smoothRoll = smoothPitch = 0f;
        }

        void LateUpdate()
        {
            if (target == null) return;
            var t = target.transform;
            float dt = Time.deltaTime;

            // ---- Gによる頭の振れ(ローパスで滑らかに) ----
            // 右方向Gが正 → 頭は外側(左)へ持っていかれる
            float latG = target.LateralGSigned;
            float lonG = target.LongitudinalGSigned;

            Vector3 targetOffset = new Vector3(-latG * LateralLean, 0f, 0f);
            float targetRoll = latG * LateralRoll;                 // Gと逆に首が傾く
            float targetPitch = -lonG * PitchPerG;                 // 制動で前のめり/加速で仰け反り

            // ---- ブラックアウト:頭がうなだれて視線が落ちる ----
            if (target.Condition != null)
            {
                float bm = target.Condition.BlackoutMeter;
                if (target.Condition.IsBlackedOut) bm = 1f;
                targetPitch += bm * 22f;          // 下を向く
                targetRoll += bm * bm * 12f;       // 力なく傾く
                targetOffset.y -= bm * 0.15f;       // 頭が落ちる
            }

            // 速度に応じた微振動(路面の手応え)
            float speedT = Mathf.Clamp01(target.CurrentSpeedKmh / 350f);
            float shake = speedT * 0.012f;
            targetOffset.x += (Mathf.PerlinNoise(Time.time * 22f, 0f) - 0.5f) * shake;
            targetOffset.y += (Mathf.PerlinNoise(0f, Time.time * 26f) - 0.5f) * shake;

            smoothHeadOffset = Vector3.Lerp(smoothHeadOffset, targetOffset, 10f * dt);
            smoothRoll = Mathf.Lerp(smoothRoll, targetRoll, 8f * dt);
            smoothPitch = Mathf.Lerp(smoothPitch, targetPitch, 8f * dt);

            // ---- 位置と向き ----
            Vector3 headPos = t.TransformPoint(HeadLocal + smoothHeadOffset);
            Vector3 lookPos = t.TransformPoint(LookLocal);
            transform.position = headPos;

            Quaternion baseRot = Quaternion.LookRotation(lookPos - headPos, t.up);
            transform.rotation = baseRot * Quaternion.Euler(smoothPitch, 0f, smoothRoll);

            // ---- 速度連動 FOV(一人称は速度感が強いので広めに振る) ----
            if (cam != null)
            {
                float fov = Mathf.Lerp(72f, 96f, speedT);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 4f * dt);
            }
        }
    }
}
