using UnboundGP.Core;
using UnboundGP.Data;
using UnboundGP.Track;
using UnityEngine;

namespace UnboundGP.Design
{
    /// <summary>
    /// 速度プロファイル法 (2パス) によるラップタイム概算。
    ///
    /// マシン設計画面で「このマシンを人間が走らせた場合」と「AIが走らせた場合」の
    /// 理論ラップを並べて表示するために使う。設計の時点で
    /// “人間がボトルネックになる瞬間”を数字で予感させるのが狙い。
    /// </summary>
    public static class LapTimeEstimator
    {
        const float Gravity = 9.81f;

        /// <param name="driverGLimit">ドライバーの持続G限界。人間なら約5、AIなら大きな値。</param>
        /// <returns>推定ラップタイム [秒]</returns>
        public static float Estimate(TrackData track, MachineStats stats, float driverGLimit)
        {
            const int n = 600;
            TrackSampler.Sample(track.controlPoints, n,
                out var pts, out var curv, out _, out _);

            // 区間長
            var ds = new float[n];
            for (int i = 0; i < n; i++)
                ds[i] = Vector3.Distance(pts[i], pts[(i + 1) % n]);

            float top = stats.EffectiveTopSpeedMs;
            float refV2 = MachineStats.ReferenceSpeedMs * MachineStats.ReferenceSpeedMs;

            // ---- パス0: 各点の旋回限界速度 ----
            var v = new float[n];
            for (int i = 0; i < n; i++)
            {
                float c = curv[i];
                if (c < 1e-4f)
                {
                    v[i] = top;
                    continue;
                }
                float denom = c - Gravity * stats.downforceFactor / refV2;
                float vMachine = denom <= 1e-6f ? top : Mathf.Sqrt(Gravity * stats.mechanicalGrip / denom);
                float vDriver = Mathf.Sqrt(Gravity * driverGLimit / c);
                v[i] = Mathf.Min(Mathf.Min(vMachine, vDriver), top);
            }

            // ---- パス1: 加速制限 (周回コースなので2周して収束させる) ----
            for (int loop = 0; loop < 2; loop++)
            {
                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;
                    float accel = stats.acceleration * (1f - Mathf.Pow(Mathf.Clamp01(v[i] / top), 3f));
                    float reachable = Mathf.Sqrt(v[i] * v[i] + 2f * Mathf.Max(accel, 0.1f) * ds[i]);
                    if (reachable < v[next]) v[next] = reachable;
                }
            }

            // ---- パス2: 制動制限 (逆走査) ----
            float brakeCap = driverGLimit * 1.5f * Gravity; // 人間は縦Gにも限界がある
            for (int loop = 0; loop < 2; loop++)
            {
                for (int i = n - 1; i >= 0; i--)
                {
                    int next = (i + 1) % n;
                    float decel = Mathf.Min(stats.BrakeDecel(v[next]), brakeCap);
                    float entry = Mathf.Sqrt(v[next] * v[next] + 2f * decel * ds[i]);
                    if (entry < v[i]) v[i] = entry;
                }
            }

            // ---- 積分 ----
            float time = 0f;
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                float avg = Mathf.Max((v[i] + v[next]) * 0.5f, 5f);
                time += ds[i] / avg;
            }
            return time;
        }

        /// <summary>"1:23.456" 形式の表示用文字列。</summary>
        public static string Format(float seconds)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return "--:--.---";
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60f;
            return $"{m}:{s:00.000}";
        }
    }
}
