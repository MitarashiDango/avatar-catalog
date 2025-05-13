using System.Collections.Generic;
using System.Linq;
using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarCatalogWindow : EditorWindow
    {
        private static readonly int MinColumnSpacing = 10;
        private static readonly char[] SearchWordsDelimiterChars = { ' ' };

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
        private float _gridItemSize = Preferences.DefaultAvatarCatalogMaxItemSize;
        private AvatarSearchIndex _avatarSearchIndex = null;
        private AvatarCatalogDatabase _avatarCatalogDatabase = null;
        private Preferences _preferences;

        [MenuItem("Tools/Avatar Catalog/Avatar List")]
        public static void ShowWindow()
        {
            var window = GetWindow<AvatarCatalogWindow>("Avatar List");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            _avatarCatalogDatabase = AvatarCatalogDatabase.Load();
            _avatarSearchIndex = AvatarSearchIndex.Load();
            _preferences = Preferences.Load();

            ApplyFromPreferences();
        }

        private void OnDestroy()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
        }

        private void ApplyFromPreferences()
        {
            _gridItemSize = _preferences != null ? _preferences.avatarCatalogItemSize : Preferences.DefaultAvatarCatalogMaxItemSize;
        }

        private GameObject ChangeSelectingObject(AvatarCatalogDatabase.AvatarCatalogEntry avatar)
        {
            if (!GlobalObjectId.TryParse(avatar.avatarGlobalObjectId, out var avatarGlobalObjectId))
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

            var preferredFontFamilyName = FontCache.GetPreferredFontFamilyName();
            if (preferredFontFamilyName != "")
            {
                var fontAsset = FontCache.GetOrCreateFontAsset(preferredFontFamilyName);
                FontCache.ApplyFont(rootVisualElement, fontAsset);
            }

            _avatarCatalogView = _avatarCatalogViewAsset.CloneTree();
            root.Add(_avatarCatalogView);

            _avatarCatalogInitialSetupView = _avatarCatalogInitialSetupViewAsset.CloneTree();
            root.Add(_avatarCatalogInitialSetupView);

            SetupAvatarCatalogInitialSetupView();
            SetupAvatarCatalogView();

            // とりあえず AvatarCatalogDatabase の実体ファイルの有無で判定する
            if (AvatarCatalogDatabase.IsDatabaseFileExists())
            {
                ShowAvatarCatalogView();
            }
            else
            {
                ShowAvatarCatalogInitialSetupView();
            }

            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            var preferredFontFamilyName = FontCache.GetPreferredFontFamilyName();
            if (preferredFontFamilyName != "")
            {
                var fontAsset = FontCache.GetOrCreateFontAsset(preferredFontFamilyName);
                FontCache.ApplyFont(rootVisualElement, fontAsset);
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
            button.RegisterCallback<ClickEvent>(e => OnRunInitialSetupButtonClick());
        }

        private void SetupAvatarCatalogView()
        {
            var root = _avatarCatalogView;

            var avatarCatalogHeader = root.Q<Toolbar>("avatar-catalog-header");
            var avatarCatalogFooter = root.Q<VisualElement>("avatar-catalog-footer");

            var searchTextField = avatarCatalogHeader.Q<ToolbarSearchField>("search-text-field");
            searchTextField.value = _searchText;
            searchTextField.RegisterValueChangedCallback(e => OnSearchTextFieldValueChanged(e));

            var reloadAvatarCatalogMenu = avatarCatalogHeader.Q<ToolbarMenu>("reload-avatar-catalog-menu");
            reloadAvatarCatalogMenu.menu.AppendAction("アバター一覧を更新", action => ReloadAvatarList());
            reloadAvatarCatalogMenu.menu.AppendAction("アバター一覧を更新（サムネイル画像再生成あり）", action => ReloadAvatarList(true));

            var resizeGridItemSlider = avatarCatalogFooter.Q<Slider>("resize-grid-item-slider");
            resizeGridItemSlider.value = _gridItemSize;
            resizeGridItemSlider.RegisterValueChangedCallback((e) => OnResizeGridItemSliderValueChanged(e));
            resizeGridItemSlider.RegisterCallback<PointerCaptureOutEvent>(e => OnResizeGridItemSliderPointerCaptureOut(e));

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

            if (_avatarCatalogDatabase == null)
            {
                return;
            }

            var avatars = _avatarCatalogDatabase.avatars;
            var scrollViewWidth = rootVisualElement.contentRect.width;
            if (avatars.Count == 0 || float.IsNaN(scrollViewWidth) || scrollViewWidth <= 0)
            {
                return;
            }

            avatars = FilterAvatars(avatars, _searchText);
            if (avatars.Count == 0)
            {
                return;
            }

            var maxColumns = Mathf.Max(1, Mathf.FloorToInt((scrollViewWidth - MinColumnSpacing) / (_gridItemSize + MinColumnSpacing)));
            var rows = Mathf.CeilToInt((float)avatars.Count / maxColumns);

            var totalRowWidth = maxColumns * _gridItemSize;
            var spaceBetween = (scrollViewWidth - totalRowWidth) / (maxColumns + 1);

            for (var row = 0; row < rows; row++)
            {
                var startIndex = row * maxColumns;
                var endIndex = Mathf.Min(startIndex + maxColumns, avatars.Count);
                var itemCountInRow = endIndex - startIndex;

                var rowContainer = _gridLayoutListRowContainerAsset.CloneTree();
                rowContainer.style.width = scrollViewWidth - (MinColumnSpacing * 2) - spaceBetween;
                rowContainer.style.justifyContent = maxColumns > 1 ? Justify.SpaceBetween : Justify.Center;

                for (var i = startIndex; i < endIndex; i++)
                {
                    var currentAvatar = avatars[i];

                    var gridLayoutItem = _avatarCatalogGridLayoutListItemAsset.CloneTree();
                    gridLayoutItem.style.height = _gridItemSize;
                    gridLayoutItem.style.width = _gridItemSize;

                    var avatarThumbnailImage = gridLayoutItem.Q<Image>("avatar-thumbnail-image");
                    avatarThumbnailImage.image = LoadAvatarThumbnailImage(currentAvatar);

                    avatarThumbnailImage.RegisterCallback<GeometryChangedEvent>(e =>
                    {
                        avatarThumbnailImage.style.width = e.newRect.height;
                    });

                    var avatarNameLabel = gridLayoutItem.Q<Label>("avatar-name-label");
                    avatarNameLabel.text = currentAvatar.avatarObjectName;

                    gridLayoutItem.RegisterCallback<ClickEvent>((e) => OnAvatarItemClick(e, currentAvatar));

                    var manipulator = GetAvatarCatalogItemContextualMenu(currentAvatar);
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
                        dummyItem.style.width = _gridItemSize;
                        dummyItem.style.height = _gridItemSize;
                        rowContainer.Add(dummyItem);
                    }
                }

                gridContainer.Add(rowContainer);
            }
        }

        private Texture2D LoadAvatarThumbnailImage(AvatarCatalogDatabase.AvatarCatalogEntry entry)
        {
            if (string.IsNullOrEmpty(entry.thumbnailImageGuid))
            {
                return null;
            }

            if (GUID.TryParse(entry.thumbnailImageGuid, out var thumbnailImageGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(thumbnailImageGuid);
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }

                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            return null;
        }

        private List<AvatarCatalogDatabase.AvatarCatalogEntry> FilterAvatars(List<AvatarCatalogDatabase.AvatarCatalogEntry> avatars, string searchText)
        {
            var result = avatars;

            if (searchText.Length > 0)
            {
                var searchWords = searchText.ToLower().Split(SearchWordsDelimiterChars);
                if (_avatarSearchIndex == null)
                {
                    _avatarSearchIndex = AvatarSearchIndex.LoadOrCreateFile();
                }

                var avatarGlobalObjectIds = _avatarSearchIndex.GetGlobalObjectIds(searchWords);
                if (avatarGlobalObjectIds.Count == 0)
                {
                    return new List<AvatarCatalogDatabase.AvatarCatalogEntry>();
                }

                result = result.Where(avatar => avatarGlobalObjectIds.Exists(id => id == avatar.avatarGlobalObjectId)).ToList();
            }

            return result;
        }

        private ContextualMenuManipulator GetAvatarCatalogItemContextualMenu(AvatarCatalogDatabase.AvatarCatalogEntry avatar)
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

                e.menu.AppendAction("Add avatar thumbnail settings component", action =>
                {
                    AddAvatarCatalogThumbnailSettingsComponent(avatar);
                });

                e.menu.AppendAction("Edit Avatar Metadata", action =>
                {
                    ShowAvatarMetadataEditor(avatar);
                });
            });

            return manipulator;
        }

        private void ShowAvatarMetadataEditor(AvatarCatalogDatabase.AvatarCatalogEntry avatar)
        {
            var currentScenePath = SceneManager.GetActiveScene().path;
            var scenePath = AssetDatabase.GetAssetPath(avatar.sceneAsset);
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.isLoaded)
            {
                if (!EditorUtility.DisplayDialog("シーン切り替え確認", $"アバター '{avatar.avatarObjectName}' のメタデータを編集するため、シーンを切り替えます。\nよろしいですか？", "はい", "いいえ"))
                {
                    return;
                }

                if (!EditorSceneManager.SaveOpenScenes())
                {
                    Debug.Log("failed to save open scene");
                    return;
                }

                EditorSceneManager.OpenScene(scenePath);
            }

            if (!GlobalObjectId.TryParse(avatar.avatarGlobalObjectId, out var avatarGlobalObjectId))
            {
                EditorSceneManager.OpenScene(currentScenePath);
                Debug.LogWarning("Failed to parse GlobalObjectId");
                return;
            }

            var avatarObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(avatarGlobalObjectId) as GameObject;
            if (avatarObject == null)
            {
                EditorSceneManager.OpenScene(currentScenePath);
                EditorUtility.DisplayDialog("エラー", $"アバター '{avatar.avatarObjectName}' が見つかりませんでした。", "OK");
                Debug.Log("failed to find avatar object");
                return;
            }

            AvatarMetadataEditorWindow.ShowWindow(avatarObject);
        }

        private void BuildAvatarCatalogs(bool withRefreshThumbnails = false)
        {
            DatabaseBuilder.BuildAvatarCatalogDatabaseAndIndexes(withRefreshThumbnails);

            _avatarCatalogDatabase = AvatarCatalogDatabase.Load();
            _avatarSearchIndex = AvatarSearchIndex.Load();
        }

        private void ReloadAvatarList(bool withRefreshThumbnails = false)
        {
            BuildAvatarCatalogs(withRefreshThumbnails);
            UpdateGridLayout();
        }

        private void ChangeToActiveAvatar(AvatarCatalogDatabase.AvatarCatalogEntry avatar)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("情報", "Play Mode実行中はアバターの切り替えは行えません", "OK");
                return;
            }

            var avatarObject = ChangeSelectingObject(avatar);
            if (avatarObject != null)
            {
                EditorGUIUtility.PingObject(avatarObject);
                Debug.Log("Selected: " + avatarObject.name);
            }
        }

        private void UpdateAvatarThumbnail(AvatarCatalogDatabase.AvatarCatalogEntry avatar)
        {
            var scenePath = AssetDatabase.GetAssetPath(avatar.sceneAsset);
            var scene = SceneManager.GetSceneByPath(scenePath);

            using var avatarRenderer = new AvatarRenderer();

            try
            {
                if (!scene.isLoaded)
                {
                    if (!EditorSceneManager.SaveOpenScenes())
                    {
                        Debug.LogError("failed to save open scene");
                        return;
                    }

                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }

                if (!GlobalObjectId.TryParse(avatar.avatarGlobalObjectId, out var avatarGlobalObjectId))
                {
                    Debug.LogWarning("Failed to parse GlobalObjectId");
                    return;
                }

                var targetAvatarObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(avatarGlobalObjectId) as GameObject;
                if (targetAvatarObject == null)
                {
                    Debug.LogWarning("failed to find avatar object");
                    return;
                }

                var avatarDescriptor = targetAvatarObject.GetComponent<VRCAvatarDescriptor>();
                if (avatarDescriptor == null)
                {
                    Debug.LogWarning("failed to find VRCAvatarDescriptor component");
                    return;
                }

                var avatarCatalogDatabaseEntry = _avatarCatalogDatabase.Get(avatar.avatarGlobalObjectId);
                if (avatarCatalogDatabaseEntry != null)
                {
                    var thumbnail = AvatarThumbnailUtil.RenderAvatarThumbnail(avatarRenderer, targetAvatarObject);
                    if (avatarCatalogDatabaseEntry.thumbnailImageGuid != "" && GUID.TryParse(avatarCatalogDatabaseEntry.thumbnailImageGuid, out var thumbnailImageGuid))
                    {
                        // 古いサムネイル画像を削除
                        AvatarThumbnailUtil.DeleteAvatarThumbnailImage(thumbnailImageGuid);
                        AssetDatabase.Refresh();
                    }

                    // 再生成したサムネイル画像を保存
                    avatarCatalogDatabaseEntry.thumbnailImageGuid = AvatarThumbnailUtil.StoreAvatarThumbnailImage(thumbnail, targetAvatarObject).ToString();
                }

                AvatarCatalogDatabase.Save(_avatarCatalogDatabase);
                AssetDatabase.Refresh();
            }
            finally
            {
                if (scene != EditorSceneManager.GetActiveScene())
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private void AddAvatarCatalogThumbnailSettingsComponent(AvatarCatalogDatabase.AvatarCatalogEntry avatar)
        {
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

                if (!GlobalObjectId.TryParse(avatar.avatarGlobalObjectId, out var avatarGlobalObjectId))
                {
                    Debug.LogWarning("Failed to parse GlobalObjectId");
                    return;
                }

                var avatarObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(avatarGlobalObjectId) as GameObject;
                if (avatarObject == null)
                {
                    Debug.Log("failed to find avatar object");
                    return;
                }

                var component = avatarObject.GetComponent<AvatarThumbnailSettings>();
                if (component != null)
                {
                    return;
                }

                avatarObject.AddComponent<AvatarThumbnailSettings>();
                EditorUtility.SetDirty(avatarObject);

                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (scene != EditorSceneManager.GetActiveScene())
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        public void ReloadAvatars()
        {
            Preferences.Load();
            _avatarCatalogDatabase = AvatarCatalogDatabase.Load();
            _avatarSearchIndex = AvatarSearchIndex.Load();

            UpdateGridLayout();

            // とりあえず AvatarCatalogDatabase の実体ファイルの有無で判定する
            if (AvatarCatalogDatabase.IsDatabaseFileExists())
            {
                ShowAvatarCatalogView();
            }
            else
            {
                ShowAvatarCatalogInitialSetupView();
            }
        }

        private void OnSearchTextFieldValueChanged(ChangeEvent<string> e)
        {
            _searchText = e.newValue;
            UpdateGridLayout();
        }

        private void OnAvatarItemClick(ClickEvent e, AvatarCatalogDatabase.AvatarCatalogEntry avatar)
        {
            if (e.button == (int)MouseButton.LeftMouse && e.clickCount == 2)
            {
                ChangeToActiveAvatar(avatar);
            }
        }

        private void OnRunInitialSetupButtonClick()
        {
            BuildAvatarCatalogs();
            UpdateGridLayout();

            // とりあえず AvatarCatalogDatabase の実体ファイルの有無で判定する
            if (AvatarCatalogDatabase.IsDatabaseFileExists())
            {
                ShowAvatarCatalogView();
            }
            else
            {
                ShowAvatarCatalogInitialSetupView();
            }
        }

        private void OnResizeGridItemSliderValueChanged(ChangeEvent<float> e)
        {
            _gridItemSize = e.newValue;
            UpdateGridLayout();
        }

        private void OnResizeGridItemSliderPointerCaptureOut(PointerCaptureOutEvent e)
        {
            if (_preferences == null)
            {
                _preferences = Preferences.LoadOrCreateFile();
            }

            _preferences.avatarCatalogItemSize = _gridItemSize;
            Preferences.Save(_preferences);
            AssetDatabase.Refresh();
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
