using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarCatalogWindow : EditorWindow
    {
        // TODO 設定で調整可能にする
        // MEMO 設定の持たせ方は要検討
        private static readonly float X_OFFSET = 0f;
        private static readonly float Y_OFFSET = 0f;
        private static readonly float Z_OFFSET = 0f;
        private static readonly Color BACKGROUND_COLOR = Color.clear;

        private static readonly int THUMBNAIL_IMAGE_SIZE = 512;
        private static readonly int GRID_ITEM_SIZE = 160;   // 128 ～ 256
        private static readonly int MIN_COLUMN_SPACING = 10;
        private static readonly char[] SEARCH_WORDS_DELIMITER_CHARS = { ' ' };

        private AvatarRenderer _avatarRenderer;
        private AvatarCatalog _avatarCatalog;
        private AvatarThumbnailCacheDatabase _avatarThumbnailCacheDatabase;

        private VisualElement _avatarCatalogView;
        private VisualElement _avatarCatalogInitialSetupView;

        [SerializeField]
        private VisualTreeAsset _avatarCatalogViewAsset;

        [SerializeField]
        private VisualTreeAsset _avatarCatalogInitialSetupViewAsset;

        [SerializeField]
        private VisualTreeAsset _avatarCatalogGridLayoutListItemAsset;

        [SerializeField]
        private VisualTreeAsset _gridLayoutListRowContainerAsset;

        private string _searchText = "";

        [MenuItem("Tools/AvatarCatalog/Avatar List")]
        public static void ShowWindow()
        {
            var window = GetWindow<AvatarCatalogWindow>("Avatar List");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
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
            DirectoryUtil.CreateUserDataFolder();
            DirectoryUtil.CreateCacheFolder();
            DirectoryUtil.CreateAvatarThumbnailsCacheFolder();
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

                    var thumbnail = _avatarRenderer.Render(avatarObject, GetCameraSetting(), THUMBNAIL_IMAGE_SIZE, THUMBNAIL_IMAGE_SIZE, null, null, false);
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
            var scene = SceneManager.GetSceneByPath(scenePath);
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

                    continue;
                }

                if (!avatarObject.activeSelf)
                {
                    avatarObject.SetActive(true);
                }

                Selection.activeGameObject = avatarObject;
            }

            return targetAvatarObject;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();

            _avatarCatalogView = _avatarCatalogViewAsset.CloneTree();
            root.Add(_avatarCatalogView);

            _avatarCatalogInitialSetupView = _avatarCatalogInitialSetupViewAsset.CloneTree();
            root.Add(_avatarCatalogInitialSetupView);

            SetupAvatarCatalogInitialSetupView();
            SetupAvatarCatalogView();

            if (_avatarCatalog != null)
            {
                ShowAvatarCatalogView();
            }
            else
            {
                ShowAvatarCatalogInitialSetupView();
            }
        }

        private void ShowAvatarCatalogInitialSetupView()
        {
            if (_avatarCatalogView == null && _avatarCatalogInitialSetupView == null)
            {
                return;
            }

            _avatarCatalogView.style.display = DisplayStyle.None;
            _avatarCatalogInitialSetupView.style.display = DisplayStyle.Flex;
        }

        private void SetupAvatarCatalogInitialSetupView()
        {
            var root = _avatarCatalogInitialSetupView;
            var button = root.Q<Button>("run-initial-setup-button");
            button.RegisterCallback<ClickEvent>(e => OnRunInitialSetupButton_Click());
        }

        private void SetupAvatarCatalogView()
        {
            var root = _avatarCatalogView;

            if (_avatarCatalog == null)
            {
                LoadAssetFiles();
            }

            var avatarCatalogHeader = root.Q<VisualElement>("avatar-catalog-header");

            var searchIcon = avatarCatalogHeader.Q<Image>("search-icon");
            searchIcon.image = EditorGUIUtility.IconContent("Search On Icon").image;

            var searchTextField = avatarCatalogHeader.Q<TextField>("search-text-field");
            searchTextField.value = _searchText;
            searchTextField.RegisterValueChangedCallback(evt =>
            {
                _searchText = evt.newValue;
                UpdateGridLayout();
            });

            var reloadAvatarCatalogButton = avatarCatalogHeader.Q<Button>("reload-avatar-catalog-button");
            reloadAvatarCatalogButton.RegisterCallback<ClickEvent>((e) => OnReloadAvatarListButton_Click());
            reloadAvatarCatalogButton.tooltip = "アバター一覧を更新";

            var reloadAvatarCatalogButtonIcon = avatarCatalogHeader.Q<Image>("reload-avatar-catalog-button-icon");
            reloadAvatarCatalogButtonIcon.image = EditorGUIUtility.IconContent("Refresh").image;

            root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void ShowAvatarCatalogView()
        {
            if (_avatarCatalogView == null && _avatarCatalogInitialSetupView == null)
            {
                return;
            }

            _avatarCatalogView.style.display = DisplayStyle.Flex;
            _avatarCatalogInitialSetupView.style.display = DisplayStyle.None;
        }

        private void UpdateGridLayout()
        {
            var gridContainer = _avatarCatalogView.Q<VisualElement>("avatar-catalog-grid-view").Q<VisualElement>("items-container");

            gridContainer.Clear();

            var scrollViewWidth = rootVisualElement.contentRect.width;
            if (_avatarCatalog == null || float.IsNaN(scrollViewWidth) || scrollViewWidth <= 0)
            {
                return;
            }

            var avatars = _avatarCatalog.avatars;
            if (_searchText.Length > 0)
            {
                var searchWords = _searchText.ToLower().Split(SEARCH_WORDS_DELIMITER_CHARS);
                avatars = avatars.Where(avatar => searchWords.All(word => avatar.avatarName.ToLower().IndexOf(word) != -1)).ToList();
            }

            var totalItems = avatars.Count;
            if (totalItems == 0)
            {
                return;
            }

            var maxColumns = Mathf.Max(1, Mathf.FloorToInt((scrollViewWidth - MIN_COLUMN_SPACING) / (GRID_ITEM_SIZE + MIN_COLUMN_SPACING)));
            var rows = Mathf.CeilToInt((float)totalItems / maxColumns);

            var totalRowWidth = maxColumns * GRID_ITEM_SIZE;
            var spaceBetween = (scrollViewWidth - totalRowWidth) / (maxColumns + 1);

            for (var row = 0; row < rows; row++)
            {
                var startIndex = row * maxColumns;
                var endIndex = Mathf.Min(startIndex + maxColumns, totalItems);
                var itemCountInRow = endIndex - startIndex;

                var rowContainer = _gridLayoutListRowContainerAsset.CloneTree();
                rowContainer.style.width = scrollViewWidth - (MIN_COLUMN_SPACING * 2) - spaceBetween;
                rowContainer.style.justifyContent = maxColumns > 1 ? Justify.SpaceBetween : Justify.Center;

                for (var i = startIndex; i < endIndex; i++)
                {
                    var currentAvatar = avatars[i];
                    if (!GlobalObjectId.TryParse(currentAvatar.globalObjectId, out var avatarGlobalObjectId))
                    {
                        Debug.LogWarning("failed to try parse avatar GlobalObjectId");
                        continue;
                    }

                    var gridLayoutItem = _avatarCatalogGridLayoutListItemAsset.CloneTree();
                    gridLayoutItem.style.height = GRID_ITEM_SIZE;
                    gridLayoutItem.style.width = GRID_ITEM_SIZE;

                    var avatarThumbnailImage = gridLayoutItem.Q<Image>("avatar-thumbnail-image");
                    var thumbnailTexture = _avatarThumbnailCacheDatabase.TryGetCachedAvatarThumbnailImage(avatarGlobalObjectId);
                    if (thumbnailTexture != null)
                    {
                        avatarThumbnailImage.image = thumbnailTexture;
                    }

                    avatarThumbnailImage.RegisterCallback<GeometryChangedEvent>(e =>
                    {
                        avatarThumbnailImage.style.width = e.newRect.height;
                    });

                    var avatarNameLabel = gridLayoutItem.Q<Label>("avatar-name-label");
                    avatarNameLabel.text = currentAvatar.avatarName;

                    gridLayoutItem.RegisterCallback<ClickEvent>((e) => OnAvatarItem_Click(e, currentAvatar));

                    var manipulator = getAvatarCatalogItemContextualMenu(currentAvatar);
                    manipulator.target = gridLayoutItem;

                    rowContainer.Add(gridLayoutItem);
                }

                // ダミー要素を追加
                if (row == rows - 1)
                {
                    var emptySlots = maxColumns - itemCountInRow;
                    for (var j = 0; j < emptySlots; j++)
                    {
                        var dummyItem = new VisualElement();
                        dummyItem.style.width = GRID_ITEM_SIZE;
                        dummyItem.style.height = GRID_ITEM_SIZE;
                        rowContainer.Add(dummyItem);
                    }
                }

                gridContainer.Add(rowContainer);
            }
        }

        private ContextualMenuManipulator getAvatarCatalogItemContextualMenu(AvatarCatalog.Avatar avatar)
        {
            var manipulator = new ContextualMenuManipulator(e =>
            {
                e.menu.AppendAction("Switch to active", action =>
                {
                    ChangeToActiveAvatar(avatar);
                });

                e.menu.AppendAction("Update avatar thumbnail image", action =>
                {
                    UpdateAvatarThumbnail(avatar);
                    ReloadAvatars();
                });
            });

            return manipulator;
        }

        private List<SceneAsset> GetAllScenes()
        {
            return AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets" })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
                .Where(asset => asset != null)
                .ToList();
        }

        private void OnAvatarItem_Click(ClickEvent e, AvatarCatalog.Avatar avatar)
        {
            if (e.button == (int)MouseButton.LeftMouse && e.clickCount == 2)
            {
                ChangeToActiveAvatar(avatar);
            }
        }

        private void ChangeToActiveAvatar(AvatarCatalog.Avatar avatar)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("情報", "Play Mode実行中はアバターの切り替えは行えません", "OK");
                return;
            }

            var avatarObject = ChangeSelectingObject(avatar);
            if (avatarObject != null)
            {
                Debug.Log("Selected: " + avatarObject.name);
            }
        }

        private void UpdateAvatarThumbnail(AvatarCatalog.Avatar avatar)
        {
            InitializeAvatarRenderer();

            var scenePath = AssetDatabase.GetAssetPath(avatar.sceneAsset);
            var scene = SceneManager.GetSceneByPath(scenePath);
            try
            {
                if (!scene.isLoaded)
                {
                    if (!EditorSceneManager.SaveOpenScenes())
                    {
                        Debug.Log("failed to save open scene");
                        return;
                    }

                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }

                if (!GlobalObjectId.TryParse(avatar.globalObjectId, out var avatarGlobalObjectId))
                {
                    Debug.LogWarning("Failed to parse GlobalObjectId");
                    return;
                }

                var targetAvatarObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(avatarGlobalObjectId) as GameObject;
                if (targetAvatarObject == null)
                {
                    Debug.Log("failed to find avatar object");
                    return;
                }

                var avatarDescriptor = targetAvatarObject.GetComponent<VRCAvatarDescriptor>();
                if (avatarDescriptor == null)
                {
                    Debug.Log("failed to find VRCAvatarDescriptor component");
                    return;
                }

                var thumbnail = _avatarRenderer.Render(targetAvatarObject, GetCameraSetting(), THUMBNAIL_IMAGE_SIZE, THUMBNAIL_IMAGE_SIZE, null, null, false);
                _avatarThumbnailCacheDatabase.StoreAvatarThumbnailImage(avatarGlobalObjectId, thumbnail);
                _avatarThumbnailCacheDatabase.Save();
            }
            finally
            {
                if (scene != EditorSceneManager.GetActiveScene())
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private AvatarRenderer.CameraSetting GetCameraSetting()
        {
            var cameraSetting = new AvatarRenderer.CameraSetting();
            cameraSetting.BackgroundColor = BACKGROUND_COLOR;
            cameraSetting.PositionOffset = new Vector3(X_OFFSET, Y_OFFSET, Z_OFFSET);
            cameraSetting.Rotation = Quaternion.Euler(0, 180, 0);
            cameraSetting.Scale = new Vector3(1, 1, 1);

            return cameraSetting;
        }

        public void ReloadAvatars()
        {
            LoadAssetFiles();
            UpdateGridLayout();

            if (_avatarCatalog != null)
            {
                ShowAvatarCatalogView();
            }
            else
            {
                ShowAvatarCatalogInitialSetupView();
            }
        }

        private void OnRunInitialSetupButton_Click()
        {
            RefreshAvatars();
            UpdateGridLayout();

            if (_avatarCatalog != null)
            {
                ShowAvatarCatalogView();
            }
            else
            {
                ShowAvatarCatalogInitialSetupView();
            }
        }

        private void OnReloadAvatarListButton_Click()
        {
            RefreshAvatars();
            UpdateGridLayout();
        }

        private void OnGeometryChanged(GeometryChangedEvent e)
        {
            UpdateGridLayout();
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
