using UnityEngine;

namespace UnboundGP.Track
{
    /// <summary>
    /// Reality Track 第1弾「SUZUKA UNBOUND」の制御点定義。
    /// 鈴鹿サーキットの特徴である“立体交差を持つ8の字レイアウト”を再現した
    /// オリジナルコース。バックストレート橋がデグナー側アンダーパスの上を跨ぐ。
    /// </summary>
    public static class SuzukaTrackDefinition
    {
        /// <summary>コース全体のスケール係数。1.6 で全長約3.6km。</summary>
        public const float Scale = 1.6f;

        static Vector3 P(float x, float y, float z) => new Vector3(x * Scale, y, z * Scale);

        /// <summary>
        /// 中心線の制御点 (閉ループ)。コメントは各セクションの呼称。
        /// </summary>
        public static Vector3[] CreateControlPoints()
        {
            return new[]
            {
                P( 140f, 0f, -150f), // 0  S/Fストレート (西向きに走行)
                P(  40f, 0f, -150f), // 1  コントロールライン
                P( -60f, 0f, -150f), // 2  ストレートエンド
                P(-140f, 0f, -145f), // 3  T1進入
                P(-200f, 3f, -100f), // 4  T1/T2 (右・複合)
                P(-190f, 0f,  -55f), // 5  デグナー進入
                P(-120f, 0f,  -60f), // 6  デグナー (切り返し)
                P( -60f, 0f,  -30f), // 7  アンダーパス進入
                P(   0f, 0f,    0f), // 8  ★立体交差 (下) — この真上を 20 が跨ぐ
                P(  60f, 0f,   30f), // 9  アンダーパス出口
                P( 120f, 0f,   60f), // 10 北ループへの登り
                P( 160f, 0f,  110f), // 11
                P( 130f, 0f,  170f), // 12 スプーン風・北端進入
                P(  60f, 0f,  200f), // 13
                P( -20f, 0f,  205f), // 14 北端
                P(-100f, 0f,  175f), // 15 スプーン出口
                P(-150f, 5f,  120f), // 16 ヘアピン進入
                P(-125f, 4f,   75f), // 17 ヘアピン (右)
                P(-120f, 4f,   60f), // 18 橋への登り
                P( -60f, 9f,   30f), // 19 バックストレート橋
                P(   0f,10f,    0f), // 20 ★立体交差 (上)
                P(  60f, 9f,  -30f), // 21 橋下り
                P( 120f, 4f,  -60f), // 22 130R風・高速左
                P( 160f, 0f, -105f), // 23 最終シケイン
                P( 158f, 0f, -138f), // 24 最終コーナー出口 → 0 へ戻る
            };
        }
    }
}
