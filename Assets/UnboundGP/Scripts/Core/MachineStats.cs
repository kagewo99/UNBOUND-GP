using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnboundGP.Core
{
    /// <summary>マシン性能を構成するステータス種別。研究ツリーの技術はこれらを変化させる。</summary>
    public enum StatType
    {
        /// <summary>最高速度 (km/h)。空気抵抗を受ける前の理論値。</summary>
        TopSpeedKmh,
        /// <summary>加速力 (m/s^2)。駆動力の指標。</summary>
        Acceleration,
        /// <summary>機械的グリップ (G)。速度に依存しない旋回限界。ファンカーの“吸着”もここに加算される。</summary>
        MechanicalGrip,
        /// <summary>ダウンフォース係数。300km/h 走行時に加算される旋回限界 (G)。速度の2乗に比例して効く。</summary>
        DownforceFactor,
        /// <summary>空気抵抗 (0〜1)。実効最高速を削る。ダウンフォース系技術の代償。</summary>
        Drag,
        /// <summary>車重 (kg)。</summary>
        WeightKg,
        /// <summary>信頼性 (0〜1)。軽量化などの代償で低下する。(Vertical Slice では表示のみ)</summary>
        Reliability,
    }

    /// <summary>
    /// 技術がステータスへ与える変化。「(現在値 + add) × multiply」の順で適用される。
    /// </summary>
    [Serializable]
    public class StatModifier
    {
        public StatType stat;
        public float add;
        public float multiply = 1f;
    }

    /// <summary>
    /// 組み上がったマシンの最終性能。CarController / AI / ラップタイム推定が共通で参照する
    /// “物理モデルの単一の真実”をここに集約する。
    /// </summary>
    [Serializable]
    public struct MachineStats
    {
        public float topSpeedKmh;
        public float acceleration;
        public float mechanicalGrip;
        public float downforceFactor;
        public float drag;
        public float weightKg;
        public float reliability;

        /// <summary>人間が持続的に耐えられる横Gの目安。この値こそが本作の“最後のレギュレーション”。</summary>
        public const float HumanSustainedGLimit = 5.0f;

        /// <summary>ダウンフォース係数の基準速度 (300km/h) [m/s]。</summary>
        public const float ReferenceSpeedMs = 300f / 3.6f;

        public float Get(StatType t)
        {
            switch (t)
            {
                case StatType.TopSpeedKmh: return topSpeedKmh;
                case StatType.Acceleration: return acceleration;
                case StatType.MechanicalGrip: return mechanicalGrip;
                case StatType.DownforceFactor: return downforceFactor;
                case StatType.Drag: return drag;
                case StatType.WeightKg: return weightKg;
                case StatType.Reliability: return reliability;
                default: return 0f;
            }
        }

        public void Set(StatType t, float v)
        {
            switch (t)
            {
                case StatType.TopSpeedKmh: topSpeedKmh = v; break;
                case StatType.Acceleration: acceleration = v; break;
                case StatType.MechanicalGrip: mechanicalGrip = v; break;
                case StatType.DownforceFactor: downforceFactor = v; break;
                case StatType.Drag: drag = v; break;
                case StatType.WeightKg: weightKg = v; break;
                case StatType.Reliability: reliability = v; break;
            }
        }

        /// <summary>修正値リストを適用した新しいステータスを返す (元の値は変更しない)。</summary>
        public MachineStats Apply(IEnumerable<StatModifier> modifiers)
        {
            var s = this;
            foreach (var m in modifiers)
            {
                if (m == null) continue;
                s.Set(m.stat, (s.Get(m.stat) + m.add) * m.multiply);
            }
            // 破綻防止のクランプ
            s.drag = Mathf.Clamp01(s.drag);
            s.reliability = Mathf.Clamp01(s.reliability);
            s.weightKg = Mathf.Max(300f, s.weightKg);
            s.mechanicalGrip = Mathf.Max(0.5f, s.mechanicalGrip);
            return s;
        }

        /// <summary>空気抵抗を加味した実効最高速 [m/s]。</summary>
        public float EffectiveTopSpeedMs => topSpeedKmh / 3.6f * (1f - 0.35f * Mathf.Clamp01(drag));

        /// <summary>実効最高速 [km/h] (UI 表示用)。</summary>
        public float EffectiveTopSpeedKmh => EffectiveTopSpeedMs * 3.6f;

        /// <summary>
        /// 速度 v [m/s] における旋回限界G。
        /// 機械的グリップ + ダウンフォース×(v/300km/h)^2 で、速いほど曲がれる=F1的特性。
        /// </summary>
        public float MaxCorneringG(float speedMs)
        {
            float r = speedMs / ReferenceSpeedMs;
            return mechanicalGrip + downforceFactor * r * r;
        }

        /// <summary>速度 v [m/s] における制動減速度 [m/s^2]。ダウンフォースが効くほど強く止まれる。</summary>
        public float BrakeDecel(float speedMs)
        {
            return Mathf.Max(9f, MaxCorneringG(speedMs) * 9.81f * 0.9f);
        }
    }
}
