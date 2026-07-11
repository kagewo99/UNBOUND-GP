using System.Collections.Generic;
using UnboundGP.Core;
using UnboundGP.Track;
using UnityEngine;

namespace UnboundGP.Data
{
    /// <summary>
    /// 既定のゲームデータを生成するファクトリ。
    /// ・エディタメニュー「UNBOUND GP/Setup Project」が .asset として書き出す元データ
    /// ・アセット未作成でも任意のシーンから再生できるようにする実行時フォールバック
    /// の両方から使われる。バランス調整の初期値はすべてここに集約されている。
    /// </summary>
    public static class DefaultDataFactory
    {
        // ----------------------------------------------------------------
        // シャシー
        // ----------------------------------------------------------------
        public static MachineChassisData CreateChassis()
        {
            var c = ScriptableObject.CreateInstance<MachineChassisData>();
            c.name = "Chassis";
            c.chassisName = "UNBOUND Type-0";
            c.description = "レギュレーション最終年のF1マシン相当のベースシャシー。ここからすべての“禁忌”が始まる。";
            c.baseStats = new MachineStats
            {
                topSpeedKmh = 330f,
                acceleration = 13f,
                mechanicalGrip = 2.5f,  // 低中速でも接地する素のグリップ(キーボードでの御しやすさ重視)
                downforceFactor = 2.0f, // 300km/h時 約4.5G(素の機械グリップ込み)
                drag = 0f,
                weightKg = 798f,
                reliability = 0.95f,
            };
            return c;
        }

        // ----------------------------------------------------------------
        // 研究ツリー
        // ----------------------------------------------------------------
        public static TechTreeData CreateTechTree()
        {
            // ローカル関数: ノード生成を簡潔に書くためのヘルパー
            TechNodeData N(string id, string name, int tier, int cost,
                string desc, string note, TechNodeData[] pre, params StatModifier[] mods)
            {
                var n = ScriptableObject.CreateInstance<TechNodeData>();
                n.name = id;
                n.techId = id;
                n.displayName = name;
                n.tier = tier;
                n.researchCost = cost;
                n.description = desc;
                n.realWorldNote = note;
                if (pre != null) n.prerequisites.AddRange(pre);
                n.modifiers.AddRange(mods);
                return n;
            }

            StatModifier M(StatType t, float add, float mul = 1f)
                => new StatModifier { stat = t, add = add, multiply = mul };

            // --- Tier 0: 出発点 ---
            var baseDev = N("base_dev", "基礎空力研究", 0, 0,
                "風洞実験の無制限化。空力開発のリミッターを外す第一歩。",
                "現実のF1では風洞使用時間すら成績に応じて制限されている (ATR)。",
                null,
                M(StatType.DownforceFactor, 0.2f));

            // --- Tier 1: 禁止技術の入り口 ---
            var turbo = N("unlimited_turbo", "無制限ターボ (1800ps)", 1, 15,
                "過給圧制限のないツインターボ。直線はもはや射出。",
                "1980年代、予選用1500馬力ターボは燃料・過給圧規制で封じられた。",
                new[] { baseDev },
                M(StatType.TopSpeedKmh, 45f), M(StatType.Acceleration, 6f), M(StatType.Reliability, -0.05f));

            var groundEffect = N("ground_effect", "グラウンドエフェクト", 1, 15,
                "車体下面全体をベンチュリ管化し、路面に吸い付く。",
                "1983年、相次ぐ事故を受けフラットボトム規定で禁止された (2022年に管理された形で復活)。",
                new[] { baseDev },
                M(StatType.DownforceFactor, 1.6f), M(StatType.Drag, 0.04f));

            var feather = N("featherweight", "超軽量モノコック", 1, 10,
                "最低重量規定が無いので、強度の限界まで削る。",
                "最低重量規定は性能の平準化と安全のために存在する。削った強度は事故の時に支払う。",
                new[] { baseDev },
                M(StatType.WeightKg, -130f), M(StatType.Acceleration, 2f), M(StatType.Reliability, -0.08f));

            // --- Tier 2: 本格的な禁忌 ---
            var fanCar = N("fan_car", "ファンカー", 2, 25,
                "巨大ファンで床下の空気を強制排出。停車中でも天井に張り付ける吸着力を得る。",
                "ブラバムBT46Bは1978年、デビュー戦を圧勝して即座に自主的に封印された。",
                new[] { groundEffect },
                M(StatType.MechanicalGrip, 2.6f), M(StatType.Drag, 0.03f));

            var activeAero = N("active_aero", "アクティブエアロ", 2, 20,
                "全翼面が毎秒数百回可動。直線では翼を寝かせ、コーナーで立てる。",
                "可動空力デバイスは原則禁止。DRSはその唯一の管理された例外だった。",
                new[] { groundEffect },
                M(StatType.DownforceFactor, 1.0f), M(StatType.TopSpeedKmh, 12f), M(StatType.Drag, -0.03f));

            var cvt = N("cvt", "CVT無段変速", 2, 15,
                "変速の概念を消し、エンジンを常にパワーバンドに固定する。",
                "ウィリアムズが1993年にテストし、走る前にルールで消された技術。",
                new[] { turbo },
                M(StatType.Acceleration, 4f), M(StatType.TopSpeedKmh, 8f));

            var activeSusp = N("active_susp", "アクティブサスペンション", 2, 20,
                "路面を読んで姿勢を完全制御。荷重変動という概念が消える。",
                "1992年のFW14Bが完成させ、1994年に電子デバイス一斉禁止で消えた。",
                new[] { feather },
                M(StatType.MechanicalGrip, 0.7f), M(StatType.DownforceFactor, 0.3f));

            // --- Tier 3: 人間の領域を超える ---
            var traction = N("traction_ctrl", "完全トラクション制御", 3, 20,
                "ホイールスピンをコンピュータが0.001秒で殺す。アクセルは“踏むだけ”。",
                "「ドライバーの腕の見せ所を奪う」としてドライバーエイドは禁止されてきた。",
                new[] { activeSusp },
                M(StatType.Acceleration, 3f), M(StatType.MechanicalGrip, 0.3f));

            var fullActive = N("full_active", "全自動空力統合制御", 3, 35,
                "ファン・翼・サスを単一AIが統合制御。マシンは一つの生き物になる。……人間の反射速度では、もう制御に参加できない。",
                "ここまで来ると問いが反転する。『このマシンに、人間は必要なのか?』",
                new[] { fanCar, activeAero },
                M(StatType.DownforceFactor, 1.8f), M(StatType.Drag, -0.05f));

            var tree = ScriptableObject.CreateInstance<TechTreeData>();
            tree.name = "TechTree";
            tree.nodes = new List<TechNodeData>
            {
                baseDev, turbo, groundEffect, feather,
                fanCar, activeAero, cvt, activeSusp,
                traction, fullActive,
            };
            return tree;
        }

        // ----------------------------------------------------------------
        // Reality Track: 鈴鹿風
        // ----------------------------------------------------------------
        public static TrackData CreateSuzukaTrack()
        {
            var t = ScriptableObject.CreateInstance<TrackData>();
            t.name = "RealityTrack_Suzuka";
            t.trackName = "Reality Track 01: SUZUKA UNBOUND";
            t.description = "実測ジオメトリから再現した実寸の鈴鹿(全長5.8km)。S字、デグナー、ヘアピン、スプーン、130R──立体交差を持つ世界唯一の8の字が、無制限マシンの旋回Gを容赦なく引き出す。";
            t.roadWidth = 18f;
            t.lapCount = 3;
            t.controlPoints = new List<Vector3>(SuzukaTrackDefinition.CreateControlPoints());
            return t;
        }

        // ----------------------------------------------------------------
        // レースモード
        // ----------------------------------------------------------------
        public static RaceModeData CreateHumanGPMode()
        {
            var m = ScriptableObject.CreateInstance<RaceModeData>();
            m.name = "Mode_HumanGP";
            m.mode = GameMode.HumanGP;
            m.title = "HUMAN GP";
            m.tagline = "あなたがステアリングを握る。";
            m.description = "人間ドライバーによるグランプリ。マシンに規制はない。だが人体には約5Gという“変更不能なレギュレーション”が刻まれている。それを超えれば視野が狭まり、やがて意識を失う。";
            m.playerDrives = true;
            m.aiOpponentCount = 3;
            m.debriefInsight = "現実のF1がグラウンドエフェクトやファンカーを禁止したのは、速さの追求が人間の限界と安全を置き去りにしたからだ。レギュレーションとは、人間がレースの主役であり続けるための約束だった。";
            return m;
        }

        public static RaceModeData CreateTimeAttackMode()
        {
            var m = ScriptableObject.CreateInstance<RaceModeData>();
            m.name = "Mode_TimeAttack";
            m.mode = GameMode.TimeAttack;
            m.title = "TIME ATTACK";
            m.tagline = "コースと、自分の限界とだけ向き合う。";
            m.description = "ソロ走行。ライバルはいない。周回は無制限で、あなたのベストラップだけが記録される。マシンの理論値と自分のタイムの差が、そのまま“人間である代償”として突きつけられる。";
            m.playerDrives = true;
            m.aiOpponentCount = 0;
            m.debriefInsight = "誰もいないコースで競う相手は、理論ラップ──つまり“人間でなければ出せたはずのタイム”だけだ。その差を縮める努力こそ、ドライバーという存在の証明かもしれない。";
            return m;
        }

        public static RaceModeData CreateMachineGPMode()
        {
            var m = ScriptableObject.CreateInstance<RaceModeData>();
            m.name = "Mode_MachineGP";
            m.mode = GameMode.MachineGP;
            m.title = "MACHINE GP";
            m.tagline = "人間という制約が消えた世界を観戦する。";
            m.description = "AIドライバーによるグランプリ。ブラックアウトも恐怖も存在しない。あなたの設計したマシンが理論限界そのままの速度でコーナーへ消えていくのを、ただ見ていることしかできない。";
            m.playerDrives = false;
            m.aiOpponentCount = 3;
            m.debriefInsight = "制約のないレースの行き着く先は、設計図の優劣を確認する儀式だ。ミスがなければ逆転はなく、恐怖がなければ勇気もない。レギュレーションは安全装置であると同時に、“物語”を生むための装置でもあった。";
            return m;
        }
    }
}
