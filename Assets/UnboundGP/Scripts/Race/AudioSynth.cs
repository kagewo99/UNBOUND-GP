using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// 効果音をコードで合成する(音源ファイル不要)。
    /// エンジン音(倍音つきの基音ループ)とフィルタードノイズ(タイヤ/風)を生成し、
    /// AudioSource.pitch/volume を実行時に変調して使う。生成クリップは静的にキャッシュして全車で共有。
    /// </summary>
    public static class AudioSynth
    {
        static AudioClip engineClip;
        static AudioClip noiseClip;

        public static AudioClip Engine() => engineClip != null ? engineClip : (engineClip = MakeEngine());
        public static AudioClip Noise() => noiseClip != null ? noiseClip : (noiseClip = MakeNoise());

        /// <summary>
        /// エンジン基音(約55Hz)+倍音+わずかなザラつき。整数サイクル長でシームレスループ。
        /// 実行時に AudioSource.pitch を上げ下げして RPM を表現する。
        /// </summary>
        static AudioClip MakeEngine()
        {
            const int sr = 44100;
            const float f0 = 55f;
            int period = Mathf.RoundToInt(sr / f0);
            int len = period * 55;                 // 約1秒・整数サイクル → 継ぎ目なし
            var data = new float[len];
            var rnd = new System.Random(1234);
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float s = 0f;
                s += Mathf.Sin(2f * Mathf.PI * f0 * t) * 0.60f;       // 基音
                s += Mathf.Sin(2f * Mathf.PI * f0 * 2f * t) * 0.30f;  // 2倍音
                s += Mathf.Sin(2f * Mathf.PI * f0 * 3f * t) * 0.20f;  // 3倍音
                s += Mathf.Sin(2f * Mathf.PI * f0 * 5f * t) * 0.10f;  // 5倍音(荒さ)
                s += ((float)rnd.NextDouble() * 2f - 1f) * 0.06f;     // ザラつき
                data[i] = s * 0.4f;
            }
            var c = AudioClip.Create("EngineSynth", len, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>ローパス気味のノイズ。タイヤスキール(高ピッチ)と風切り(低ピッチ)に使い回す。</summary>
        static AudioClip MakeNoise()
        {
            const int sr = 44100;
            int len = sr; // 1秒(ノイズはループ継ぎ目が目立たない)
            var data = new float[len];
            var rnd = new System.Random(99);
            float prev = 0f;
            for (int i = 0; i < len; i++)
            {
                float w = (float)rnd.NextDouble() * 2f - 1f;
                prev = Mathf.Lerp(prev, w, 0.45f); // 簡易ローパス
                data[i] = prev * 0.6f;
            }
            var c = AudioClip.Create("NoiseSynth", len, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
