using System;
using System.Collections.Generic;
using UnboundGP.Data;
using UnityEngine;

namespace UnboundGP.Core
{
    /// <summary>
    /// シーンを跨いで生存するゲーム全体のコンテキスト (シングルトン)。
    /// ・データアセット (研究ツリー/シャシー/トラック/モード) の解決
    /// ・研究状態と装備の管理、セーブ/ロード
    /// ・選択中のレースモードの保持
    /// を担当する。どのシーンから再生しても GameContext.I へのアクセスで自動生成される。
    /// </summary>
    public class GameContext : MonoBehaviour
    {
        const string SaveKey = "UNBOUND_GP_SAVE_V1";

        static GameContext instance;

        public static GameContext I
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[GameContext]");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<GameContext>();
                    instance.Initialize();
                }
                return instance;
            }
        }

        public TechTreeData TechTree { get; private set; }
        public MachineChassisData Chassis { get; private set; }
        public TrackData Track { get; private set; }
        public RaceModeData HumanGPMode { get; private set; }
        public RaceModeData MachineGPMode { get; private set; }
        public RaceModeData TimeAttackMode { get; private set; }
        public SaveData Save { get; private set; }

        /// <summary>ガレージで選択され、Race シーンが参照するモード。</summary>
        public GameMode SelectedMode = GameMode.HumanGP;

        public RaceModeData SelectedModeData
        {
            get
            {
                switch (SelectedMode)
                {
                    case GameMode.MachineGP: return MachineGPMode;
                    case GameMode.TimeAttack: return TimeAttackMode;
                    default: return HumanGPMode;
                }
            }
        }

        /// <summary>研究/装備が変化したときに UI が購読するイベント。</summary>
        public event Action StateChanged;

        void Initialize()
        {
            // Resources 内のアセットを優先し、無ければ既定データを実行時生成する。
            // これにより「Setup 未実行でも、どのシーンを直接再生しても」動作する。
            TechTree = LoadOr("UnboundGP/TechTree", DefaultDataFactory.CreateTechTree);
            Chassis = LoadOr("UnboundGP/Chassis", DefaultDataFactory.CreateChassis);
            Track = LoadOr("UnboundGP/RealityTrack_Suzuka", DefaultDataFactory.CreateSuzukaTrack);
            HumanGPMode = LoadOr("UnboundGP/Mode_HumanGP", DefaultDataFactory.CreateHumanGPMode);
            MachineGPMode = LoadOr("UnboundGP/Mode_MachineGP", DefaultDataFactory.CreateMachineGPMode);
            TimeAttackMode = LoadOr("UnboundGP/Mode_TimeAttack", DefaultDataFactory.CreateTimeAttackMode);

            LoadSave();

            // コスト0のルートノードは常に解放済みにする
            foreach (var n in TechTree.nodes)
            {
                if (n != null && n.researchCost <= 0 && !Save.unlockedTechIds.Contains(n.techId))
                {
                    Save.unlockedTechIds.Add(n.techId);
                    if (!Save.equippedTechIds.Contains(n.techId))
                        Save.equippedTechIds.Add(n.techId);
                }
            }
        }

        static T LoadOr<T>(string path, Func<T> fallback) where T : ScriptableObject
        {
            var asset = Resources.Load<T>(path);
            return asset != null ? asset : fallback();
        }

        // ----------------------------------------------------------------
        // 研究 (Research)
        // ----------------------------------------------------------------
        public bool IsUnlocked(TechNodeData node)
            => node != null && Save.unlockedTechIds.Contains(node.techId);

        public bool PrerequisitesMet(TechNodeData node)
        {
            foreach (var p in node.prerequisites)
            {
                if (p != null && !IsUnlocked(p)) return false;
            }
            return true;
        }

        public bool CanUnlock(TechNodeData node)
            => node != null
               && !IsUnlocked(node)
               && PrerequisitesMet(node)
               && Save.researchPoints >= node.researchCost;

        /// <summary>研究ポイントを消費して技術を解放する。解放と同時に装備もする。</summary>
        public bool TryUnlock(TechNodeData node)
        {
            if (!CanUnlock(node)) return false;
            Save.researchPoints -= node.researchCost;
            Save.unlockedTechIds.Add(node.techId);
            Save.equippedTechIds.Add(node.techId);
            SaveGame();
            StateChanged?.Invoke();
            return true;
        }

        public void AddResearchPoints(int points)
        {
            Save.researchPoints += Mathf.Max(0, points);
            SaveGame();
            StateChanged?.Invoke();
        }

        // ----------------------------------------------------------------
        // マシン設計 (装備)
        // ----------------------------------------------------------------
        public bool IsEquipped(TechNodeData node)
            => node != null && Save.equippedTechIds.Contains(node.techId);

        /// <summary>技術の装備/取り外し。設計画面から呼ばれる。</summary>
        public void SetEquipped(TechNodeData node, bool equipped)
        {
            if (node == null || !IsUnlocked(node)) return;
            if (equipped)
            {
                if (Save.equippedTechIds.Contains(node.techId)) return;
                Save.equippedTechIds.Add(node.techId);
            }
            else if (!Save.equippedTechIds.Remove(node.techId))
            {
                return;
            }
            SaveGame();
            StateChanged?.Invoke();
        }

        /// <summary>現在装備中の技術ノード一覧。</summary>
        public List<TechNodeData> EquippedTechs()
        {
            var result = new List<TechNodeData>();
            foreach (var id in Save.equippedTechIds)
            {
                var n = TechTree.FindById(id);
                if (n != null) result.Add(n);
            }
            return result;
        }

        /// <summary>ベースシャシー + 装備技術から最終性能を計算する。</summary>
        public MachineStats CurrentMachineStats()
        {
            var mods = new List<StatModifier>();
            foreach (var n in EquippedTechs()) mods.AddRange(n.modifiers);
            return Chassis.baseStats.Apply(mods);
        }

        // ----------------------------------------------------------------
        // セーブ / ロード
        // ----------------------------------------------------------------
        public void SaveGame()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Save));
            PlayerPrefs.Save();
        }

        void LoadSave()
        {
            Save = PlayerPrefs.HasKey(SaveKey)
                ? JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey))
                : new SaveData();
            if (Save == null) Save = new SaveData();
        }

        /// <summary>進行をすべて初期化する (タイトル画面のデバッグ用)。</summary>
        public void ResetSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            Save = new SaveData();
            foreach (var n in TechTree.nodes)
            {
                if (n != null && n.researchCost <= 0)
                {
                    Save.unlockedTechIds.Add(n.techId);
                    Save.equippedTechIds.Add(n.techId);
                }
            }
            SaveGame();
            StateChanged?.Invoke();
        }
    }
}
