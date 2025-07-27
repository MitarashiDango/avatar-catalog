using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor.Api;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarCatalogWindow : EditorWindow
    {
        private static readonly string _mainUxmlGuid = "486221a5f5fdd0b4cbc5ccc22397d354";
        private static readonly string _gridLayoutListItemUxmlGuid = "74e74187aebb7f6469bfc215a2ec332d";
        private static readonly string _gridLayoutListRowContainerUxmlGuid = "41211d8814f507c4bae94f406711e600";

        private static readonly int MinColumnSpacing = 10;
        private static readonly char[] SearchWordsDelimiterChars = { ' ' };

        private VisualElement _avatarListView;
        private VisualElement _initialSetupView;

        private VisualTreeAsset _gridLayoutListItemAsset;
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
                Debug.LogError("failed to try parse avatar GlobalObjectId");
                return null;
            }

            var scenePath = AssetDatabase.GetAssetPath(avatar.sceneAsset);
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.isLoaded)
            {
                if (!EditorSceneManager.SaveOpenScenes())
                {
                    Debug.LogError("failed to save open scene");
                    return null;
                }

                scene = EditorSceneManager.OpenScene(scenePath);
            }

            var targetAvatarObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(avatarGlobalObjectId) as GameObject;
            if (targetAvatarObject == null)
            {
                Debug.LogError("failed to find avatar object");
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

            if (!LoadUxmlAssets())
            {
                return;
            }

            _avatarListView = root.Q<VisualElement>("avatar-list-view");
            _initialSetupView = root.Q<VisualElement>("initial-setup-view");

            SetupInitialSetupView();
            SetupAvatarListView();

            UpdateViews();

            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private bool LoadUxmlAssets()
        {
            var mainUxmlAsset = MiscUtil.LoadVisualTreeAsset(_mainUxmlGuid);
            if (mainUxmlAsset == null)
            {
                Debug.LogError($"Cannot load UXML file: {_mainUxmlGuid}");
                return false;
            }

            _gridLayoutListItemAsset = MiscUtil.LoadVisualTreeAsset(_gridLayoutListItemUxmlGuid);
            if (_gridLayoutListItemAsset == null)
            {
                Debug.LogError($"Cannot load UXML file: {_gridLayoutListItemUxmlGuid}");
                return false;
            }

            _gridLayoutListRowContainerAsset = MiscUtil.LoadVisualTreeAsset(_gridLayoutListRowContainerUxmlGuid);
            if (_gridLayoutListRowContainerAsset == null)
            {
                Debug.LogError($"Cannot load UXML file: {_gridLayoutListRowContainerUxmlGuid}");
                return false;
            }

            mainUxmlAsset.CloneTree(rootVisualElement);

            return true;
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

        private void ShowInitialSetupView()
        {
            if (_avatarListView == null && _initialSetupView == null)
            {
                return;
            }

            _avatarListView.style.display = DisplayStyle.None;
            _initialSetupView.style.display = DisplayStyle.Flex;
        }

        private void SetupInitialSetupView()
        {
            var root = _initialSetupView;
            var button = root.Q<Button>("run-initial-setup-button");
            button.RegisterCallback<ClickEvent>(e => OnRunInitialSetupButtonClick());
        }

        private void SetupAvatarListView()
        {
            var root = _avatarListView;

            var header = root.Q<Toolbar>("avatar-list-view-header");
            var footer = root.Q<VisualElement>("avatar-list-view-footer");

            var searchTextField = header.Q<ToolbarSearchField>("search-text-field");
            searchTextField.value = _searchText;
            searchTextField.RegisterValueChangedCallback(e => OnSearchTextFieldValueChanged(e));

            var avatarCatalogMenu = header.Q<ToolbarMenu>("avatar-catalog-menu");
            avatarCatalogMenu.menu.AppendAction("Update avatar catalog", action => ReloadAvatarList());
            avatarCatalogMenu.menu.AppendAction("Update avatar catalog (with regenerate thumbnails)", action => ReloadAvatarList(true));

            var resizeGridItemSlider = footer.Q<Slider>("resize-grid-item-slider");
            resizeGridItemSlider.value = _gridItemSize;
            resizeGridItemSlider.RegisterValueChangedCallback((e) => OnResizeGridItemSliderValueChanged(e));
            resizeGridItemSlider.RegisterCallback<PointerCaptureOutEvent>(e => OnResizeGridItemSliderPointerCaptureOut(e));

            root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void ShowAvatarListView()
        {
            if (_avatarListView == null && _initialSetupView == null)
            {
                return;
            }

            _avatarListView.style.display = DisplayStyle.Flex;
            _initialSetupView.style.display = DisplayStyle.None;
        }

        private void UpdateGridLayout()
        {
            var gridContainer = _avatarListView.Q<VisualElement>("avatar-list-scroll-view").Q<VisualElement>("items-container");

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

                    var gridLayoutItem = _gridLayoutListItemAsset.CloneTree();
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

                e.menu.AppendAction("Generate avatar thumbnail image", action =>
                {
                    GenerateAvatarThumbnail(avatar);
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

                e.menu.AppendAction("Build and Publish avatar", async action =>
                {

                    await BuildAndPublishAvatar(avatar);
                });
            });

            return manipulator;
        }

        private async Task BuildAndPublishAvatar(AvatarCatalogDatabase.AvatarCatalogEntry avatar)
        {
            ChangeToActiveAvatar(avatar);

            if (!GlobalObjectId.TryParse(avatar.avatarGlobalObjectId, out var avatarGlobalObjectId))
            {
                Debug.LogError("Failed to parse GlobalObjectId");
                return;
            }

            var avatarObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(avatarGlobalObjectId) as GameObject;
            if (avatarObject == null)
            {
                EditorUtility.DisplayDialog("エラー", $"アバター '{avatar.avatarObjectName}' が見つかりませんでした。", "OK");
                Debug.LogError("failed to find avatar object");
                return;
            }

            if (VRCSdkControlPanel.window == null)
            {
                EditorUtility.DisplayDialog("エラー", "VRChat SDKを表示してください。", "OK");
                Debug.LogError("please open VRChat SDK window");
                return;
            }

            if (!ConfigManager.RemoteConfig.IsInitialized())
            {
                API.SetOnlineMode(true);
                ConfigManager.RemoteConfig.Init();
            }

            if (!APIUser.IsLoggedIn)
            {
                VRCSdkControlPanel.InitAccount();
                if (ApiCredentials.Load())
                {
                    var tcs = new TaskCompletionSource<bool>();
                    APIUser.InitialFetchCurrentUser(c =>
                    {
                        if (c.Model is not APIUser apiUser)
                        {
                            Debug.LogError("failed to load user, please login again with your VRChat account");
                            tcs.SetResult(false);
                            return;
                        }
                        AnalyticsSDK.LoggedInUserChanged(apiUser);
                        tcs.SetResult(true);
                    }, err =>
                    {
                        Debug.LogError(err.Error);
                        tcs.SetResult(false);
                    });

                    await tcs.Task;
                }

                if (!APIUser.IsLoggedIn)
                {
                    EditorUtility.DisplayDialog("エラー", "VRChat アカウントでログインしてください。", "OK");
                    Debug.LogError("please login with your VRChat account");
                    return;
                }
            }

            if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder))
            {
                Debug.LogError("failed to get avatar builder");
                return;
            }

            var pipelineManager = avatarObject.GetComponent<PipelineManager>();
            if (pipelineManager == null)
            {
                Debug.LogError("failed to find Pipeline Manager");
                return;
            }

            if (string.IsNullOrEmpty(pipelineManager.blueprintId))
            {
                Debug.LogError("Blueprint ID is null or empty");
                return;
            }

            VRCAvatar vrcAvatar = default;
            try
            {
                vrcAvatar = await VRCApi.GetAvatar(pipelineManager.blueprintId, true);
            }
            catch (ApiErrorException ex)
            {
                if (ex.StatusCode != HttpStatusCode.NotFound)
                {
                    throw new Exception("Unexpected error", ex);
                }
            }

            if (string.IsNullOrEmpty(vrcAvatar.ID))
            {
                Debug.LogError("Avatars not yet uploaded");
                return;
            }

            try
            {
                await builder.BuildAndUpload(avatarObject, vrcAvatar);
                EditorUtility.DisplayDialog("情報", "アバターのビルドおよびアップロードが完了しました。", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
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
                    Debug.LogError("failed to save open scene");
                    return;
                }

                EditorSceneManager.OpenScene(scenePath);
            }

            if (!GlobalObjectId.TryParse(avatar.avatarGlobalObjectId, out var avatarGlobalObjectId))
            {
                EditorSceneManager.OpenScene(currentScenePath);
                Debug.LogError("Failed to parse GlobalObjectId");
                return;
            }

            var avatarObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(avatarGlobalObjectId) as GameObject;
            if (avatarObject == null)
            {
                EditorSceneManager.OpenScene(currentScenePath);
                EditorUtility.DisplayDialog("エラー", $"アバター '{avatar.avatarObjectName}' が見つかりませんでした。", "OK");
                Debug.LogError("failed to find avatar object");
                return;
            }

            AvatarMetadataEditorWindow.ShowWindow(avatarObject);
        }

        private void BuildAvatarCatalogDatabase(bool withRegenerateThumbnails = false)
        {
            DatabaseBuilder.BuildAvatarCatalogDatabaseAndIndexes(withRegenerateThumbnails);

            _avatarCatalogDatabase = AvatarCatalogDatabase.Load();
            _avatarSearchIndex = AvatarSearchIndex.Load();
        }

        private void ReloadAvatarList(bool withRegenerateThumbnails = false)
        {
            BuildAvatarCatalogDatabase(withRegenerateThumbnails);
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

        private void GenerateAvatarThumbnail(AvatarCatalogDatabase.AvatarCatalogEntry avatar)
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
                        Debug.LogError("failed to save open scene");
                        return;
                    }

                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }

                if (!GlobalObjectId.TryParse(avatar.avatarGlobalObjectId, out var avatarGlobalObjectId))
                {
                    Debug.LogError("Failed to parse GlobalObjectId");
                    return;
                }

                var avatarObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(avatarGlobalObjectId) as GameObject;
                if (avatarObject == null)
                {
                    Debug.LogError("failed to find avatar object");
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
            UpdateViews();
        }

        private void UpdateViews()
        {
            if (AvatarCatalogDatabase.IsDatabaseFileExists())
            {
                ShowAvatarListView();
                return;
            }

            var gridContainer = _avatarListView.Q<VisualElement>("avatar-list-scroll-view").Q<VisualElement>("items-container");
            gridContainer.Clear();

            ShowInitialSetupView();
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
            BuildAvatarCatalogDatabase();
            UpdateGridLayout();
            UpdateViews();
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
