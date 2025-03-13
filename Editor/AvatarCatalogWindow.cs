using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarCatalogWindow : EditorWindow
    {
        private AvatarRenderer _avatarRenderer;
        private int _imageSize = 192;
        private int _padding = 10;
        private int _columns = 3;
        private Vector2 _scrollPosition;
        private AvatarCatalog _avatarCatalog;
        private AvatarThumbnailCacheDatabase _avatarThumbnailCacheDatabase;

        [MenuItem("Tools/Avatar Catalog/Avatar List")]
        public static void ShowWindow()
        {
            var window = GetWindow<AvatarCatalogWindow>("Avatar List");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            CreateFolders();
            CreateOrLoadAssetFiles();

            // RefreshAvatars();
        }

        private void OnDisable()
        {
            _avatarRenderer?.Dispose();
            _avatarRenderer = null;
        }

        private void InitializeAvatarRenderer()
        {
            if (_avatarRenderer != null)
            {
                return;
            }

            _avatarRenderer = new AvatarRenderer();
        }

        private void CreateFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/AvatarCatalog"))
            {
                AssetDatabase.CreateFolder("Assets", "AvatarCatalog");
            }

            if (!AssetDatabase.IsValidFolder("Assets/AvatarCatalog/Cache"))
            {
                AssetDatabase.CreateFolder("Assets/AvatarCatalog", "Cache");
            }

            if (!AssetDatabase.IsValidFolder("Assets/AvatarCatalog/Cache/AvatarThumbnail"))
            {
                AssetDatabase.CreateFolder("Assets/AvatarCatalog/Cache", "AvatarThumbnail");
            }
        }

        private void CreateOrLoadAssetFiles()
        {
            _avatarCatalog = AvatarCatalog.CreateOrLoad();
            _avatarThumbnailCacheDatabase = AvatarThumbnailCacheDatabase.CreateOrLoad();
        }

        private void RefreshAvatars()
        {
            // TODO 設定で調整可能にする
            // MEMO 設定の持たせ方は要検討
            var xOffset = 0f;
            var yOffset = -0.5f;
            var zOffset = 5.2f;
            var backgroundColor = Color.white;

            CreateOrLoadAssetFiles();
            InitializeAvatarRenderer();

            _avatarCatalog.Clear();

            var scenes = GetAllScenes();
            for (var i = 0; i < scenes.Count; i++)
            {
                var scenePath = AssetDatabase.GetAssetPath(scenes[i]);
                var currentScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                var currentSceneRootObjects = currentScene.GetRootGameObjects();
                var avatarObjects = currentSceneRootObjects.Where(o => o != null && o.GetComponent<VRCAvatarDescriptor>() != null);

                foreach (var avatarObject in avatarObjects)
                {
                    var avatarDescriptor = avatarObject.GetComponent<VRCAvatarDescriptor>();
                    var avatar = new AvatarCatalog.Avatar(scenes[i], avatarObject);
                    _avatarCatalog.AddAvatar(avatar);
                    EditorUtility.SetDirty(_avatarCatalog);

                    if (!GlobalObjectId.TryParse(avatar.globalObjectId, out var avatarGlobalObjectId))
                    {
                        Debug.LogWarning("Failed to parse GlobalObjectId");
                        continue;
                    }

                    if (_avatarThumbnailCacheDatabase.IsExists(avatarGlobalObjectId))
                    {
                        continue;
                    }

                    var cameraPosition = new Vector3(xOffset, avatarDescriptor.ViewPosition.y + yOffset, avatarDescriptor.ViewPosition.z + zOffset);

                    _avatarRenderer.cameraPosition = cameraPosition;
                    _avatarRenderer.cameraRotation = Quaternion.Euler(0, 180, 0);
                    _avatarRenderer.cameraScale = new Vector3(1, 1, 1);


                    // TODO 画像をキャッシュするようにする
                    var thumbnail = _avatarRenderer.Render(avatarObject, 256, 256, null, null, false);
                    thumbnail = _avatarThumbnailCacheDatabase.StoreAvatarThumbnailImage(avatarGlobalObjectId, thumbnail);
                    EditorUtility.SetDirty(_avatarThumbnailCacheDatabase);
                }

                if (currentScene != EditorSceneManager.GetActiveScene())
                {
                    EditorSceneManager.CloseScene(currentScene, true);
                }
            }

            AssetDatabase.SaveAssets();

            CreateOrLoadAssetFiles();
        }

        private void ChangeSelectingObject(GameObject obj)
        {
            if (obj.scene.isLoaded)
            {
                var gameObjects = EditorSceneManager.GetActiveScene().GetRootGameObjects()
                    .Where(o => o != null && o.GetComponent<VRCAvatarDescriptor>() != null).ToList();

                foreach (var currentGameObject in gameObjects)
                {
                    if (obj != currentGameObject)
                    {
                        if (currentGameObject.activeSelf)
                        {
                            currentGameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        if (!currentGameObject.activeSelf)
                        {
                            currentGameObject.SetActive(true);
                        }
                    }
                }
            }
            else
            {
                var gid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
                var path = AssetDatabase.GUIDToAssetPath(gid.assetGUID.ToString());
                EditorSceneManager.OpenScene(path);
            }
        }

        private void OnGUI()
        {
            if (_avatarCatalog == null)
            {
                CreateOrLoadAssetFiles();
            }

            if (_avatarCatalog.avatars.Count == 0)
            {
                EditorGUILayout.LabelField("No avatars found.");

                if (GUILayout.Button("Reload Avatars"))
                {
                    RefreshAvatars();
                    Repaint();
                }

                return;
            }

            // ウィンドウ幅に基づいて列数を決定（最大サイズ192x192）
            _columns = Mathf.Max(1, (int)(position.width / (_imageSize + _padding)));
            _imageSize = Mathf.Min(192, (int)(position.width / _columns) - _padding); // サイズ制限

            var rows = Mathf.CeilToInt((float)_avatarCatalog.avatars.Count / _columns);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (var row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal();

                for (var col = 0; col < _columns; col++)
                {
                    var index = row * _columns + col;
                    if (index >= _avatarCatalog.avatars.Count)
                    {
                        break;
                    }

                    var currentAvatar = _avatarCatalog.avatars[index];
                    if (!GlobalObjectId.TryParse(currentAvatar.globalObjectId, out var avatarGlobalObjectId))
                    {
                        Debug.LogWarning("failed to try parse avatar GlobalObjectId");
                        continue;
                    }

                    // 画像＋テキストを1つのボタンとしてラップ
                    if (GUILayout.Button("", GUILayout.Width(_imageSize), GUILayout.Height(_imageSize + 20)))
                    {
                        var scenePath = AssetDatabase.GetAssetPath(currentAvatar.sceneAsset);
                        Scene scene = SceneManager.GetSceneByPath(scenePath);
                        if (!scene.isLoaded)
                        {
                            scene = EditorSceneManager.OpenScene(scenePath);
                        }

                        var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(avatarGlobalObjectId) as GameObject;

                        Debug.Log("Selected: " + obj.name);
                        ChangeSelectingObject(obj);
                    }

                    Rect lastRect = GUILayoutUtility.GetLastRect();
                    var thumbnailTexture = _avatarThumbnailCacheDatabase.TryGetCachedAvatarThumbnailImage(avatarGlobalObjectId);
                    if (thumbnailTexture != null)
                    {
                        GUI.DrawTexture(new Rect(lastRect.x, lastRect.y, _imageSize, _imageSize), thumbnailTexture, ScaleMode.ScaleToFit);
                    }

                    GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true
                    };

                    GUI.Label(new Rect(lastRect.x, lastRect.y + _imageSize, _imageSize, 20), currentAvatar.avatarName, labelStyle);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Reload Avatars"))
            {
                RefreshAvatars();
                Repaint();
            }
        }

        private List<SceneAsset> GetAllScenes()
        {
            return AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets" })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
                .Where(asset => asset != null)
                .ToList();
        }

        internal class AvatarListItem
        {
            public string avatarName { get; set; }
            public SceneAsset scene { get; set; }
            public GlobalObjectId avatarGlobalObjectId { get; set; }
            public string thumbnailGlobalObjectId { get; set; }

            public AvatarListItem(SceneAsset sceneAsset, GameObject avatar)
            {
                scene = sceneAsset;
                avatarName = avatar.name;
                avatarGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(avatar);
            }
        }
    }
}
