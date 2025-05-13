using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

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
    }
}
