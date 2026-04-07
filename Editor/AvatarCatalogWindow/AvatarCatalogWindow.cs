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
using VRC.SDKBase;
using VRC.SDKBase.Editor.Api;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarCatalogWindow : EditorWindow
    {
        private static readonly string _mainUxmlGuid = "486221a5f5fdd0b4cbc5ccc22397d354";
        private static readonly string _gridLayoutListItemUxmlGuid = "74e74187aebb7f6469bfc215a2ec332d";

        private static readonly int MinColumnSpacing = 10;
        private static readonly char[] SearchWordsDelimiterChars = { ' ' };

        private VisualElement _avatarListView;
        private VisualElement _initialSetupView;
        private ListView _avatarGridListView;
        private Label _noResultsLabel;

        private VisualTreeAsset _gridLayoutListItemAsset;

        private string _searchText = "";
        private float _gridItemSize = Preferences.DefaultAvatarCatalogMaxItemSize;
        private AvatarSearchIndex _avatarSearchIndex = null;
        private AvatarDatabase _avatarCatalogDatabase = null;
        private Preferences _preferences;

        private List<AvatarDatabase.AvatarDatabaseEntry> _filteredAvatars = new List<AvatarDatabase.AvatarDatabaseEntry>();
        private int _currentMaxColumns = 1;
        private Dictionary<string, Texture2D> _thumbnailCache = new Dictionary<string, Texture2D>();

        [MenuItem("Tools/Avatar Catalog/Avatar List")]
        public static void ShowWindow()
        {
            var window = GetWindow<AvatarCatalogWindow>("Avatar List");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            _avatarCatalogDatabase = AvatarDatabase.Load();
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

        private bool EnsureSceneLoaded(string scenePath, OpenSceneMode mode, out Scene scene)
        {
            scene = SceneManager.GetSceneByPath(scenePath);
            if (scene.isLoaded)
            {
                return true;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                return false;
            }

            scene = EditorSceneManager.OpenScene(scenePath, mode);
            return scene.IsValid() && scene.isLoaded;
        }

        private GameObject ChangeSelectingObject(AvatarDatabase.AvatarDatabaseEntry avatar)
        {
            if (!GlobalObjectId.TryParse(avatar.avatarGlobalObjectId, out var avatarGlobalObjectId))
            {
                Debug.LogError("failed to try parse avatar GlobalObjectId");
                return null;
            }

            var scenePath = AssetDatabase.GUIDToAssetPath(avatar.sceneAssetGuid);

            if (!EnsureSceneLoaded(scenePath, OpenSceneMode.Single, out var scene))
            {
                return null;
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

            FontCache.ApplyPreferredFont(rootVisualElement);

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

            mainUxmlAsset.CloneTree(rootVisualElement);

            return true;
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            FontCache.ApplyPreferredFont(rootVisualElement);
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

            _avatarGridListView = root.Q<ListView>("avatar-grid-list-view");
            _avatarGridListView.makeItem = MakeGridRow;
            _avatarGridListView.bindItem = BindGridRow;
            _avatarGridListView.unbindItem = UnbindGridRow;

            _noResultsLabel = root.Q<Label>("no-results-label");

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

        private void RefreshGridView()
        {
            if (_avatarGridListView == null)
            {
                return;
            }

            if (_avatarCatalogDatabase == null || _avatarCatalogDatabase.avatars.Count == 0)
            {
                _avatarGridListView.itemsSource = null;
                _avatarGridListView.style.display = DisplayStyle.Flex;
                _noResultsLabel.style.display = DisplayStyle.None;
                return;
            }

            _filteredAvatars = FilterAvatars(_avatarCatalogDatabase.avatars, _searchText);

            if (_filteredAvatars.Count == 0)
            {
                _avatarGridListView.itemsSource = null;
                _avatarGridListView.style.display = DisplayStyle.None;
                _noResultsLabel.style.display = _searchText.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                return;
            }

            _noResultsLabel.style.display = DisplayStyle.None;
            _avatarGridListView.style.display = DisplayStyle.Flex;

            var scrollViewWidth = rootVisualElement.contentRect.width;
            if (float.IsNaN(scrollViewWidth) || scrollViewWidth <= 0)
            {
                return;
            }

            _currentMaxColumns = Mathf.Max(1, Mathf.FloorToInt((scrollViewWidth - MinColumnSpacing) / (_gridItemSize + MinColumnSpacing)));
            var rowCount = Mathf.CeilToInt((float)_filteredAvatars.Count / _currentMaxColumns);

            _avatarGridListView.fixedItemHeight = _gridItemSize + 15;
            _avatarGridListView.itemsSource = Enumerable.Range(0, rowCount).ToList();
            _avatarGridListView.Rebuild();
        }

        private VisualElement MakeGridRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.NoWrap;
            row.style.alignSelf = Align.Center;
            return row;
        }

        private VisualElement CreateGridItem()
        {
            var gridItem = _gridLayoutListItemAsset.CloneTree();

            var avatarThumbnailImage = gridItem.Q<Image>("avatar-thumbnail-image");
            avatarThumbnailImage.RegisterCallback<GeometryChangedEvent>(e =>
            {
                avatarThumbnailImage.style.width = e.newRect.height;
            });

            gridItem.RegisterCallback<ClickEvent>(e =>
            {
                if (gridItem.userData is AvatarDatabase.AvatarDatabaseEntry avatar)
                {
                    OnAvatarItemClick(e, avatar);
                }
            });

            var manipulator = new ContextualMenuManipulator(e =>
            {
                if (gridItem.userData is AvatarDatabase.AvatarDatabaseEntry avatar)
                {
                    e.menu.AppendAction("Switch to active", action => ChangeToActiveAvatar(avatar));
                    e.menu.AppendAction("Generate avatar thumbnail image", action =>
                    {
                        GenerateAvatarThumbnail(avatar);
                        ReloadAvatars();
                    });
                    e.menu.AppendAction("Add avatar thumbnail settings component", action => AddAvatarCatalogThumbnailSettingsComponent(avatar));
                    e.menu.AppendAction("Add avatar metadata component", action => AddAvatarMetadataComponent(avatar));
                    e.menu.AppendAction("Build and Publish avatar", action => { _ = BuildAndPublishAvatarSafe(avatar); });
                }
            });
            gridItem.AddManipulator(manipulator);

            return gridItem;
        }

        private void BindGridRow(VisualElement row, int rowIndex)
        {
            // 必要に応じて子要素を追加（列数が増えた場合）
            while (row.childCount < _currentMaxColumns)
            {
                row.Add(CreateGridItem());
            }

            var startIndex = rowIndex * _currentMaxColumns;
            var scrollViewWidth = _avatarGridListView.resolvedStyle.width;
            if (float.IsNaN(scrollViewWidth) || scrollViewWidth <= 0)
            {
                scrollViewWidth = rootVisualElement.contentRect.width;
            }

            var totalRowWidth = _currentMaxColumns * _gridItemSize;
            var spaceBetween = (scrollViewWidth - totalRowWidth) / (_currentMaxColumns + 1);
            row.style.width = scrollViewWidth - (MinColumnSpacing * 2) - spaceBetween;
            row.style.justifyContent = _currentMaxColumns > 1 ? Justify.SpaceBetween : Justify.Center;

            var col = 0;
            foreach (var gridItem in row.Children())
            {
                if (col >= _currentMaxColumns)
                {
                    // 列数が減った場合の余剰スロットを非表示にする
                    gridItem.style.display = DisplayStyle.None;
                    col++;
                    continue;
                }

                var avatarIndex = startIndex + col;
                gridItem.style.display = DisplayStyle.Flex;
                gridItem.style.width = _gridItemSize;
                gridItem.style.height = _gridItemSize;

                if (avatarIndex < _filteredAvatars.Count)
                {
                    var avatar = _filteredAvatars[avatarIndex];
                    gridItem.userData = avatar;
                    gridItem.style.visibility = Visibility.Visible;

                    var image = gridItem.Q<Image>("avatar-thumbnail-image");
                    image.image = LoadAvatarThumbnailImage(avatar);

                    var label = gridItem.Q<Label>("avatar-name-label");
                    label.text = avatar.avatarObjectName;
                }
                else
                {
                    // 末尾行のダミースロット
                    gridItem.userData = null;
                    gridItem.style.visibility = Visibility.Hidden;
                }

                col++;
            }
        }

        private void UnbindGridRow(VisualElement row, int rowIndex)
        {
            foreach (var gridItem in row.Children())
            {
                gridItem.userData = null;
                var image = gridItem.Q<Image>("avatar-thumbnail-image");
                if (image != null)
                {
                    image.image = null;
                }
            }
        }

        private Texture2D LoadAvatarThumbnailImage(AvatarDatabase.AvatarDatabaseEntry entry)
        {
            if (string.IsNullOrEmpty(entry.thumbnailImageGuid))
            {
                return null;
            }

            if (_thumbnailCache.TryGetValue(entry.thumbnailImageGuid, out var cached))
            {
                return cached;
            }

            if (GUID.TryParse(entry.thumbnailImageGuid, out var thumbnailImageGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(thumbnailImageGuid);
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    _thumbnailCache[entry.thumbnailImageGuid] = texture;
                }
                return texture;
            }

            return null;
        }

        private List<AvatarDatabase.AvatarDatabaseEntry> FilterAvatars(List<AvatarDatabase.AvatarDatabaseEntry> avatars, string searchText)
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
                    return new List<AvatarDatabase.AvatarDatabaseEntry>();
                }

                result = result.Where(avatar => avatarGlobalObjectIds.Exists(id => id == avatar.avatarGlobalObjectId)).ToList();
            }

            return result;
        }

        private async Task BuildAndPublishAvatarSafe(AvatarDatabase.AvatarDatabaseEntry avatar)
        {
            try
            {
                await BuildAndPublishAvatar(avatar);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                EditorUtility.DisplayDialog("エラー", $"アバターのビルドおよびアップロード中に予期しないエラーが発生しました。\n\n{e.Message}", "OK");
            }
        }

        private async Task BuildAndPublishAvatar(AvatarDatabase.AvatarDatabaseEntry avatar)
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

            VRChatUtil.ClearVRCSDKIssues();
            VRChatUtil.InitializeRemoteConfig();

            try
            {
                if (!await VRChatUtil.LogIn())
                {
                    EditorUtility.DisplayDialog("エラー", "VRChat アカウントでログインしてください。", "OK");
                    Debug.LogError("please login with your VRChat account");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                EditorUtility.DisplayDialog("エラー", $"VRChat へのログイン中にエラーが発生しました。\n\n{ex.Message}", "OK");
                return;
            }

            if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder))
            {
                Debug.LogError("failed to get avatar builder");
                EditorUtility.DisplayDialog("エラー", "VRChat SDK のアバタービルダーを取得できませんでした。VRChat SDK が正しくインストールされているか確認してください。", "OK");
                return;
            }

            var pipelineManager = avatarObject.GetComponent<PipelineManager>();
            if (pipelineManager == null)
            {
                Debug.LogError("failed to find Pipeline Manager");
                EditorUtility.DisplayDialog("エラー", $"アバター '{avatar.avatarObjectName}' に Pipeline Manager コンポーネントが見つかりませんでした。", "OK");
                return;
            }

            if (string.IsNullOrEmpty(pipelineManager.blueprintId))
            {
                Debug.LogError("Blueprint ID is null or empty");
                EditorUtility.DisplayDialog("エラー", $"アバター '{avatar.avatarObjectName}' の Blueprint ID が設定されていません。VRChat SDK のコントロールパネルからアバター情報を設定してください。", "OK");
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
                EditorUtility.DisplayDialog("エラー", $"アバター '{avatar.avatarObjectName}' はまだ VRChat にアップロードされていません。先に VRChat SDK のコントロールパネルから初回アップロードを行ってください。", "OK");
                return;
            }

            if (!VRChatUtil.AgreedContentThisSession.Contains(vrcAvatar.ID))
            {
                if (!EditorUtility.DisplayDialog("Confirm", VRCCopyrightAgreement.AgreementText, "OK", "Cancel"))
                {
                    return;
                }
            }

            try
            {
                await VRChatUtil.AgreeCopyrightAgreement(vrcAvatar.ID);
                await builder.BuildAndUpload(avatarObject, vrcAvatar);
                EditorUtility.DisplayDialog("情報", "アバターのビルドおよびアップロードが完了しました。", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                EditorUtility.DisplayDialog("エラー", $"アバターのビルドおよびアップロード中にエラーが発生しました。\n\n{e.Message}", "OK");
            }
        }

        private void BuildAvatarCatalogDatabase(bool withRegenerateThumbnails = false)
        {
            var databaseBuilder = new DatabaseBuilder();
            databaseBuilder.BuildAvatarCatalogDatabaseAndIndexes(withRegenerateThumbnails);

            _avatarCatalogDatabase = AvatarDatabase.Load();
            _avatarSearchIndex = AvatarSearchIndex.Load();
        }

        private void ReloadAvatarList(bool withRegenerateThumbnails = false)
        {
            BuildAvatarCatalogDatabase(withRegenerateThumbnails);
            _thumbnailCache.Clear();
            RefreshGridView();
        }

        private void ChangeToActiveAvatar(AvatarDatabase.AvatarDatabaseEntry avatar)
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

        private void GenerateAvatarThumbnail(AvatarDatabase.AvatarDatabaseEntry avatar)
        {
            var scenePath = AssetDatabase.GUIDToAssetPath(avatar.sceneAssetGuid);

            // Additiveでロード
            if (!EnsureSceneLoaded(scenePath, OpenSceneMode.Additive, out var scene))
            {
                return;
            }

            using var avatarRenderer = new AvatarRenderer();

            try
            {
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

                var avatarCatalogDatabaseEntry = _avatarCatalogDatabase.avatars.FirstOrDefault(a => a.avatarGlobalObjectId == avatar.avatarGlobalObjectId);
                if (avatarCatalogDatabaseEntry != null)
                {
                    var thumbnail = AvatarThumbnailUtil.RenderAvatarThumbnail(avatarRenderer, targetAvatarObject);
                    try
                    {
                        if (avatarCatalogDatabaseEntry.thumbnailImageGuid != "" && GUID.TryParse(avatarCatalogDatabaseEntry.thumbnailImageGuid, out var thumbnailImageGuid))
                        {
                            // 古いサムネイル画像を削除
                            AvatarThumbnailUtil.DeleteAvatarThumbnailImage(thumbnailImageGuid);
                            avatarCatalogDatabaseEntry.thumbnailImageGuid = "";
                            AssetDatabase.Refresh();
                        }

                        // 再生成したサムネイル画像を保存
                        avatarCatalogDatabaseEntry.thumbnailImageGuid = AvatarThumbnailUtil.StoreAvatarThumbnailImage(thumbnail).ToString();
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(thumbnail);
                    }
                }

                _thumbnailCache.Remove(avatar.thumbnailImageGuid);
                AvatarDatabase.Save(_avatarCatalogDatabase);
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

        private void AddAvatarCatalogThumbnailSettingsComponent(AvatarDatabase.AvatarDatabaseEntry avatar)
        {
            var scenePath = AssetDatabase.GUIDToAssetPath(avatar.sceneAssetGuid);

            // Additiveでロード
            if (!EnsureSceneLoaded(scenePath, OpenSceneMode.Additive, out var scene))
            {
                return;
            }

            try
            {
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

        private void AddAvatarMetadataComponent(AvatarDatabase.AvatarDatabaseEntry avatar)
        {
            var scenePath = AssetDatabase.GUIDToAssetPath(avatar.sceneAssetGuid);

            SceneProcessor.ProcessSceneTemporarily(scenePath, (scene) =>
            {
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

                var component = avatarObject.GetComponent<AvatarMetadata>();
                if (component != null)
                {
                    return;
                }

                avatarObject.AddComponent<AvatarMetadata>();
                EditorUtility.SetDirty(avatarObject);

                EditorSceneManager.SaveScene(scene);
            });
        }

        public void ReloadAvatars()
        {
            Preferences.Load();
            _avatarCatalogDatabase = AvatarDatabase.Load();
            _avatarSearchIndex = AvatarSearchIndex.Load();
            _thumbnailCache.Clear();

            RefreshGridView();
            UpdateViews();
        }

        private void UpdateViews()
        {
            if (AvatarDatabase.IsDatabaseFileExists())
            {
                ShowAvatarListView();
                return;
            }

            if (_avatarGridListView != null)
            {
                _avatarGridListView.itemsSource = null;
            }

            ShowInitialSetupView();
        }

        private void OnSearchTextFieldValueChanged(ChangeEvent<string> e)
        {
            _searchText = e.newValue;
            RefreshGridView();
        }

        private void OnAvatarItemClick(ClickEvent e, AvatarDatabase.AvatarDatabaseEntry avatar)
        {
            if (e.button == (int)MouseButton.LeftMouse && e.clickCount == 2)
            {
                ChangeToActiveAvatar(avatar);
            }
        }

        private void OnRunInitialSetupButtonClick()
        {
            BuildAvatarCatalogDatabase();
            RefreshGridView();
            UpdateViews();
        }

        private void OnResizeGridItemSliderValueChanged(ChangeEvent<float> e)
        {
            _gridItemSize = e.newValue;
            RefreshGridView();
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
            // 幅が変わっていない（高さのみの変更や微小なレイアウト更新）場合はリビルドをスキップ
            if (Mathf.Approximately(e.oldRect.width, e.newRect.width))
            {
                return;
            }

            RefreshGridView();
        }
    }
}