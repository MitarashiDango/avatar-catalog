using System;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace MitarashiDango.AvatarCatalog
{
    public static class SceneProcessor
    {
        public static IEnumerable<string> GetAllSceneAssetPaths()
        {
            return AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets" })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => !string.IsNullOrEmpty(path));
        }

        public static void WalkScenes(IEnumerable<string> sceneAssetPaths, Action<SceneAsset, Scene> walkAction)
        {
            foreach (var sceneAssetPath in sceneAssetPaths)
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(sceneAssetPath);
                ProcessSceneTemporarily(sceneAssetPath, (scene) => walkAction(sceneAsset, scene));
            }
        }

        public static IEnumerable<T> WalkScenes<T>(IEnumerable<string> sceneAssetPaths, Func<SceneAsset, Scene, T> walkAction)
        {
            return sceneAssetPaths.Select(sceneAssetPath =>
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(sceneAssetPath);
                return ProcessSceneTemporarily(sceneAssetPath, (scene) => walkAction(sceneAsset, scene));
            });
        }

        public static T ProcessSceneTemporarily<T>(SceneAsset sceneAsset, Func<Scene, T> processAction)
        {
            return ProcessSceneTemporarily(AssetDatabase.GetAssetPath(sceneAsset), processAction);
        }

        public static T ProcessSceneTemporarily<T>(string scenePath, Func<Scene, T> processAction)
        {
            return ProcessSceneTemporarilyInternal(scenePath, processAction);
        }

        public static void ProcessSceneTemporarily(SceneAsset sceneAsset, Action<Scene> processAction)
        {
            ProcessSceneTemporarily(AssetDatabase.GetAssetPath(sceneAsset), processAction);
        }

        public static void ProcessSceneTemporarily(string scenePath, Action<Scene> processAction)
        {
            ProcessSceneTemporarilyInternal<object>(scenePath, scene =>
            {
                if (processAction == null)
                {
                    return default;
                }

                processAction.Invoke(scene);
                return default;
            });
        }

        internal static T ProcessSceneTemporarilyInternal<T>(string scenePath, Func<Scene, T> processAction)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                throw new Exception("Scene path is null or empty.");
            }

            if (processAction == null)
            {
                return default;
            }

            var scene = EditorSceneManager.GetSceneByPath(scenePath);
            var shouldCloseScene = false;

            // 既にロード済み（アクティブ、またはマルチシーン編集で開いている）場合
            // 新たに開く/閉じる処理はスキップしてアクションのみ実行
            if (!scene.IsValid() || !scene.isLoaded)
            {
                try
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    shouldCloseScene = true;
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to open scene at path '{scenePath}'", e);
                }
            }

            try
            {
                return processAction.Invoke(scene);
            }
            catch (Exception e)
            {
                throw new Exception($"Error occurred while processing scene '{scene.name}'", e);
            }
            finally
            {
                // 処理が成功しても失敗しても、一時的に開いたシーンは必ず閉じる
                if (shouldCloseScene)
                {
                    if (scene.isDirty)
                    {
                        var choice = SceneSaveConfirmationDialog.Prompt(scene.name);
                        if (choice == SceneSaveChoice.Save)
                        {
                            EditorSceneManager.SaveScene(scene);
                        }
                    }

                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}