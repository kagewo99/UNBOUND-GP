using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// 速度連動サウンド。既存の状態(変速機RPM / スリップ角 / 速度)へ配線するだけで、
    /// 無音=死んだ印象を解消する。3D音源として車体に付くので、観戦/一人称どちらでも
    /// 注視中の車が大きく聞こえる。
    /// </summary>
    public class CarAudio : MonoBehaviour
    {
        CarController car;
        AudioSource engine, tire, wind;

        public void Setup(CarController c)
        {
            car = c;
            engine = MakeSource("Engine", AudioSynth.Engine(), 0.0f);
            tire = MakeSource("Tire", AudioSynth.Noise(), 0.0f);
            wind = MakeSource("Wind", AudioSynth.Noise(), 0.0f);
        }

        AudioSource MakeSource(string name, AudioClip clip, float vol)
        {
            var go = new GameObject("Audio_" + name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.volume = vol;
            src.spatialBlend = 1f;                 // 3D
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 4f;
            src.maxDistance = 140f;
            src.dopplerLevel = 0.3f;
            src.Play();
            return src;
        }

        void Update()
        {
            if (car == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // ---- エンジン:RPM でピッチ、負荷(RPM)で音量 ----
            var gb = car.Gearbox;
            float rpmN = Mathf.Clamp01(gb.Rpm / Transmission.RedlineRPM);
            engine.pitch = Mathf.Lerp(0.65f, 2.7f, rpmN);
            engine.volume = Mathf.Lerp(engine.volume, Mathf.Lerp(0.16f, 0.55f, rpmN), 12f * dt);

            // ---- タイヤスキール:スリップ角が大きく、ある程度速度が出ているとき ----
            float slip = Mathf.Abs(car.SlipAngleDeg);
            float screech = Mathf.Clamp01((slip - 6f) / 16f)
                          * Mathf.Clamp01((car.CurrentSpeedKmh - 15f) / 30f);
            screech = Mathf.Max(screech, car.RearGripUsage > 0.95f ? 0.4f : 0f); // ホイールスピン/ロック
            tire.pitch = 1.7f;
            tire.volume = Mathf.Lerp(tire.volume, screech * 0.5f, 10f * dt);

            // ---- 風切り:速度で音量・ピッチ ----
            float sp = Mathf.Clamp01(car.CurrentSpeedKmh / 400f);
            wind.pitch = Mathf.Lerp(0.8f, 1.7f, sp);
            wind.volume = Mathf.Lerp(wind.volume, sp * 0.28f, 5f * dt);
        }
    }
}
