using System;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace MitarashiDango.AvatarCatalog
{
    public static class SceneProcessor
    {
        public static List<SceneAsset> GetAllSceneAssets()
        {
            return AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets" })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
                .Where(asset => asset != null)
                .ToList();
        }

        public static void WalkAllScenes(Action<SceneAsset, Scene> walkAction)
        {
            var sceneAssets = GetAllSceneAssets();
            foreach (var sceneAsset in sceneAssets)
            {
                ProcessSceneTemporarily(sceneAsset, (scene) => walkAction(sceneAsset, scene));
            }
        }

        public static void ProcessSceneTemporarily(SceneAsset sceneAsset, Action<Scene> processAction)
        {
            ProcessSceneTemporarily(AssetDatabase.GetAssetPath(sceneAsset), processAction);
        }

        public static void ProcessSceneTemporarily(string scenePath, Action<Scene> processAction)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError("[SceneProcessor] Scene path is null or empty.");
                return;
            }

            var scene = EditorSceneManager.GetSceneByPath(scenePath);

            if (scene.IsValid() && scene.isLoaded)
            {
                // 既にロード済み（アクティブ、またはマルチシーン編集で開いている）場合
                // 新たに開く/閉じる処理はスキップしてアクションのみ実行
                try
                {
                    processAction?.Invoke(scene);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SceneProcessor] Error occurred while processing scene '{scene.name}': {e.Message}\n{e.StackTrace}");
                }
                return;
            }

            try
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneProcessor] Failed to open scene at path '{scenePath}': {e.Message}");
                return;
            }

            // 処理実行と後始末
            try
            {
                processAction?.Invoke(scene);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneProcessor] Error occurred while processing scene '{scene.name}': {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                // 処理が成功しても失敗しても、一時的に開いたシーンは必ず閉じる
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}