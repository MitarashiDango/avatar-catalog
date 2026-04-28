using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
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
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.Api;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarCatalogWindow : EditorWindow
    {
        private static readonly string _mainUxmlGuid = "486221a5f5fdd0b4cbc5ccc22397d354";
        private static readonly string _gridLayoutListItemUxmlGuid = "74e74187aebb7f6469bfc215a2ec332d";

        private static readonly int MinColumnSpacing = 10;
        // グリッドのサムネイル下部に表示するアバター名ラベルの高さ (px)。fixedItemHeight = サムネイル高さ + このラベル分
        private static readonly int GridItemLabelHeight = 15;
        // Avatar List ウィンドウの最小サイズ
        private static readonly Vector2 DefaultMinWindowSize = new Vector2(800, 600);
        // 半角スペース、全角スペース (U+3000)、タブを区切り文字とする
        private static readonly char[] SearchWordsDelimiterChars = { ' ', '\u3000', '\t' };

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

        // アバターのマルチセレクト状態を avatarGlobalObjectId 単位で保持する。
        // ListView 標準の selection は本ビューのレイアウトで使えないため独自管理している。
        // 検索フィルタ変更時も選択状態を維持するため、フィルタ後に表示されないアバターも含めて保持する。
        private HashSet<string> _selectedAvatarGlobalObjectIds = new HashSet<string>();
        // Shift+クリックの範囲選択アンカー (avatarGlobalObjectId)。アンカーが現在のフィルタ結果に存在しない場合は単一選択にフォールバック。
        private string _selectionAnchorAvatarGlobalObjectId;

        private const string AvatarGridItemSelectedClass = "avatar-grid-item-selected";

        [MenuItem("Tools/Avatar Catalog/Avatar List")]
        public static void ShowWindow()
        {
            var window = GetWindow<AvatarCatalogWindow>(AcL10n.Tr("window.avatar_list.title"));
            window.minSize = DefaultMinWindowSize;
        }

        private void OnEnable()
        {
            _avatarCatalogDatabase = AvatarDatabase.Load();
            _avatarSearchIndex = AvatarSearchIndex.Load();
            _preferences = Preferences.Load();

            ApplyFromPreferences();

            AcL10n.OnLanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            AcL10n.OnLanguageChanged -= OnLanguageChanged;
            // ウィンドウ非表示時・アセンブリリロード前に参照を解放し、キャッシュの無制限な膨張を防ぐ
            _thumbnailCache.Clear();
        }

        private void OnDestroy()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            AcL10n.OnLanguageChanged -= OnLanguageChanged;
            _thumbnailCache.Clear();
        }

        private void OnLanguageChanged()
        {
            // 言語切替時にウィンドウタイトルと UI を再構築する
            titleContent = new GUIContent(AcL10n.Tr("window.avatar_list.title"));
            rootVisualElement.Clear();
            CreateGUI();
        }

        private void ApplyFromPreferences()
        {
            _gridItemSize = _preferences != null ? _preferences.avatarCatalogItemSize : Preferences.DefaultAvatarCatalogMaxItemSize;
        }

        private bool EnsureSceneLoaded(string scenePath, OpenSceneMode mode, out Scene scene, BulkScenePolicy scenePolicy = BulkScenePolicy.Ask)
        {
            scene = SceneManager.GetSceneByPath(scenePath);
            if (scene.isLoaded)
            {
                return true;
            }

            // Single モード切替で現在のシーンが閉じられる場合の保存挙動はポリシーに従う
            if (mode == OpenSceneMode.Single)
            {
                switch (scenePolicy)
                {
                    case BulkScenePolicy.Ask:
                        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        {
                            return false;
                        }
                        break;
                    case BulkScenePolicy.AutoSave:
                        // パスが設定されている dirty なシーンのみ自動保存。未保存(無題)シーンは保存できないのでスキップ。
                        for (var i = 0; i < SceneManager.sceneCount; i++)
                        {
                            var s = SceneManager.GetSceneAt(i);
                            if (s.isDirty && !string.IsNullOrEmpty(s.path))
                            {
                                EditorSceneManager.SaveScene(s);
                            }
                        }
                        break;
                    case BulkScenePolicy.Discard:
                        // 何もしない: OpenScene Single が現在のシーンを置き換える際に変更は破棄される
                        break;
                }
            }

            scene = EditorSceneManager.OpenScene(scenePath, mode);
            return scene.IsValid() && scene.isLoaded;
        }

        private GameObject ChangeSelectingObject(AvatarDatabase.AvatarDatabaseEntry avatar, BulkScenePolicy scenePolicy = BulkScenePolicy.Ask)
        {
            if (!GlobalObjectId.TryParse(avatar.avatarGlobalObjectId, out var avatarGlobalObjectId))
            {
                Debug.LogError("failed to try parse avatar GlobalObjectId");
                return null;
            }

            var scenePath = AssetDatabase.GUIDToAssetPath(avatar.sceneAssetGuid);

            if (!EnsureSceneLoaded(scenePath, OpenSceneMode.Single, out var scene, scenePolicy))
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
            UxmlLocalizer.Apply(rootVisualElement);

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
            avatarCatalogMenu.text = AcL10n.Tr("toolbar.menu");
            avatarCatalogMenu.menu.AppendAction(AcL10n.Tr("menu.update_avatar_database"), action => ReloadAvatarList());
            avatarCatalogMenu.menu.AppendAction(AcL10n.Tr("menu.update_avatar_database_with_thumbnails"), action => ReloadAvatarList(true));

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

            _avatarGridListView.fixedItemHeight = _gridItemSize + GridItemLabelHeight;
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
                if (gridItem.userData is not AvatarDatabase.AvatarDatabaseEntry avatar)
                {
                    return;
                }

                // 右クリック対象が選択集合に含まれており、かつ可視選択数が 2 件以上の場合のみ一括メニューを表示。
                // 単一選択や、フィルタにより右クリック対象しか可視でない場合は通常メニューを優先する。
                var visibleSelected = GetSelectedVisibleAvatars();
                var isBulkContext = visibleSelected.Count >= 2
                    && _selectedAvatarGlobalObjectIds.Contains(avatar.avatarGlobalObjectId);

                if (isBulkContext)
                {
                    e.menu.AppendAction(AcL10n.Tr("context.bulk_build_and_publish", visibleSelected.Count),
                        action => { _ = BulkBuildAndPublishAvatarsSafe(visibleSelected); });
                    return;
                }

                e.menu.AppendAction(AcL10n.Tr("context.switch_active"), action => ChangeToActiveAvatar(avatar, out _));
                e.menu.AppendAction(AcL10n.Tr("context.generate_thumbnail"), action =>
                {
                    GenerateAvatarThumbnail(avatar);
                    ReloadAvatars();
                });
                e.menu.AppendAction(AcL10n.Tr("context.add_thumbnail_settings"), action => AddAvatarCatalogThumbnailSettingsComponent(avatar));
                e.menu.AppendAction(AcL10n.Tr("context.add_metadata"), action => AddAvatarMetadataComponent(avatar));
                e.menu.AppendAction(AcL10n.Tr("context.build_and_publish"), action => { _ = BuildAndPublishAvatarSafe(avatar); });
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

                    // 選択状態を視覚化 (再バインド時にもクラス状態を確実に同期)
                    var isSelected = !string.IsNullOrEmpty(avatar.avatarGlobalObjectId)
                        && _selectedAvatarGlobalObjectIds.Contains(avatar.avatarGlobalObjectId);
                    gridItem.EnableInClassList(AvatarGridItemSelectedClass, isSelected);
                }
                else
                {
                    // 末尾行のダミースロット
                    gridItem.userData = null;
                    gridItem.style.visibility = Visibility.Hidden;
                    gridItem.EnableInClassList(AvatarGridItemSelectedClass, false);
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
                gridItem.EnableInClassList(AvatarGridItemSelectedClass, false);
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
                // Unity の破棄済みオブジェクト判定 (== null) で stale エントリを除去する
                if (cached != null)
                {
                    return cached;
                }
                _thumbnailCache.Remove(entry.thumbnailImageGuid);
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
                // カタカナ/ひらがな同一視、全角/半角統一、カルチャ非依存の小文字化を行ったうえで、
                // 区切り文字で分割し、空エントリは除外する
                var searchWords = SearchTextNormalizer.Normalize(searchText)
                    .Split(SearchWordsDelimiterChars, StringSplitOptions.RemoveEmptyEntries);

                if (searchWords.Length == 0)
                {
                    return result;
                }

                if (_avatarSearchIndex == null)
                {
                    _avatarSearchIndex = AvatarSearchIndex.LoadOrCreateFile();
                }

                var avatarGlobalObjectIds = _avatarSearchIndex.GetGlobalObjectIds(searchWords);
                if (avatarGlobalObjectIds.Count == 0)
                {
                    return new List<AvatarDatabase.AvatarDatabaseEntry>();
                }

                result = result.Where(avatar => avatarGlobalObjectIds.Contains(avatar.avatarGlobalObjectId)).ToList();
            }

            return result;
        }

        private static string BuildAndPublishProgressTitle => AcL10n.Tr("progress.build_publish.title");

        private async Task BuildAndPublishAvatarSafe(AvatarDatabase.AvatarDatabaseEntry avatar)
        {
            try
            {
                await BuildAndPublishAvatar(avatar);
            }
            catch (OperationCanceledException)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    AcL10n.Tr("dialog.title.info"),
                    AcL10n.Tr("info.upload_cancelled"),
                    AcL10n.Tr("dialog.button.ok"));
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    AcL10n.Tr("dialog.title.error"),
                    AcL10n.Tr("error.unexpected_build_upload", e.Message),
                    AcL10n.Tr("dialog.button.ok"));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private async Task BuildAndPublishAvatar(AvatarDatabase.AvatarDatabaseEntry avatar)
        {
            EditorUtility.DisplayProgressBar(BuildAndPublishProgressTitle, AcL10n.Tr("progress.build_publish.preparing"), 0.05f);

            if (!ChangeToActiveAvatar(avatar, out var avatarObject))
            {
                return;
            }

            VRChatUtil.ClearVRCSDKIssues();
            VRChatUtil.InitializeRemoteConfig();

            EditorUtility.DisplayProgressBar(BuildAndPublishProgressTitle, AcL10n.Tr("progress.build_publish.logging_in"), 0.15f);

            if (!await EnsureVRChatLoggedIn())
            {
                return;
            }

            EditorUtility.DisplayProgressBar(BuildAndPublishProgressTitle, AcL10n.Tr("progress.build_publish.getting_builder"), 0.25f);

            if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder))
            {
                ShowError(
                    AcL10n.Tr("dialog.title.error"),
                    AcL10n.Tr("error.cannot_get_sdk_builder"));
                return;
            }

            if (!ValidateAvatarSetup(avatarObject, avatar, out var pipelineManager))
            {
                return;
            }

            EditorUtility.DisplayProgressBar(BuildAndPublishProgressTitle, AcL10n.Tr("progress.build_publish.getting_avatar_info"), 0.35f);

            var (resolved, vrcAvatar) = await TryResolveVRCAvatar(pipelineManager.blueprintId, avatar.avatarObjectName);
            if (!resolved)
            {
                return;
            }

            EditorUtility.DisplayProgressBar(BuildAndPublishProgressTitle, AcL10n.Tr("progress.build_publish.confirming_agreement"), 0.45f);

            if (!await ConfirmCopyrightAgreement(vrcAvatar.ID))
            {
                return;
            }

            await ExecuteBuildAndUpload(builder, avatarObject, vrcAvatar);
        }

        private async Task<bool> EnsureVRChatLoggedIn()
        {
            try
            {
                if (!await VRChatUtil.LogIn())
                {
                    ShowError(AcL10n.Tr("dialog.title.error"), AcL10n.Tr("error.not_logged_in"));
                    return false;
                }
                return true;
            }
            catch (VRChatSessionExpiredException ex)
            {
                ShowError(
                    AcL10n.Tr("dialog.title.error"),
                    AcL10n.Tr("error.session_expired"),
                    ex);
                return false;
            }
            catch (Exception ex)
            {
                ShowError(
                    AcL10n.Tr("dialog.title.error"),
                    AcL10n.Tr("error.login_failed", ex.Message),
                    ex);
                return false;
            }
        }

        private bool ValidateAvatarSetup(
            GameObject avatarObject,
            AvatarDatabase.AvatarDatabaseEntry avatar,
            out PipelineManager pipelineManager)
        {
            if (TryValidateAvatarSetup(avatarObject, avatar, out pipelineManager, out var errorMessage))
            {
                return true;
            }
            ShowError(AcL10n.Tr("dialog.title.error"), errorMessage);
            return false;
        }

        // ダイアログ表示を伴わない検証コア。一括処理時にエラーメッセージを集約するために使用する。
        private static bool TryValidateAvatarSetup(
            GameObject avatarObject,
            AvatarDatabase.AvatarDatabaseEntry avatar,
            out PipelineManager pipelineManager,
            out string errorMessage)
        {
            errorMessage = null;
            pipelineManager = avatarObject.GetComponent<PipelineManager>();
            if (pipelineManager == null)
            {
                errorMessage = AcL10n.Tr("error.pipeline_manager_missing", avatar.avatarObjectName);
                return false;
            }

            if (string.IsNullOrEmpty(pipelineManager.blueprintId))
            {
                errorMessage = AcL10n.Tr("error.blueprint_id_missing", avatar.avatarObjectName);
                return false;
            }

            return true;
        }

        private async Task<(bool ok, VRCAvatar vrcAvatar)> TryResolveVRCAvatar(string blueprintId, string avatarDisplayName)
        {
            var (ok, vrcAvatar, errorMessage, exception) = await TryResolveVRCAvatarCore(blueprintId, avatarDisplayName);
            if (!ok)
            {
                ShowError(AcL10n.Tr("dialog.title.error"), errorMessage, exception);
            }
            return (ok, vrcAvatar);
        }

        // ダイアログ表示を伴わないアバター情報取得コア。一括処理時にエラーメッセージを集約するために使用する。
        private static async Task<(bool ok, VRCAvatar vrcAvatar, string errorMessage, Exception exception)> TryResolveVRCAvatarCore(
            string blueprintId,
            string avatarDisplayName)
        {
            try
            {
                var vrcAvatar = await VRCApi.GetAvatar(blueprintId, true);
                if (string.IsNullOrEmpty(vrcAvatar.ID))
                {
                    return (false, default, AcL10n.Tr("error.avatar_not_uploaded", avatarDisplayName), null);
                }
                return (true, vrcAvatar, null, null);
            }
            catch (ApiErrorException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return (false, default, AcL10n.Tr("error.avatar_not_uploaded", avatarDisplayName), ex);
            }
            catch (ApiErrorException ex)
            {
                return (false, default, AcL10n.Tr("error.get_avatar_info_failed", (int)ex.StatusCode, ex.StatusCode, ex.Message), ex);
            }
            catch (TaskCanceledException ex)
            {
                return (false, default, AcL10n.Tr("error.network_failure", ex.Message), ex);
            }
        }

        private async Task<bool> ConfirmCopyrightAgreement(string avatarId)
        {
            if (VRChatUtil.AgreedContentThisSession.Contains(avatarId))
            {
                return true;
            }

            // 同意ダイアログが進捗バーと重ならないよう一旦クリア
            EditorUtility.ClearProgressBar();

            if (!EditorUtility.DisplayDialog(
                AcL10n.Tr("dialog.title.confirm"),
                VRCCopyrightAgreement.AgreementText,
                AcL10n.Tr("dialog.button.ok"),
                AcL10n.Tr("dialog.button.cancel")))
            {
                return false;
            }

            try
            {
                await VRChatUtil.AgreeCopyrightAgreement(avatarId);
                return true;
            }
            catch (Exception ex)
            {
                ShowError(
                    AcL10n.Tr("dialog.title.error"),
                    AcL10n.Tr("error.agreement_processing_failed", ex.Message),
                    ex);
                return false;
            }
        }

        private async Task ExecuteBuildAndUpload(
            IVRCSdkAvatarBuilderApi builder,
            GameObject avatarObject,
            VRCAvatar vrcAvatar)
        {
            var succeeded = false;
            try
            {
                await ExecuteBuildAndUploadCore(builder, avatarObject, vrcAvatar, BuildAndPublishProgressTitle);
                succeeded = true;
            }
            catch (OperationCanceledException)
            {
                // BuildAndPublishAvatarSafe 側で一元的にキャンセルメッセージを出す
                throw;
            }
            catch (Exception ex)
            {
                ShowError(AcL10n.Tr("dialog.title.error"), GetBuildExceptionMessage(ex), ex);
            }

            if (succeeded)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    AcL10n.Tr("dialog.title.info"),
                    AcL10n.Tr("info.upload_completed"),
                    AcL10n.Tr("dialog.button.ok"));
            }
        }

        // ビルド・アップロードのコア処理。進捗バー連携と SDK イベントハンドラの登録/解除のみを行い、
        // ダイアログ表示はしない。例外はそのまま伝播させ、呼び出し側 (単発/一括) が処理する。
        // progressTitle: 進捗バーに表示するタイトル (一括処理では "(i/N)" を含める)。
        // externalCancellation: 外部からキャンセルしたい場合に渡す (バッチ全体の中断などに使う)。
        private async Task ExecuteBuildAndUploadCore(
            IVRCSdkAvatarBuilderApi builder,
            GameObject avatarObject,
            VRCAvatar vrcAvatar,
            string progressTitle,
            CancellationToken externalCancellation = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellation);

            // ビルドフェーズは 50% 地点に固定（SDK が進捗率を提供しないため）
            const float BuildPhaseProgress = 0.55f;
            // アップロードフェーズは 70% → 100% にマッピング
            const float UploadPhaseStart = 0.70f;
            const float UploadPhaseRange = 0.30f;

            // SDK のイベントはバックグラウンドスレッドからも呼ばれる可能性があるため、
            // イベントハンドラでは共有状態の更新のみを行い、EditorUtility の API 呼び出しは
            // EditorApplication.update (必ずメインスレッド) 側に寄せる。
            var progressLock = new object();
            var progressMessage = AcL10n.Tr("progress.build_publish.starting_build");
            var progressValue = 0.50f;
            var progressDirty = true;

            EventHandler<string> onBuildProgress = (_, status) =>
            {
                lock (progressLock)
                {
                    progressMessage = AcL10n.Tr("progress.build_publish.building", status);
                    progressValue = BuildPhaseProgress;
                    progressDirty = true;
                }
            };

            EventHandler<(string status, float percentage)> onUploadProgress = (_, pg) =>
            {
                lock (progressLock)
                {
                    progressMessage = AcL10n.Tr("progress.build_publish.uploading", pg.status);
                    progressValue = UploadPhaseStart + Mathf.Clamp01(pg.percentage) * UploadPhaseRange;
                    progressDirty = true;
                }
            };

            void ProgressTick()
            {
                string message;
                float value;
                lock (progressLock)
                {
                    if (!progressDirty)
                    {
                        return;
                    }
                    message = progressMessage;
                    value = progressValue;
                    progressDirty = false;
                }

                if (EditorUtility.DisplayCancelableProgressBar(progressTitle, message, value))
                {
                    cts.Cancel();
                }
            }

            builder.OnSdkBuildProgress += onBuildProgress;
            builder.OnSdkUploadProgress += onUploadProgress;
            EditorApplication.update += ProgressTick;

            try
            {
                EditorUtility.DisplayCancelableProgressBar(
                    progressTitle,
                    AcL10n.Tr("progress.build_publish.starting_build"),
                    0.50f);

                await builder.BuildAndUpload(avatarObject, vrcAvatar, cancellationToken: cts.Token);
            }
            finally
            {
                EditorApplication.update -= ProgressTick;
                builder.OnSdkBuildProgress -= onBuildProgress;
                builder.OnSdkUploadProgress -= onUploadProgress;
            }
        }

        // ビルド・アップロード時の例外をローカライズ済みメッセージに変換する。
        // 単発/一括処理の双方で同じ翻訳ロジックを使うために共通化している。
        private static string GetBuildExceptionMessage(Exception ex)
        {
            return ex switch
            {
                BuildBlockedException e => AcL10n.Tr("error.build_blocked", e.Message),
                ValidationException e => AcL10n.Tr("error.validation_failed", e.Message),
                OwnershipException => AcL10n.Tr("error.not_owner"),
                BundleExistsException e => AcL10n.Tr("error.bundle_already_exists", e.Message),
                UploadException e => AcL10n.Tr("error.upload_failed", e.Message),
                BuilderException e => AcL10n.Tr("error.build_failed", e.Message),
                CopyrightOwnershipAgreementException e => AcL10n.Tr("error.agreement_required", e.Message),
                _ => AcL10n.Tr("error.build_upload_failed", ex.Message),
            };
        }

        private enum BulkBuildResultStatus
        {
            Succeeded,
            Failed,
            Cancelled,
            Skipped,
        }

        private struct BulkBuildResult
        {
            public AvatarDatabase.AvatarDatabaseEntry Avatar;
            public BulkBuildResultStatus Status;
            public string ErrorMessage;
        }

        // バッチ処理中のシーン切替時に未保存の変更をどう扱うかのポリシー。
        // - Ask: Unity 標準の保存確認ダイアログを毎回表示 (単発処理と同等の挙動)
        // - AutoSave: 開いている全シーンを自動保存してから切替
        // - Discard: 保存ダイアログを出さずに OpenScene Single で破棄
        private enum BulkScenePolicy
        {
            Ask,
            AutoSave,
            Discard,
        }

        // バッチ開始前にシーン保存ポリシーをユーザに確認する。
        // DisplayDialogComplex の戻り値: 0=ok, 1=cancel(Esc)/中央, 2=alt
        // Esc 押下時は Ask (= Unity 標準挙動 = 最も安全) にフォールバックさせるため、ok=AutoSave / cancel=Ask / alt=Discard の配置とする。
        private static BulkScenePolicy AskBulkScenePolicy()
        {
            var choice = EditorUtility.DisplayDialogComplex(
                AcL10n.Tr("dialog.title.bulk_scene_policy"),
                AcL10n.Tr("dialog.message.bulk_scene_policy"),
                AcL10n.Tr("dialog.button.bulk_scene_policy.auto_save"),
                AcL10n.Tr("dialog.button.bulk_scene_policy.ask"),
                AcL10n.Tr("dialog.button.bulk_scene_policy.discard"));

            return choice switch
            {
                0 => BulkScenePolicy.AutoSave,
                2 => BulkScenePolicy.Discard,
                _ => BulkScenePolicy.Ask,
            };
        }

        // 選択された複数アバターを順次ビルド＆アップロードするエントリポイント (例外集約)。
        // - ログイン/Builder取得は最初に1回のみ
        // - 著作権規約はバッチ開始時に1回まとめて確認
        // - 各アバターの失敗はサマリに記録して継続
        // - 進捗バーの Cancel で現在のアバターを即時中断し、残りはスキップ
        private async Task BulkBuildAndPublishAvatarsSafe(IReadOnlyList<AvatarDatabase.AvatarDatabaseEntry> avatars)
        {
            if (avatars == null || avatars.Count == 0)
            {
                return;
            }

            try
            {
                await BulkBuildAndPublishAvatars(avatars);
            }
            catch (Exception e)
            {
                // 想定外の例外 (前準備フェーズなど) を吸収して通知
                Debug.LogError(e);
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    AcL10n.Tr("dialog.title.error"),
                    AcL10n.Tr("error.unexpected_build_upload", e.Message),
                    AcL10n.Tr("dialog.button.ok"));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private async Task BulkBuildAndPublishAvatars(IReadOnlyList<AvatarDatabase.AvatarDatabaseEntry> avatars)
        {
            var totalCount = avatars.Count;
            var results = new List<BulkBuildResult>(totalCount);

            // Play Mode 中はシーン切替不可なのでバッチ開始前に弾く
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    AcL10n.Tr("dialog.title.info"),
                    AcL10n.Tr("info.play_mode_cannot_switch"),
                    AcL10n.Tr("dialog.button.ok"));
                return;
            }

            // === 前準備 (バッチで1回) ===
            VRChatUtil.ClearVRCSDKIssues();
            VRChatUtil.InitializeRemoteConfig();

            EditorUtility.DisplayProgressBar(
                AcL10n.Tr("progress.build_publish.bulk_title", 0, totalCount),
                AcL10n.Tr("progress.build_publish.logging_in"),
                0.05f);

            if (!await EnsureVRChatLoggedIn())
            {
                return;
            }

            if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder))
            {
                ShowError(
                    AcL10n.Tr("dialog.title.error"),
                    AcL10n.Tr("error.cannot_get_sdk_builder"));
                return;
            }

            // 著作権規約への一括同意 (1回のみ)
            EditorUtility.ClearProgressBar();
            if (!EditorUtility.DisplayDialog(
                AcL10n.Tr("dialog.title.confirm"),
                AcL10n.Tr("confirm.bulk_agreement", totalCount, VRCCopyrightAgreement.AgreementText),
                AcL10n.Tr("dialog.button.ok"),
                AcL10n.Tr("dialog.button.cancel")))
            {
                return;
            }

            // バッチ中のシーン保存挙動をユーザに選択させる (Esc/閉じるは Ask にフォールバック)
            var scenePolicy = AskBulkScenePolicy();

            // === 逐次ループ ===
            var cancelledMidBatch = false;
            for (var i = 0; i < totalCount; i++)
            {
                var avatar = avatars[i];
                var perAvatarTitle = AcL10n.Tr("progress.build_publish.bulk_title", i + 1, totalCount);

                try
                {
                    EditorUtility.DisplayProgressBar(
                        perAvatarTitle,
                        AcL10n.Tr("progress.build_publish.bulk_preparing", avatar.avatarObjectName),
                        0.05f);

                    if (!TryChangeToActiveAvatar(avatar, out var avatarObject, out var sceneError, scenePolicy))
                    {
                        results.Add(new BulkBuildResult
                        {
                            Avatar = avatar,
                            Status = BulkBuildResultStatus.Failed,
                            ErrorMessage = sceneError ?? AcL10n.Tr("error.scene_cannot_open", avatar.avatarObjectName),
                        });
                        continue;
                    }

                    if (!TryValidateAvatarSetup(avatarObject, avatar, out var pipelineManager, out var validationError))
                    {
                        results.Add(new BulkBuildResult
                        {
                            Avatar = avatar,
                            Status = BulkBuildResultStatus.Failed,
                            ErrorMessage = validationError,
                        });
                        continue;
                    }

                    EditorUtility.DisplayProgressBar(
                        perAvatarTitle,
                        AcL10n.Tr("progress.build_publish.getting_avatar_info"),
                        0.35f);

                    var (resolved, vrcAvatar, resolveError, resolveException) =
                        await TryResolveVRCAvatarCore(pipelineManager.blueprintId, avatar.avatarObjectName);
                    if (!resolved)
                    {
                        if (resolveException != null)
                        {
                            Debug.LogError(resolveException);
                        }
                        results.Add(new BulkBuildResult
                        {
                            Avatar = avatar,
                            Status = BulkBuildResultStatus.Failed,
                            ErrorMessage = resolveError,
                        });
                        continue;
                    }

                    // 規約同意 (バッチ同意済みなのでダイアログは出さず、未同意のものだけ API 呼び出し)
                    if (!VRChatUtil.AgreedContentThisSession.Contains(vrcAvatar.ID))
                    {
                        try
                        {
                            await VRChatUtil.AgreeCopyrightAgreement(vrcAvatar.ID);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError(ex);
                            results.Add(new BulkBuildResult
                            {
                                Avatar = avatar,
                                Status = BulkBuildResultStatus.Failed,
                                ErrorMessage = AcL10n.Tr("error.agreement_processing_failed", ex.Message),
                            });
                            continue;
                        }
                    }

                    await ExecuteBuildAndUploadCore(builder, avatarObject, vrcAvatar, perAvatarTitle);
                    results.Add(new BulkBuildResult
                    {
                        Avatar = avatar,
                        Status = BulkBuildResultStatus.Succeeded,
                    });
                }
                catch (OperationCanceledException)
                {
                    results.Add(new BulkBuildResult
                    {
                        Avatar = avatar,
                        Status = BulkBuildResultStatus.Cancelled,
                    });
                    // 残りは Skipped として記録
                    for (var j = i + 1; j < totalCount; j++)
                    {
                        results.Add(new BulkBuildResult
                        {
                            Avatar = avatars[j],
                            Status = BulkBuildResultStatus.Skipped,
                        });
                    }
                    cancelledMidBatch = true;
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                    results.Add(new BulkBuildResult
                    {
                        Avatar = avatar,
                        Status = BulkBuildResultStatus.Failed,
                        ErrorMessage = GetBuildExceptionMessage(ex),
                    });
                }
            }

            EditorUtility.ClearProgressBar();
            ShowBulkBuildSummary(results, cancelledMidBatch);
        }

        private static void ShowBulkBuildSummary(IReadOnlyList<BulkBuildResult> results, bool cancelledMidBatch)
        {
            var succeeded = 0;
            var failed = 0;
            var skipped = 0;
            foreach (var r in results)
            {
                switch (r.Status)
                {
                    case BulkBuildResultStatus.Succeeded: succeeded++; break;
                    case BulkBuildResultStatus.Failed: failed++; break;
                    case BulkBuildResultStatus.Cancelled:
                    case BulkBuildResultStatus.Skipped: skipped++; break;
                }
            }

            // 失敗・キャンセル・スキップしたアバターの一覧を本文に追記
            var problemLines = results
                .Where(r => r.Status != BulkBuildResultStatus.Succeeded)
                .Select(r =>
                {
                    var statusLabel = r.Status switch
                    {
                        BulkBuildResultStatus.Cancelled => "[Cancelled]",
                        BulkBuildResultStatus.Skipped => "[Skipped]",
                        _ => "[Failed]",
                    };
                    return string.IsNullOrEmpty(r.ErrorMessage)
                        ? $"- {statusLabel} {r.Avatar.avatarObjectName}"
                        : $"- {statusLabel} {r.Avatar.avatarObjectName}: {r.ErrorMessage}";
                })
                .ToList();

            string message;
            if (problemLines.Count == 0)
            {
                message = AcL10n.Tr("info.bulk_completed", succeeded, failed, skipped);
            }
            else
            {
                message = AcL10n.Tr("info.bulk_completed_with_failures",
                    succeeded, failed, skipped, string.Join("\n", problemLines));
            }

            if (cancelledMidBatch)
            {
                Debug.Log(AcL10n.Tr("info.bulk_cancelled_remaining", skipped));
            }

            EditorUtility.DisplayDialog(
                AcL10n.Tr("dialog.title.info"),
                message,
                AcL10n.Tr("dialog.button.ok"));
        }

        private static void ShowError(string title, string message, Exception ex = null)
        {
            EditorUtility.ClearProgressBar();
            if (ex != null)
            {
                Debug.LogError($"{title}: {message}\n{ex}");
            }
            else
            {
                Debug.LogError($"{title}: {message}");
            }
            EditorUtility.DisplayDialog(title, message, AcL10n.Tr("dialog.button.ok"));
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
            // データベース再構築でアバターGUIDが変動する可能性があるため、選択状態は破棄する
            _selectedAvatarGlobalObjectIds.Clear();
            _selectionAnchorAvatarGlobalObjectId = null;
            RefreshGridView();
        }

        private bool ChangeToActiveAvatar(AvatarDatabase.AvatarDatabaseEntry avatar, out GameObject avatarObject)
        {
            if (TryChangeToActiveAvatar(avatar, out avatarObject, out var errorMessage))
            {
                return true;
            }
            if (errorMessage != null)
            {
                ShowError(AcL10n.Tr("dialog.title.error"), errorMessage);
            }
            return false;
        }

        // ダイアログ表示を伴わないシーン切替コア。一括処理で個別にダイアログ表示せず、サマリへ集約するために使用する。
        // 戻り値が false で errorMessage が null の場合は Play Mode (続行不能) を意味する。
        // scenePolicy: バッチ時のシーン保存挙動。単発処理では Ask (= Unity 標準ダイアログ) を使う。
        private bool TryChangeToActiveAvatar(
            AvatarDatabase.AvatarDatabaseEntry avatar,
            out GameObject avatarObject,
            out string errorMessage,
            BulkScenePolicy scenePolicy = BulkScenePolicy.Ask)
        {
            avatarObject = null;
            errorMessage = null;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                errorMessage = AcL10n.Tr("info.play_mode_cannot_switch");
                return false;
            }

            avatarObject = ChangeSelectingObject(avatar, scenePolicy);
            if (avatarObject == null)
            {
                errorMessage = AcL10n.Tr("error.scene_cannot_open", avatar.avatarObjectName);
                return false;
            }

            EditorGUIUtility.PingObject(avatarObject);
            Debug.Log("Selected: " + avatarObject.name);
            return true;
        }

        private void GenerateAvatarThumbnail(AvatarDatabase.AvatarDatabaseEntry avatar)
        {
            var scenePath = AssetDatabase.GUIDToAssetPath(avatar.sceneAssetGuid);

            using var avatarRenderer = new AvatarRenderer();

            SceneProcessor.ProcessSceneTemporarily(scenePath, (scene) =>
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
            });
        }

        private void AddAvatarCatalogThumbnailSettingsComponent(AvatarDatabase.AvatarDatabaseEntry avatar)
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

                var component = avatarObject.GetComponent<AvatarThumbnailSettings>();
                if (component != null)
                {
                    return;
                }

                avatarObject.AddComponent<AvatarThumbnailSettings>();
                EditorUtility.SetDirty(avatarObject);

                EditorSceneManager.SaveScene(scene);
            });
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
            if (e.button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            if (e.clickCount == 2)
            {
                ChangeToActiveAvatar(avatar, out _);
                return;
            }

            HandleSelectionClick(avatar, e.ctrlKey || e.commandKey, e.shiftKey);
        }

        // Ctrl/Shift+クリックでマルチセレクトを構築する。
        // - 修飾キーなし: 単一選択 (アンカー更新)
        // - Ctrl: トグル選択 (アンカー更新)
        // - Shift: 直前のアンカーから対象までを範囲選択 (アンカーは更新しない)
        // 範囲はあくまで現在の _filteredAvatars 内のインデックスを基準とするため、フィルタ外の項目は範囲対象外。
        private void HandleSelectionClick(AvatarDatabase.AvatarDatabaseEntry avatar, bool ctrl, bool shift)
        {
            if (string.IsNullOrEmpty(avatar.avatarGlobalObjectId))
            {
                return;
            }

            if (shift && !string.IsNullOrEmpty(_selectionAnchorAvatarGlobalObjectId))
            {
                var anchorIndex = _filteredAvatars.FindIndex(a => a.avatarGlobalObjectId == _selectionAnchorAvatarGlobalObjectId);
                var targetIndex = _filteredAvatars.FindIndex(a => a.avatarGlobalObjectId == avatar.avatarGlobalObjectId);

                if (anchorIndex < 0 || targetIndex < 0)
                {
                    // アンカーが現在のフィルタに存在しない場合は単一選択にフォールバック
                    _selectedAvatarGlobalObjectIds.Clear();
                    _selectedAvatarGlobalObjectIds.Add(avatar.avatarGlobalObjectId);
                    _selectionAnchorAvatarGlobalObjectId = avatar.avatarGlobalObjectId;
                }
                else
                {
                    var start = Mathf.Min(anchorIndex, targetIndex);
                    var end = Mathf.Max(anchorIndex, targetIndex);
                    _selectedAvatarGlobalObjectIds.Clear();
                    for (var i = start; i <= end; i++)
                    {
                        _selectedAvatarGlobalObjectIds.Add(_filteredAvatars[i].avatarGlobalObjectId);
                    }
                    // Shift+Click ではアンカーを更新しない (連続範囲拡張のため)
                }
            }
            else if (ctrl)
            {
                // トグル: HashSet.Add は新規挿入時 true、既存時 false を返す
                if (!_selectedAvatarGlobalObjectIds.Add(avatar.avatarGlobalObjectId))
                {
                    _selectedAvatarGlobalObjectIds.Remove(avatar.avatarGlobalObjectId);
                }
                _selectionAnchorAvatarGlobalObjectId = avatar.avatarGlobalObjectId;
            }
            else
            {
                _selectedAvatarGlobalObjectIds.Clear();
                _selectedAvatarGlobalObjectIds.Add(avatar.avatarGlobalObjectId);
                _selectionAnchorAvatarGlobalObjectId = avatar.avatarGlobalObjectId;
            }

            _avatarGridListView?.RefreshItems();
        }

        // 現在のフィルタ結果に表示されている選択中アバターを、表示順で返す。
        // 一括ビルド時の対象集合を決定するために使用する (非表示アバターはビルド対象外)。
        private List<AvatarDatabase.AvatarDatabaseEntry> GetSelectedVisibleAvatars()
        {
            if (_selectedAvatarGlobalObjectIds.Count == 0 || _filteredAvatars == null)
            {
                return new List<AvatarDatabase.AvatarDatabaseEntry>();
            }

            return _filteredAvatars
                .Where(a => _selectedAvatarGlobalObjectIds.Contains(a.avatarGlobalObjectId))
                .ToList();
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