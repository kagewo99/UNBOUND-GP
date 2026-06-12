using UnboundGP.Data;
using UnityEngine;

namespace UnboundGP.Track
{
    /// <summary>
    /// 走行可能なコースの“論理表現”。サンプル点列・累積距離・曲率を保持し、
    /// AI の走行ライン追従、ラップ進行度の計測、グリッド配置に使われる。
    /// メッシュ(見た目)とは独立しているため、ビジュアル差し替えが容易。
    /// </summary>
    public class TrackPath : MonoBehaviour
    {
        public Vector3[] Points { get; private set; }
        public Vector3[] Forwards { get; private set; }
        public float[] Curvature { get; private set; }
        public float[] CumDist { get; private set; }
        public float TotalLength { get; private set; }
        public TrackData Data { get; private set; }

        public int SampleCount => Points.Length;

        public void Init(TrackData data, int sampleCount = 1000)
        {
            Data = data;
            TrackSampler.Sample(data.controlPoints, sampleCount,
                out var pts, out var curv, out var dist, out float total);
            Points = pts;
            Curvature = curv;
            CumDist = dist;
            TotalLength = total;

            Forwards = new Vector3[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                Vector3 d = Points[(i + 1) % sampleCount] - Points[i];
                Forwards[i] = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
            }
        }

        /// <summary>
        /// 最寄りサンプル点のインデックスを返す。
        /// hint (前回の結果) があれば近傍のみ探索するため、立体交差でも誤判定しない。
        /// </summary>
        public int NearestIndex(Vector3 pos, int hint = -1)
        {
            int n = SampleCount;
            int best = 0;
            float bestSq = float.MaxValue;

            if (hint < 0)
            {
                for (int i = 0; i < n; i++) Consider(i, pos, ref best, ref bestSq);
            }
            else
            {
                const int window = 60;
                for (int o = -window; o <= window; o++)
                    Consider(((hint + o) % n + n) % n, pos, ref best, ref bestSq);
            }
            return best;
        }

        void Consider(int i, Vector3 pos, ref int best, ref float bestSq)
        {
            float sq = (Points[i] - pos).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = i;
            }
        }

        /// <summary>サンプル点のコース始点からの距離 [m]。</summary>
        public float DistanceAt(int index) => CumDist[index];

        /// <summary>
        /// 距離 d [m] 地点の座標。サンプルがほぼ等間隔である近似を利用する。
        /// </summary>
        public Vector3 PointAtDistance(float d)
        {
            return Points[IndexAtDistance(d)];
        }

        public int IndexAtDistance(float d)
        {
            d = Mathf.Repeat(d, TotalLength);
            int i = Mathf.Clamp(Mathf.RoundToInt(d / TotalLength * SampleCount), 0, SampleCount - 1);
            // 近似誤差を局所探索で補正
            while (i < SampleCount - 1 && CumDist[i + 1] < d) i++;
            while (i > 0 && CumDist[i] > d) i--;
            return i;
        }

        /// <summary>距離 dFrom〜dTo 区間の最大曲率 [rad/m]。AI の先読み制動に使う。</summary>
        public float MaxCurvatureBetween(float dFrom, float dTo)
        {
            int i0 = IndexAtDistance(dFrom);
            int i1 = IndexAtDistance(dTo);
            int n = SampleCount;
            int steps = ((i1 - i0) % n + n) % n;
            float max = 0f;
            for (int s = 0; s <= steps; s++)
            {
                float c = Curvature[(i0 + s) % n];
                if (c > max) max = c;
            }
            return max;
        }

        /// <summary>距離 d 地点での進行方向を向く回転。スポーン・リセットに使う。</summary>
        public Quaternion RotationAtDistance(float d)
        {
            var f = Forwards[IndexAtDistance(d)];
            var flat = new Vector3(f.x, 0f, f.z);
            return Quaternion.LookRotation(flat.sqrMagnitude > 1e-6f ? flat : Vector3.forward);
        }
    }
}
