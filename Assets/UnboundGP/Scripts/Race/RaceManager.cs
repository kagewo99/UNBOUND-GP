using System.Collections.Generic;
using UnboundGP.Core;
using UnboundGP.Design;
using UnboundGP.Track;
using UnboundGP.UI;
using UnityEngine;

namespace UnboundGP.Race
{
    /// <summary>
    /// Race シーンの中枢。コース生成、出走者スポーン、カウントダウン、
    /// ラップ計測、順位、リザルト→デブリーフまでのレースフローを管理する。
    ///
    /// GameContext.SelectedMode に応じて:
    /// - Human GP   : プレイヤーが自分の設計マシンを運転。ライバルAIも人間のG限界を持つ。
    /// - Machine GP : 全車AI。プレイヤーの設計マシンもAIが理論限界で走らせ、プレイヤーは観戦する。
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        enum Phase { Countdown, Racing, Finished }

        /// <summary>出走者1台分のレース状態。</summary>
        class Entrant
        {
            public string name;
            public CarController car;
            public DriverCondition condition;
            public bool isPlayerMachine;

            public int lap;             // 0=フォーメーション、1〜=レース中
            public float lapStartTime;
            public float bestLap = float.MaxValue;
            public int nearestIdx = -1;
            public float lastS;
            public float raceDistance;  // 順位判定用の総走行距離

            public bool finished;
            public int place;
            public float stuckTimer;
        }

        static readonly Color[] TeamColors =
        {
            new Color(0.85f, 0.1f, 0.1f),  // プレイヤー: 赤
            new Color(0.1f, 0.35f, 0.85f),
            new Color(0.9f, 0.75f, 0.1f),
            new Color(0.15f, 0.7f, 0.65f),
            new Color(0.6f, 0.3f, 0.8f),
        };

        GameContext ctx;
        TrackPath path;
        RaceHUD hud;
        IRaceCamera raceCam;

        readonly List<Entrant> entrants = new List<Entrant>();
        Entrant playerEntrant;
        int watchIndex;

        Phase phase = Phase.Countdown;
        float countdown = 4f;
        float raceStartTime;
        int finishCounter;
        bool debriefShown;

        void Start()
        {
            ctx = GameContext.I;
            var modeData = ctx.SelectedModeData;

            EnsureSceneEssentials();
            path = TrackBuilder.Build(ctx.Track);
            SpawnEntrants(modeData);

            hud = RaceHUD.Create();
            hud.SetMode($"{modeData.title} ── {ctx.Track.trackName}");
            hud.SetHint(modeData.playerDrives
                ? "一人称視点  WASD/矢印: 運転   Space: ブレーキ   R: コース復帰"
                : "観戦モード   Tab: カメラ切替   ※あなたのマシンはAIが運転しています");

            // カメラセットアップ:Human GP は一人称(人体のGを自分の目で受ける)、
            // Machine GP は三人称(人間不在のレースを外から眺める)。視点の対比でテーマを語る。
            var cam = Camera.main;
            if (ctx.SelectedMode == GameMode.HumanGP)
                raceCam = cam.gameObject.AddComponent<CockpitCamera>();
            else
                raceCam = cam.gameObject.AddComponent<ChaseCamera>();

            watchIndex = entrants.IndexOf(playerEntrant);
            raceCam.SetTarget(playerEntrant.car);
        }

        /// <summary>シーンが空でも動くよう、カメラとライトを保証する。</summary>
        void EnsureSceneEssentials()
        {
            if (Camera.main == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            if (FindObjectOfType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            }
        }

        // ----------------------------------------------------------------
        // スポーン
        // ----------------------------------------------------------------
        void SpawnEntrants(Data.RaceModeData modeData)
        {
            var playerStats = ctx.CurrentMachineStats();
            int total = 1 + modeData.aiOpponentCount;

            for (int i = 0; i < total; i++)
            {
                // S/Fラインの手前にグリッドを並べる (千鳥配置)
                float gridDist = path.TotalLength - 30f - i * 14f;
                Vector3 pos = path.PointAtDistance(gridDist)
                              + path.RotationAtDistance(gridDist) * Vector3.right * (i % 2 == 0 ? 3.2f : -3.2f);
                Quaternion rot = path.RotationAtDistance(gridDist);

                bool isPlayerMachine = i == 0;
                var e = new Entrant { isPlayerMachine = isPlayerMachine };

                MachineStats stats;
                DriverProfile profile;
                bool playerControlled;

                if (isPlayerMachine)
                {
                    stats = playerStats;
                    playerControlled = modeData.playerDrives;
                    profile = modeData.mode == GameMode.HumanGP
                        ? DriverProfile.Human("YOU")
                        : DriverProfile.Machine("UNIT-00 (あなたの設計)");
                    e.name = modeData.mode == GameMode.HumanGP ? "YOU" : "UNIT-00 ★あなたの設計";
                }
                else
                {
                    // ライバルはプレイヤー比 93〜98% の性能 (将来は独自の研究状態を持たせる拡張点)
                    stats = ScaleStats(playerStats, 0.98f - i * 0.025f);
                    playerControlled = false;
                    profile = modeData.mode == GameMode.HumanGP
                        ? DriverProfile.Human($"RIVAL-{i:00}", 0.88f + i * 0.02f)
                        : DriverProfile.Machine($"UNIT-{i:00}");
                    e.name = modeData.mode == GameMode.HumanGP ? $"RIVAL-{i:00}" : $"UNIT-{i:00}";
                }

                e.car = CarFactory.Create(e.name, TeamColors[i % TeamColors.Length],
                    stats, pos, rot, profile, playerControlled, path);
                e.condition = e.car.Condition;
                e.car.InputEnabled = false;
                e.nearestIdx = path.NearestIndex(e.car.transform.position);
                e.lastS = path.DistanceAt(e.nearestIdx);

                entrants.Add(e);
                if (isPlayerMachine) playerEntrant = e;
            }
        }

        static MachineStats ScaleStats(MachineStats s, float f)
        {
            s.topSpeedKmh *= f;
            s.acceleration *= f;
            s.mechanicalGrip *= f;
            s.downforceFactor *= f;
            return s;
        }

        // ----------------------------------------------------------------
        // レース進行
        // ----------------------------------------------------------------
        void Update()
        {
            switch (phase)
            {
                case Phase.Countdown: UpdateCountdown(); break;
                case Phase.Racing:
                case Phase.Finished: UpdateRace(); break;
            }
            UpdateHud();
            UpdateSpectator();
        }

        void UpdateCountdown()
        {
            countdown -= Time.deltaTime;
            if (countdown <= 0f)
            {
                phase = Phase.Racing;
                raceStartTime = Time.time;
                foreach (var e in entrants)
                {
                    e.car.InputEnabled = true;
                    e.lapStartTime = raceStartTime;
                }
                hud.SetCountdown("");
            }
            else
            {
                int n = Mathf.CeilToInt(countdown - 1f);
                hud.SetCountdown(n <= 0 ? "GO!" : n.ToString());
            }
        }

        void UpdateRace()
        {
            float L = path.TotalLength;
            foreach (var e in entrants)
            {
                // 進行度の更新 (ヒント付き最寄り点探索なので立体交差でも安全)
                e.nearestIdx = path.NearestIndex(e.car.transform.position, e.nearestIdx);
                float s = path.DistanceAt(e.nearestIdx);

                // S/Fライン通過 (距離が一気に巻き戻る) を検出
                if (phase == Phase.Racing && e.lastS > L * 0.7f && s < L * 0.3f)
                {
                    OnCrossedLine(e);
                }
                e.lastS = s;
                // lap0 (ライン通過前) はグリッド位置ぶんマイナスにして順位を正しく保つ
                e.raceDistance = e.lap == 0 ? s - L : (e.lap - 1) * L + s;

                UpdateStuckRecovery(e, s);
            }

            // プレイヤー(のマシン)がフィニッシュしたらデブリーフへ
            if (!debriefShown && playerEntrant.finished)
            {
                debriefShown = true;
                phase = Phase.Finished;
                Invoke(nameof(ShowDebrief), 1.6f);
            }

            // 手動リセット
            if (ctx.SelectedModeData.playerDrives && Input.GetKeyDown(KeyCode.R))
            {
                ResetEntrant(playerEntrant);
            }
        }

        void OnCrossedLine(Entrant e)
        {
            float now = Time.time;
            if (e.lap == 0)
            {
                // グリッド→最初のライン通過 = ラップ1開始
                e.lap = 1;
                e.lapStartTime = now;
                return;
            }

            float lapTime = now - e.lapStartTime;
            if (lapTime < e.bestLap) e.bestLap = lapTime;
            e.lapStartTime = now;
            e.lap++;

            if (e.lap > ctx.Track.lapCount && !e.finished)
            {
                e.finished = true;
                e.place = ++finishCounter;
            }
        }

        /// <summary>スタック/転倒/失神事故からの自動復帰。</summary>
        void UpdateStuckRecovery(Entrant e, float s)
        {
            if (e.finished) { return; }

            bool flipped = Vector3.Dot(e.car.transform.up, Vector3.up) < 0.15f;
            bool offTrack = Vector3.Distance(e.car.transform.position, path.Points[e.nearestIdx])
                            > ctx.Track.roadWidth * 0.5f + 10f;
            bool slow = e.car.CurrentSpeedMs < 1.5f && phase == Phase.Racing;
            bool blackedOut = e.condition != null && e.condition.IsBlackedOut;

            // 失神中は“事故が起きるところ”まで見せたいので復帰させない
            if (!blackedOut && (flipped || (slow && Time.time - raceStartTime > 5f) || (offTrack && e.car.CurrentSpeedMs < 4f)))
            {
                e.stuckTimer += Time.deltaTime;
                if (e.stuckTimer > 3f) ResetEntrant(e);
            }
            else
            {
                e.stuckTimer = 0f;
            }
        }

        void ResetEntrant(Entrant e)
        {
            float s = path.DistanceAt(e.nearestIdx);
            e.car.ResetTo(path.PointAtDistance(s), path.RotationAtDistance(s));
            e.stuckTimer = 0f;
        }

        // ----------------------------------------------------------------
        // HUD / 観戦
        // ----------------------------------------------------------------
        void UpdateHud()
        {
            if (hud == null) return;

            var watch = entrants[Mathf.Clamp(watchIndex, 0, entrants.Count - 1)];
            bool humanMode = ctx.SelectedMode == GameMode.HumanGP;

            hud.SetTelemetry(
                watch.car.CurrentSpeedKmh,
                watch.condition != null ? watch.condition.CurrentG : watch.car.CurrentLateralG,
                watch.condition != null ? watch.condition.BlackoutMeter : 0f,
                watch.condition != null && watch.condition.IsBlackedOut,
                humanMode);

            float t = phase == Phase.Countdown ? 0f : Time.time - watch.lapStartTime;
            hud.SetLap(
                $"LAP {Mathf.Clamp(watch.lap, 1, ctx.Track.lapCount)}/{ctx.Track.lapCount}",
                LapTimeEstimator.Format(t));

            // 順位表 (総走行距離でソート)
            var sorted = new List<Entrant>(entrants);
            sorted.Sort((a, b) =>
            {
                if (a.finished != b.finished) return a.finished ? -1 : 1;
                if (a.finished && b.finished) return a.place.CompareTo(b.place);
                return b.raceDistance.CompareTo(a.raceDistance);
            });
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < sorted.Count; i++)
            {
                var e = sorted[i];
                string best = e.bestLap < float.MaxValue ? LapTimeEstimator.Format(e.bestLap) : "-:--.---";
                sb.AppendLine($"{i + 1}. {e.name}   BEST {best}{(e.finished ? "  [FIN]" : "")}");
            }
            hud.SetStandings(sb.ToString());
        }

        void UpdateSpectator()
        {
            if (ctx.SelectedModeData.playerDrives) return;
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                watchIndex = (watchIndex + 1) % entrants.Count;
                raceCam.SetTarget(entrants[watchIndex].car);
            }
        }

        // ----------------------------------------------------------------
        // フィニッシュ → デブリーフ
        // ----------------------------------------------------------------
        void ShowDebrief()
        {
            var stats = ctx.CurrentMachineStats();
            int place = playerEntrant.place;
            int points = place switch { 1 => 40, 2 => 28, 3 => 20, 4 => 14, _ => 10 };

            ctx.Save.racesCompleted++;
            ctx.AddResearchPoints(points); // 内部で SaveGame される

            // 最終順位行
            var sorted = new List<Entrant>(entrants);
            sorted.Sort((a, b) =>
            {
                if (a.finished != b.finished) return a.finished ? -1 : 1;
                if (a.finished && b.finished) return a.place.CompareTo(b.place);
                return b.raceDistance.CompareTo(a.raceDistance);
            });
            var lines = new string[sorted.Count];
            for (int i = 0; i < sorted.Count; i++)
            {
                var e = sorted[i];
                string best = e.bestLap < float.MaxValue ? LapTimeEstimator.Format(e.bestLap) : "-:--.---";
                lines[i] = $"{i + 1}. {e.name} ── BEST {best}";
            }

            var result = new RaceResult
            {
                mode = ctx.SelectedMode,
                standingLines = lines,
                playerPlace = place,
                playerBestLap = playerEntrant.bestLap < float.MaxValue ? playerEntrant.bestLap : -1f,
                peakG = playerEntrant.condition.PeakG,
                blackoutCount = playerEntrant.condition.BlackoutCount,
                timeOverHumanLimit = playerEntrant.condition.TimeOverHumanLimit,
                estHumanLap = LapTimeEstimator.Estimate(ctx.Track, stats, MachineStats.HumanSustainedGLimit),
                estMachineLap = LapTimeEstimator.Estimate(ctx.Track, stats, 1000f),
                pointsAwarded = points,
            };
            DebriefView.Show(result);
        }
    }
}
