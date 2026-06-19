using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// 「重量感」を出すビジュアル層。進行方向の物理(VehicleModel)は一切いじらず、
    /// 見た目だけを車の状態から動かす:
    ///  - バネ上ボディ:制動でノーズが沈み、加速で仰け反り、コーナーでロールし、揺り戻す
    ///  - 接地追従:路面法線に車体を合わせ、坂(橋)で前上がりになる
    ///  - ホイール:速度で回転、前輪は操舵、(簡易)上下動
    ///  - タイヤスモーク:リアグリップ飽和/スリップで発生
    /// これらは LateUpdate で物理の後に適用する。物理から切り離してあるので挙動は壊れない。
    /// </summary>
    public class CarVisuals : MonoBehaviour
    {
        CarController car;
        Transform body;                  // バネ上(車体の見た目)
        Transform[] wheels;              // 4輪(0,1=前 / 2,3=後)
        bool[] isFront;
        float[] spin;                    // 各輪の回転角[deg]
        ParticleSystem smokeL, smokeR;

        Vector3 bodyBaseLocalPos;
        // バネのスムーズダンプ状態
        float pitch, pitchVel, roll, rollVel, heave, heaveVel;

        // 体感の誇張量(数値ではなく“動きの有無”が重要。すべて feel 調整可)
        const float PitchPerG = 2.2f;    // 縦Gあたりのピッチ[deg]
        const float RollPerG = 3.0f;     // 横Gあたりのロール[deg]
        const float SpringTime = 0.10f;  // バネの収束時間(小さいほどキビキビ)
        const float WheelRadius = 0.35f;

        public void Setup(CarController car, Transform body, Transform[] wheels, bool[] isFront)
        {
            this.car = car;
            this.body = body;
            this.wheels = wheels;
            this.isFront = isFront;
            spin = new float[wheels.Length];
            bodyBaseLocalPos = body.localPosition;
            CreateSmoke();
        }

        void LateUpdate()
        {
            if (car == null || body == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // ---- バネ上ボディ:G から目標姿勢、スムーズダンプで追従(揺れ・揺り戻し)----
            float longG = car.LongitudinalGSigned;   // 加速+ / 制動-
            float latG = car.LateralGSigned;         // 右+ / 左-

            // 制動(longG<0)でノーズダウン=euler X 正。加速で仰け反り=euler X 負。
            float targetPitch = Mathf.Clamp(-longG * PitchPerG, -7f, 7f);
            // 右旋回(latG>0)で外側(左)が沈む=右側が上がる=euler Z 正。
            float targetRoll = Mathf.Clamp(latG * RollPerG, -8f, 8f);
            // 縦の揺れ(加減速の荷重で僅かに上下)
            float targetHeave = Mathf.Clamp(-Mathf.Abs(longG) * 0.01f - Mathf.Abs(latG) * 0.008f, -0.06f, 0f);

            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVel, SpringTime);
            roll = Mathf.SmoothDamp(roll, targetRoll, ref rollVel, SpringTime);
            heave = Mathf.SmoothDamp(heave, targetHeave, ref heaveVel, SpringTime * 1.4f);

            // ---- 接地追従:路面法線へ車体を傾ける(坂で前上がり)----
            float slopePitch = 0f, slopeRoll = 0f;
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out var hit, 4f))
            {
                Vector3 localN = transform.InverseTransformDirection(hit.normal);
                // 法線が後ろへ倒れる(登り坂)→ ノーズアップ。符号は feel 調整可。
                slopePitch = Mathf.Atan2(localN.z, Mathf.Max(localN.y, 0.2f)) * Mathf.Rad2Deg;
                slopeRoll = -Mathf.Atan2(localN.x, Mathf.Max(localN.y, 0.2f)) * Mathf.Rad2Deg;
                slopePitch = Mathf.Clamp(slopePitch, -25f, 25f);
                slopeRoll = Mathf.Clamp(slopeRoll, -25f, 25f);
            }

            body.localRotation = Quaternion.Euler(pitch + slopePitch, 0f, roll + slopeRoll);
            body.localPosition = bodyBaseLocalPos + Vector3.up * heave;

            // ---- ホイール:回転・操舵 ----
            float steerDeg = car.SteerInput * 22f;       // 前輪の見た目の切れ角
            float spinDeg = car.CurrentSpeedMs / (2f * Mathf.PI * WheelRadius) * 360f * dt
                          * (Vector3.Dot(car.transform.forward, GetPlanarVel()) >= 0f ? 1f : -1f);
            for (int i = 0; i < wheels.Length; i++)
            {
                spin[i] = (spin[i] + spinDeg) % 360f;
                float steer = isFront[i] ? steerDeg : 0f;
                // 基準: Euler(0,0,90)で円筒の軸をX(車軸)へ。次に車軸まわりに回転、最後に操舵(Y)。
                wheels[i].localRotation = Quaternion.Euler(0f, steer, 0f)
                                        * Quaternion.AngleAxis(spin[i], Vector3.right)
                                        * Quaternion.Euler(0f, 0f, 90f);
            }

            // ---- タイヤスモーク ----
            float smoke = Mathf.Clamp01((car.RearGripUsage - 0.85f) / 0.15f);
            smoke = Mathf.Max(smoke, Mathf.Clamp01((Mathf.Abs(car.SlipAngleDeg) - 10f) / 20f));
            SetSmoke(smokeL, smoke);
            SetSmoke(smokeR, smoke);
        }

        Vector3 GetPlanarVel()
        {
            var rb = car.GetComponent<Rigidbody>();
            if (rb == null) return car.transform.forward;
            var v = rb.velocity; v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : car.transform.forward;
        }

        // ----------------------------------------------------------------
        // タイヤスモーク(後輪左右)
        // ----------------------------------------------------------------
        void CreateSmoke()
        {
            smokeL = MakeSmoke(new Vector3(-0.85f, 0.2f, -1.6f));
            smokeR = MakeSmoke(new Vector3(0.85f, 0.2f, -1.6f));
        }

        ParticleSystem MakeSmoke(Vector3 localPos)
        {
            var go = new GameObject("TireSmoke");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = new Color(0.85f, 0.85f, 0.85f, 0.5f);
            main.startSize = 1.2f;
            main.startLifetime = 0.8f;
            main.startSpeed = 0.6f;
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;   // 実行時に制御
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.7f, 0.7f, 0.7f), 1f) },
                new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.material = TrackBuilder.MakeMaterial(new Color(0.9f, 0.9f, 0.9f));
            return ps;
        }

        static void SetSmoke(ParticleSystem ps, float amount)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.rateOverTime = amount > 0.05f ? amount * 60f : 0f;
        }
    }
}
