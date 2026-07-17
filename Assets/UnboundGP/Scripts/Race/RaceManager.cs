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
        UnboundGP.UI.WheelCalibrationOverlay calibration;

        readonly List<Entrant> entrants = new List<Entrant>();
        Entrant playerEntrant;
        int watchIndex;

        Phase phase = Phase.Countdown;
        float countdown = 4f;
        float raceStartTime;
        int finishCounter;
        bool debriefShown;

        /// <summary>タイムアタック(ソロ・周回無制限)か。</summary>
        bool IsTimeAttack => ctx.SelectedMode == GameMode.TimeAttack;

        /// <summary>タイムアタックのラップ履歴(新しい順に表示するため追記式)。</summary>
        readonly List<float> taLaps = new List<float>();

        void Start()
        {
            ctx = GameContext.I;
            var modeData = ctx.SelectedModeData;

            EnsureSceneEssentials();
            path = TrackBuilder.Build(ctx.Track);
            SpawnEntrants(modeData);

            hud = RaceHUD.Create();
            hud.SetMode($"{modeData.title} ── {ctx.Track.trackName}");
            string driveHint = "W/↑:アクセル S/↓:ブレーキ A/D:操舵  E/Q:シフト(低速でN→R) T:AT/MT  R:復帰 F1:ハンドル設定 F3:物理数値";
            if (IsTimeAttack) driveHint += "  Esc:走行終了";
            hud.SetHint(modeData.playerDrives
                ? driveHint
                : "観戦モード   Tab:カメラ切替 F3:物理数値   ※あなたのマシンはAIが運転しています");

            // カメラセットアップ:自分で運転するモードは一人称(人体のGを自分の目で受ける)、
            // Machine GP は三人称(人間不在のレースを外から眺める)。視点の対比でテーマを語る。
            var cam = Camera.main;
            if (modeData.playerDrives)
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
            // CVT(無段変速)を装備しているか。装備時は全車シフト無し。
            bool hasCVT = ctx.Save.equippedTechIds.Contains("cvt");

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
                    // Machine GP 以外(Human GP / Time Attack)は人間が乗る
                    profile = modeData.mode != GameMode.MachineGP
                        ? DriverProfile.Human("YOU")
                        : DriverProfile.Machine("UNIT-00 (あなたの設計)");
                    e.name = modeData.mode != GameMode.MachineGP ? "YOU" : "UNIT-00 ★あなたの設計";
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
                    stats, pos, rot, profile, playerControlled, path, hasCVT);
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
            HandleCalibrationToggle();
            switch (phase)
            {
                case Phase.Countdown: UpdateCountdown(); break;
                case Phase.Racing:
                case Phase.Finished: UpdateRace(); break;
            }
            UpdateHud();
            UpdateSpectator();
        }

        /// <summary>F1 でハンドル設定(キャリブレーション)を開く。Human GP のみ。</summary>
        void HandleCalibrationToggle()
        {
            if (!ctx.SelectedModeData.playerDrives) return;
            if (!Input.GetKeyDown(KeyCode.F1)) return;
            if (calibration == null)
            {
                var pid = playerEntrant.car.GetComponent<PlayerInputDriver>();
                calibration = UnboundGP.UI.WheelCalibrationOverlay.Open(pid, () => calibration = null);
            }
            else
            {
                calibration.CloseExternally();
            }
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

            // タイムアタック: Esc でセッション終了 → デブリーフ
            if (IsTimeAttack && !debriefShown && Input.GetKeyDown(KeyCode.Escape))
            {
                debriefShown = true;
                phase = Phase.Finished;
                playerEntrant.place = 1;
                ShowDebrief();
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

            if (IsTimeAttack)
            {
                // 周回無制限。ラップ履歴だけ積む(フィニッシュしない)
                if (e.isPlayerMachine) taLaps.Add(lapTime);
                return;
            }

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
            bool humanMode = ctx.SelectedMode != GameMode.MachineGP;

            // ---- F3: 物理テレメトリの表示/更新 ----
            if (Input.GetKeyDown(KeyCode.F3)) hud.TogglePhysics();
            if (hud.PhysicsVisible) hud.SetPhysics(BuildPhysicsReadout(watch.car));

            hud.SetTelemetry(
                watch.car.CurrentSpeedKmh,
                watch.condition != null ? watch.condition.CurrentG : watch.car.CurrentG,
                watch.condition != null ? watch.condition.BlackoutMeter : 0f,
                watch.condition != null && watch.condition.IsBlackedOut,
                humanMode);

            var gb = watch.car.Gearbox;
            hud.SetGearRPM(gb.GearLabel, gb.Rpm, Transmission.RedlineRPM, Transmission.MaxRPM, gb.IsCVT, gb.AutoShift, gb.AtLimiter);

            float t = phase == Phase.Countdown ? 0f : Time.time - watch.lapStartTime;
            hud.SetLap(
                IsTimeAttack
                    ? $"LAP {Mathf.Max(watch.lap, 1)}"
                    : $"LAP {Mathf.Clamp(watch.lap, 1, ctx.Track.lapCount)}/{ctx.Track.lapCount}",
                LapTimeEstimator.Format(t));

            if (IsTimeAttack)
            {
                // タイムアタック: 順位表の代わりにラップボード(ベスト+直近履歴)
                var tb = new System.Text.StringBuilder();
                string best = playerEntrant.bestLap < float.MaxValue
                    ? LapTimeEstimator.Format(playerEntrant.bestLap) : "-:--.---";
                tb.AppendLine($"BEST  {best}");
                for (int i = taLaps.Count - 1; i >= Mathf.Max(0, taLaps.Count - 5); i--)
                {
                    string mark = Mathf.Approximately(taLaps[i], playerEntrant.bestLap) ? " *" : "";
                    tb.AppendLine($"LAP{i + 1,2}  {LapTimeEstimator.Format(taLaps[i])}{mark}");
                }
                hud.SetStandings(tb.ToString());
                return;
            }

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

        /// <summary>
        /// F3 で表示する物理テレメトリ。設計画面の数値がコース上で実際にどう働いているかを
        /// リアルタイムに可視化する(ダウンフォース・グリップ限界・荷重移動・実測G)。
        /// </summary>
        string BuildPhysicsReadout(CarController car)
        {
            var st = car.Stats;
            float v = car.CurrentSpeedMs;
            float r = v / MachineStats.ReferenceSpeedMs;
            float dfG = st.downforceFactor * r * r;              // 現在速度のダウンフォース[G]
            float dfKgf = st.weightKg * dfG;                     // ≒ 車体を押し付ける力[kgf]
            float gripG = st.MaxCorneringG(v);                   // 旋回限界[G]

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>━ PHYSICS TELEMETRY (F3で閉じる) ━</b>");
            sb.AppendLine($"速度 <b>{car.CurrentSpeedKmh:0}</b> km/h   実効最高速 {st.EffectiveTopSpeedKmh:0} km/h");
            sb.AppendLine($"ダウンフォース <b>{dfG:0.00} G</b> ≒ {dfKgf:0} kgf");
            sb.AppendLine($"  内訳: 機械グリップ {st.mechanicalGrip:0.00}G + 速度依存 {dfG:0.00}G");
            sb.AppendLine($"旋回限界 <b>{gripG:0.00} G</b> @現在速度");
            sb.AppendLine($"実測G  総合 {car.CurrentG:0.0} / 横 {car.LateralGSigned:+0.0;-0.0} / 縦 {car.LongitudinalGSigned:+0.0;-0.0}");
            sb.AppendLine($"スリップ角 {car.SlipAngleDeg:0.0}°   リア駆動使用率 {car.RearGripUsage * 100f:0}%");
            sb.AppendLine($"アクスル荷重  前 {car.FrontLoadKg:0} / 後 {car.RearLoadKg:0} kgf (車重 {st.weightKg:0} kg)");
            sb.AppendLine($"空気抵抗 {st.drag:0.00}   信頼性 {st.reliability * 100f:0}%");
            sb.AppendLine($"ステア入力 {car.SteerInput:+0.00;-0.00}   ギア {car.Gearbox.GearLabel} / {car.Gearbox.Rpm:0} rpm");
            return sb.ToString();
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
            // タイムアタックは完走概念がないため、1周以上でデータ収集報酬
            int points = IsTimeAttack
                ? (taLaps.Count > 0 ? 12 : 4)
                : place switch { 1 => 40, 2 => 28, 3 => 20, 4 => 14, _ => 10 };

            ctx.Save.racesCompleted++;
            ctx.AddResearchPoints(points); // 内部で SaveGame される

            string[] lines;
            if (IsTimeAttack)
            {
                // ラップ履歴(全周)を結果行として渡す
                lines = new string[taLaps.Count];
                for (int i = 0; i < taLaps.Count; i++)
                {
                    string mark = Mathf.Approximately(taLaps[i], playerEntrant.bestLap) ? "  ★BEST" : "";
                    lines[i] = $"LAP {i + 1} ── {LapTimeEstimator.Format(taLaps[i])}{mark}";
                }
            }
            else
            {
                // 最終順位行
                var sorted = new List<Entrant>(entrants);
                sorted.Sort((a, b) =>
                {
                    if (a.finished != b.finished) return a.finished ? -1 : 1;
                    if (a.finished && b.finished) return a.place.CompareTo(b.place);
                    return b.raceDistance.CompareTo(a.raceDistance);
                });
                lines = new string[sorted.Count];
                for (int i = 0; i < sorted.Count; i++)
                {
                    var e = sorted[i];
                    string best = e.bestLap < float.MaxValue ? LapTimeEstimator.Format(e.bestLap) : "-:--.---";
                    lines[i] = $"{i + 1}. {e.name} ── BEST {best}";
                }
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
