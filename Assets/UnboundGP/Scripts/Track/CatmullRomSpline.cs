using System.Collections.Generic;
using UnityEngine;

namespace UnboundGP.Track
{
    /// <summary>閉ループの Catmull-Rom スプライン評価ユーティリティ。</summary>
    public static class CatmullRomSpline
    {
        /// <summary>t ∈ [0,1) でループ全体上の点を返す。</summary>
        public static Vector3 GetPoint(IReadOnlyList<Vector3> cps, float t)
        {
            int n = cps.Count;
            float ft = Mathf.Repeat(t, 1f) * n;
            int i = Mathf.FloorToInt(ft) % n;
            float u = ft - Mathf.Floor(ft);

            Vector3 p0 = cps[(i - 1 + n) % n];
            Vector3 p1 = cps[i];
            Vector3 p2 = cps[(i + 1) % n];
            Vector3 p3 = cps[(i + 2) % n];

            // 標準 Catmull-Rom (tension 0.5)
            return 0.5f * (
                2f * p1
                + (-p0 + p2) * u
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (u * u)
                + (-p0 + 3f * p1 - 3f * p2 + p3) * (u * u * u));
        }
    }

    /// <summary>
    /// スプラインを等間隔(近似)にサンプリングし、距離・曲率を前計算する純粋関数群。
    /// TrackPath (シーン内) と LapTimeEstimator (ガレージのオフライン計算) が共用する。
    /// </summary>
    public static class TrackSampler
    {
        public static void Sample(IReadOnlyList<Vector3> cps, int count,
            out Vector3[] points, out float[] curvature, out float[] cumDist, out float totalLength)
        {
            points = new Vector3[count];
            for (int i = 0; i < count; i++)
                points[i] = CatmullRomSpline.GetPoint(cps, (float)i / count);

            cumDist = new float[count];
            float total = 0f;
            for (int i = 1; i < count; i++)
            {
                total += Vector3.Distance(points[i - 1], points[i]);
                cumDist[i] = total;
            }
            totalLength = total + Vector3.Distance(points[count - 1], points[0]);

            // 曲率 [rad/m]: 水平面上の進行方向変化÷弧長。AIの目標速度とラップ推定の根拠になる。
            curvature = new float[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 a = points[(i - 1 + count) % count];
                Vector3 b = points[i];
                Vector3 c = points[(i + 1) % count];
                var d1 = new Vector2(b.x - a.x, b.z - a.z);
                var d2 = new Vector2(c.x - b.x, c.z - b.z);
                float ds = (d1.magnitude + d2.magnitude) * 0.5f;
                curvature[i] = ds > 0.01f
                    ? Vector2.Angle(d1, d2) * Mathf.Deg2Rad / ds
                    : 0f;
            }
        }
    }
}
