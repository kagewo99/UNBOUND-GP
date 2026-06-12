using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnboundGP.UI
{
    /// <summary>シーン遷移ヘルパー。ビルド設定に無い場合は誘導メッセージを出す。</summary>
    public static class SceneFlow
    {
        public const string MainMenu = "MainMenu";
        public const string Garage = "Garage";
        public const string Race = "Race";

        public static void Load(string sceneName)
        {
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                Debug.LogWarning(
                    $"シーン '{sceneName}' がビルド設定にありません。" +
                    "メニュー [UNBOUND GP > Setup Project] を実行してシーンを生成してください。");
            }
        }

        public static void Reload()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
