using System.Collections.Generic;
using System.IO;
using UnboundGP.Data;
using UnboundGP.Race;
using UnboundGP.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnboundGP.EditorTools
{
    /// <summary>
    /// プロジェクトのワンクリックセットアップ。
    /// メニュー [UNBOUND GP > Setup Project] で
    /// 1. 既定の ScriptableObject アセット群を Resources 配下に書き出し (既存は保持)
    /// 2. MainMenu / Garage / Race の3シーンを生成
    /// 3. ビルド設定にシーンを登録
    /// を行う。生成後は各アセットをインスペクタで自由に調整できる。
    /// </summary>
    public static class UnboundProjectBuilder
    {
        const string DataDir = "Assets/UnboundGP/Resources/UnboundGP";
        const string SceneDir = "Assets/UnboundGP/Scenes";

        [MenuItem("UNBOUND GP/Setup Project (Data + Scenes)")]
        public static void Setup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            CreateDataAssets();
            CreateScenes();

            EditorUtility.DisplayDialog("UNBOUND GP",
                "セットアップ完了。\n\nAssets/UnboundGP/Scenes/MainMenu.unity を開いて再生してください。\n" +
                "データは Assets/UnboundGP/Resources/UnboundGP/ で調整できます。",
                "OK");
        }

        // ----------------------------------------------------------------
        // データアセット
        // ----------------------------------------------------------------
        static void CreateDataAssets()
        {
            EnsureFolder(DataDir);

            // 研究ツリーはノードをサブアセットとして1ファイルにまとめる
            string treePath = DataDir + "/TechTree.asset";
            if (AssetDatabase.LoadAssetAtPath<TechTreeData>(treePath) == null)
            {
                var tree = DefaultDataFactory.CreateTechTree();
                AssetDatabase.CreateAsset(tree, treePath);
                foreach (var node in tree.nodes)
                {
                    AssetDatabase.AddObjectToAsset(node, tree);
                }
                AssetDatabase.ImportAsset(treePath);
            }

            CreateIfMissing(DefaultDataFactory.CreateChassis, DataDir + "/Chassis.asset");
            CreateIfMissing(DefaultDataFactory.CreateSuzukaTrack, DataDir + "/RealityTrack_Suzuka.asset");
            CreateIfMissing(DefaultDataFactory.CreateHumanGPMode, DataDir + "/Mode_HumanGP.asset");
            CreateIfMissing(DefaultDataFactory.CreateMachineGPMode, DataDir + "/Mode_MachineGP.asset");

            AssetDatabase.SaveAssets();
        }

        static void CreateIfMissing<T>(System.Func<T> factory, string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
            AssetDatabase.CreateAsset(factory(), path);
        }

        // ----------------------------------------------------------------
        // シーン
        // ----------------------------------------------------------------
        static void CreateScenes()
        {
            EnsureFolder(SceneDir);

            var scenePaths = new List<string>
            {
                CreateScene(SceneFlow.MainMenu, root => root.AddComponent<MainMenuScreen>()),
                CreateScene(SceneFlow.Garage, root => root.AddComponent<GarageScreen>()),
                CreateScene(SceneFlow.Race, root => root.AddComponent<RaceManager>()),
            };

            var buildScenes = new List<EditorBuildSettingsScene>();
            foreach (var p in scenePaths)
            {
                buildScenes.Add(new EditorBuildSettingsScene(p, true));
            }
            EditorBuildSettings.scenes = buildScenes.ToArray();
        }

        /// <summary>
        /// ルートに1コンポーネントだけを置いたシーンを生成する。
        /// 各シーンの中身は実行時にコードで構築されるため、シーンファイルは最小で済む。
        /// </summary>
        static string CreateScene(string name, System.Action<GameObject> attach)
        {
            string path = $"{SceneDir}/{name}.unity";
            if (File.Exists(path)) return path; // 既存シーンは上書きしない

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var root = new GameObject(name);
            attach(root);
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // ----------------------------------------------------------------
        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
