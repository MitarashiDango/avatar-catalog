using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarCatalogWindow : EditorWindow
    {
        private static readonly int IMAGE_SIZE = 192;
        private static readonly int ROW_SPACING = 20;
        private static readonly int MIN_COLUMN_SPACING = 20;

        private AvatarRenderer _avatarRenderer;
        private Vector2 _scrollPosition;
        private AvatarCatalog _avatarCatalog;
        private AvatarThumbnailCacheDatabase _avatarThumbnailCacheDatabase;

        [MenuItem("Tools/Avatar Catalog/Avatars List")]
        public static void ShowWindow()
        {
            var window = GetWindow<AvatarCatalogWindow>("Avatars List");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            CreateFolders();
            CreateOrLoadAssetFiles();
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
            if (!AssetDatabase.IsValidFolder("Assets/AvatarCatalog User Data"))
            {
                AssetDatabase.CreateFolder("Assets", "AvatarCatalog User Data");
            }

            if (!AssetDatabase.IsValidFolder("Assets/AvatarCatalog User Data/Cache"))
            {
                AssetDatabase.CreateFolder("Assets/AvatarCatalog User Data", "Cache");
            }

            if (!AssetDatabase.IsValidFolder("Assets/AvatarCatalog User Data/Cache/AvatarThumbnails"))
            {
                AssetDatabase.CreateFolder("Assets/AvatarCatalog User Data/Cache", "AvatarThumbnails");
            }
        }

        private void CreateOrLoadAssetFiles()
        {
            _avatarCatalog = AvatarCatalog.CreateOrLoad();
            _avatarThumbnailCacheDatabase = AvatarThumbnailCacheDatabase.CreateOrLoad();
        }

        public void LoadAssetFiles()
        {
            _avatarCatalog = AvatarCatalog.Load();
            _avatarThumbnailCacheDatabase = AvatarThumbnailCacheDatabase.Load();
        }

        private void RefreshAvatars()
        {
            // TODO 設定で調整可能にする
            // MEMO 設定の持たせ方は要検討
            var xOffset = 0f;
            var yOffset = -0.5f;
            var zOffset = 5.2f;
            var backgroundColor = Color.white;

            CreateFolders();
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

                    var cameraSetting = new AvatarRenderer.CameraSetting();
                    cameraSetting.Position = new Vector3(xOffset, avatarDescriptor.ViewPosition.y + yOffset, avatarDescriptor.ViewPosition.z + zOffset);
                    cameraSetting.Rotation = Quaternion.Euler(0, 180, 0);
                    cameraSetting.Scale = new Vector3(1, 1, 1);

                    var thumbnail = _avatarRenderer.Render(avatarObject, cameraSetting, 256, 256, null, null, false);
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

        private GameObject ChangeSelectingObject(AvatarCatalog.Avatar avatar)
        {
            if (!GlobalObjectId.TryParse(avatar.globalObjectId, out var avatarGlobalObjectId))
            {
                Debug.LogWarning("failed to try parse avatar GlobalObjectId");
                return null;
            }

            var scenePath = AssetDatabase.GetAssetPath(avatar.sceneAsset);
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.isLoaded)
            {
                if (!EditorSceneManager.SaveOpenScenes())
                {
                    Debug.Log("failed to save open scene");
                    return null;
                }

                scene = EditorSceneManager.OpenScene(scenePath);
            }

            var targetAvatarObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(avatarGlobalObjectId) as GameObject;
            if (targetAvatarObject == null)
            {
                Debug.Log("failed to find avatar object");
                return null;
            }

            var avatarObjects = scene.GetRootGameObjects()
                .Where(o => o != null && o.GetComponent<VRCAvatarDescriptor>() != null).ToList();

            foreach (var avatarObject in avatarObjects)
            {
                if (targetAvatarObject != avatarObject)
                {
                    if (avatarObject.activeSelf)
                    {
                        avatarObject.SetActive(false);
                    }
                }
                else if (!avatarObject.activeSelf)
                {
                    avatarObject.SetActive(true);
                }
            }

            return targetAvatarObject;
        }

        private void OnGUI()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Play Modeまたはビルド中は操作できません", MessageType.Info);
                return;
            }

            if (_avatarCatalog == null)
            {
                LoadAssetFiles();
            }

            if (_avatarCatalog == null)
            {
                EditorGUILayout.LabelField("初回セットアップが完了していません");

                if (GUILayout.Button("初回セットアップする"))
                {
                    OnFirstTimeSetupButton_Click();
                }

                return;
            }

            if (_avatarCatalog.avatars.Count == 0)
            {
                EditorGUILayout.LabelField("No avatars found.");

                if (GUILayout.Button("アバター再読み込み"))
                {
                    OnReloadAvatarListButton_Click();
                }

                return;
            }

            // ウィンドウ幅に基づいて列数を決定
            var innerWidth = Mathf.FloorToInt(position.width - 22);  // スクロールバーの横幅分引く
            var columns = Mathf.Max(1, (innerWidth - MIN_COLUMN_SPACING * 2) / (IMAGE_SIZE + MIN_COLUMN_SPACING));
            var totalPadding = innerWidth - (columns * IMAGE_SIZE);
            var columnSpacing = columns >= 1 ? Mathf.Max(totalPadding / (columns + 1), 0) : 0;

            var totalItems = _avatarCatalog.avatars.Count;
            var rows = Mathf.CeilToInt((float)totalItems / columns);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            for (var row = 0; row < rows; row++)
            {
                if (columns > 1)
                {
                    GUILayout.Space(ROW_SPACING);
                }
                else
                {
                    GUILayout.Space(Mathf.Min(ROW_SPACING, columnSpacing));
                }

                EditorGUILayout.BeginHorizontal(GUILayout.Width(innerWidth));

                for (var col = 0; col < columns; col++)
                {
                    var index = row * columns + col;
                    if (index >= totalItems)
                    {
                        break;
                    }

                    if (columnSpacing > 0)
                    {
                        GUILayout.Space(columnSpacing); // 列間のスペース
                    }

                    EditorGUILayout.BeginVertical(GUILayout.Width(IMAGE_SIZE));

                    var currentAvatar = _avatarCatalog.avatars[index];
                    if (!GlobalObjectId.TryParse(currentAvatar.globalObjectId, out var avatarGlobalObjectId))
                    {
                        Debug.LogWarning("failed to try parse avatar GlobalObjectId");
                        continue;
                    }

                    if (GUILayout.Button("", GUILayout.Width(IMAGE_SIZE), GUILayout.Height(IMAGE_SIZE + 20)))
                    {
                        OnAvatarItem_Click(currentAvatar);
                    }

                    var itemRect = GUILayoutUtility.GetLastRect();
                    var thumbnailTexture = _avatarThumbnailCacheDatabase.TryGetCachedAvatarThumbnailImage(avatarGlobalObjectId);
                    if (thumbnailTexture != null)
                    {
                        GUI.DrawTexture(new Rect(itemRect.x, itemRect.y, IMAGE_SIZE, IMAGE_SIZE), thumbnailTexture, ScaleMode.ScaleToFit);
                    }

                    GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true
                    };

                    GUI.Label(new Rect(itemRect.x, itemRect.y + IMAGE_SIZE, IMAGE_SIZE, 20), currentAvatar.avatarName, labelStyle);

                    EditorGUILayout.EndVertical();
                }

                if (columnSpacing > 0)
                {
                    GUILayout.Space(columnSpacing);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("アバター再読み込み"))
            {
                OnReloadAvatarListButton_Click();
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

        private void OnAvatarItem_Click(AvatarCatalog.Avatar avatar)
        {
            var avatarObject = ChangeSelectingObject(avatar);
            if (avatarObject != null)
            {
                Debug.Log("Selected: " + avatarObject.name);
            }
        }

        private void OnFirstTimeSetupButton_Click()
        {
            RefreshAvatars();
            Repaint();
        }

        private void OnReloadAvatarListButton_Click()
        {
            RefreshAvatars();
            Repaint();
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
