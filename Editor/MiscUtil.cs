using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    /// <summary>
    /// ごった煮
    /// </summary>
    public class MiscUtil
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
                var scenePath = AssetDatabase.GetAssetPath(sceneAsset);
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                walkAction(sceneAsset, scene);

                if (scene != EditorSceneManager.GetActiveScene())
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        public static VisualTreeAsset LoadVisualTreeAsset(string guid)
        {
            if (GUID.TryParse(guid, out var parsedGuid))
            {
                return LoadVisualTreeAsset(parsedGuid);
            }

            return null;
        }

        public static VisualTreeAsset LoadVisualTreeAsset(GUID guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        }
    }
}
