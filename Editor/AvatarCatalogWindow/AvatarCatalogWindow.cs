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

        [MenuItem("Tools/Avatar Catalog/Avatar List")]
        public static void ShowWindow()
        {
            var window = GetWindow<AvatarCatalogWindow>("Avatar List");
            window.minSize = DefaultMinWindowSize;
        }

        private void OnEnable()
        {
            _avatarCatalogDatabase = AvatarDatabase.Load();
            _avatarSearchIndex = AvatarSearchIndex.Load();
            _preferences = Preferences.Load();

            ApplyFromPreferences();
        }

        private void OnDisable()
        {
            // ウィンドウ非表示時・アセンブリリロード前に参照を解放し、キャッシュの無制限な膨張を防ぐ
            _thumbnailCache.Clear();
        }

        private void OnDestroy()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            _thumbnailCache.Clear();
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

            // Single モード切替で現在のシーンが閉じられる場合に備え、ユーザーに保存可否を確認する
            if (mode == OpenSceneMode.Single)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return false;
                }
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
                if (gridItem.userData is AvatarDatabase.AvatarDatabaseEntry avatar)
                {
                    e.menu.AppendAction("Switch to active", action => ChangeToActiveAvatar(avatar, out _));
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

        private const string BuildAndPublishProgressTitle = "アバターのビルド・アップロード";

        private async Task BuildAndPublishAvatarSafe(AvatarDatabase.AvatarDatabaseEntry avatar)
        {
            try
            {
                await BuildAndPublishAvatar(avatar);
            }
            catch (OperationCanceledException)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("情報", "アバターのアップロードをキャンセルしました。", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    "エラー",
                    $"アバターのビルドおよびアップロード中に予期しないエラーが発生しました。\n\n{e.Message}",
                    "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private async Task BuildAndPublishAvatar(AvatarDatabase.AvatarDatabaseEntry avatar)
        {
            EditorUtility.DisplayProgressBar(BuildAndPublishProgressTitle, "アバターを準備中...", 0.05f);

            if (!ChangeToActiveAvatar(avatar, out var avatarObject))
            {
                return;
            }

            VRChatUtil.ClearVRCSDKIssues();
            VRChatUtil.InitializeRemoteConfig();

            EditorUtility.DisplayProgressBar(BuildAndPublishProgressTitle, "VRChat にログイン中...", 0.15f);

            if (!await EnsureVRChatLoggedIn())
            {
                return;
            }

            EditorUtility.DisplayProgressBar(BuildAndPublishProgressTitle, "SDK ビルダーを取得中...", 0.25f);

            if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder))
            {
                ShowError(
                    "エラー",
                    "VRChat SDK のアバタービルダーを取得できませんでした。VRChat SDK が正しくインストールされているか確認してください。");
                return;
            }

            if (!ValidateAvatarSetup(avatarObject, avatar, out var pipelineManager))
            {
                return;
            }

            EditorUtility.DisplayProgressBar(BuildAndPublishProgressTitle, "アバター情報を取得中...", 0.35f);

            var (resolved, vrcAvatar) = await TryResolveVRCAvatar(pipelineManager.blueprintId, avatar.avatarObjectName);
            if (!resolved)
            {
                return;
            }

            EditorUtility.DisplayProgressBar(BuildAndPublishProgressTitle, "ライセンス規約を確認中...", 0.45f);

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
                    ShowError("エラー", "VRChat アカウントでログインしてください。");
                    return false;
                }
                return true;
            }
            catch (VRChatSessionExpiredException ex)
            {
                ShowError(
                    "エラー",
                    "VRChat のログインセッションが切れています。\nVRChat SDK のコントロールパネルから再度ログインしてください。",
                    ex);
                return false;
            }
            catch (Exception ex)
            {
                ShowError(
                    "エラー",
                    $"VRChat へのログイン中にエラーが発生しました。\n\n{ex.Message}",
                    ex);
                return false;
            }
        }

        private bool ValidateAvatarSetup(
            GameObject avatarObject,
            AvatarDatabase.AvatarDatabaseEntry avatar,
            out PipelineManager pipelineManager)
        {
            pipelineManager = avatarObject.GetComponent<PipelineManager>();
            if (pipelineManager == null)
            {
                ShowError(
                    "エラー",
                    $"アバター '{avatar.avatarObjectName}' に Pipeline Manager コンポーネントが見つかりませんでした。");
                return false;
            }

            if (string.IsNullOrEmpty(pipelineManager.blueprintId))
            {
                ShowError(
                    "エラー",
                    $"アバター '{avatar.avatarObjectName}' の Blueprint ID が設定されていません。VRChat SDK のコントロールパネルからアバター情報を設定してください。");
                return false;
            }

            return true;
        }

        private async Task<(bool ok, VRCAvatar vrcAvatar)> TryResolveVRCAvatar(string blueprintId, string avatarDisplayName)
        {
            try
            {
                var vrcAvatar = await VRCApi.GetAvatar(blueprintId, true);
                if (string.IsNullOrEmpty(vrcAvatar.ID))
                {
                    ShowError(
                        "エラー",
                        $"アバター '{avatarDisplayName}' はまだ VRChat にアップロードされていません。先に VRChat SDK のコントロールパネルから初回アップロードを行ってください。");
                    return (false, default);
                }
                return (true, vrcAvatar);
            }
            catch (ApiErrorException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                ShowError(
                    "エラー",
                    $"アバター '{avatarDisplayName}' はまだ VRChat にアップロードされていません。先に VRChat SDK のコントロールパネルから初回アップロードを行ってください。",
                    ex);
                return (false, default);
            }
            catch (ApiErrorException ex)
            {
                ShowError(
                    "エラー",
                    $"アバター情報の取得に失敗しました。\nHTTPステータス: {(int)ex.StatusCode} ({ex.StatusCode})\n\n{ex.Message}",
                    ex);
                return (false, default);
            }
            catch (TaskCanceledException ex)
            {
                ShowError(
                    "エラー",
                    $"VRChat サーバーとの通信に失敗しました。接続を確認して再度お試しください。\n\n{ex.Message}",
                    ex);
                return (false, default);
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

            if (!EditorUtility.DisplayDialog("Confirm", VRCCopyrightAgreement.AgreementText, "OK", "Cancel"))
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
                    "エラー",
                    $"ライセンス規約への同意処理でエラーが発生しました。\n\n{ex.Message}",
                    ex);
                return false;
            }
        }

        private async Task ExecuteBuildAndUpload(
            IVRCSdkAvatarBuilderApi builder,
            GameObject avatarObject,
            VRCAvatar vrcAvatar)
        {
            using var cts = new CancellationTokenSource();

            // ビルドフェーズは 50% 地点に固定（SDK が進捗率を提供しないため）
            const float BuildPhaseProgress = 0.55f;
            // アップロードフェーズは 70% → 100% にマッピング
            const float UploadPhaseStart = 0.70f;
            const float UploadPhaseRange = 0.30f;

            // SDK のイベントはバックグラウンドスレッドからも呼ばれる可能性があるため、
            // イベントハンドラでは共有状態の更新のみを行い、EditorUtility の API 呼び出しは
            // EditorApplication.update (必ずメインスレッド) 側に寄せる。
            var progressLock = new object();
            var progressMessage = "ビルドを開始中...";
            var progressValue = 0.50f;
            var progressDirty = true;

            EventHandler<string> onBuildProgress = (_, status) =>
            {
                lock (progressLock)
                {
                    progressMessage = $"ビルド中: {status}";
                    progressValue = BuildPhaseProgress;
                    progressDirty = true;
                }
            };

            EventHandler<(string status, float percentage)> onUploadProgress = (_, pg) =>
            {
                lock (progressLock)
                {
                    progressMessage = $"アップロード中: {pg.status}";
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

                if (EditorUtility.DisplayCancelableProgressBar(BuildAndPublishProgressTitle, message, value))
                {
                    cts.Cancel();
                }
            }

            builder.OnSdkBuildProgress += onBuildProgress;
            builder.OnSdkUploadProgress += onUploadProgress;
            EditorApplication.update += ProgressTick;

            var succeeded = false;
            try
            {
                EditorUtility.DisplayCancelableProgressBar(
                    BuildAndPublishProgressTitle,
                    "ビルドを開始中...",
                    0.50f);

                await builder.BuildAndUpload(avatarObject, vrcAvatar, cancellationToken: cts.Token);
                succeeded = true;
            }
            catch (OperationCanceledException)
            {
                // BuildAndPublishAvatarSafe 側で一元的にキャンセルメッセージを出す
                throw;
            }
            catch (BuildBlockedException ex)
            {
                ShowError("エラー", $"他の処理によりビルドが中断されました。\n\n{ex.Message}", ex);
            }
            catch (ValidationException ex)
            {
                ShowError(
                    "エラー",
                    $"アバターのバリデーションに失敗しました。VRChat SDK のコントロールパネルでエラー内容を確認してください。\n\n{ex.Message}",
                    ex);
            }
            catch (OwnershipException ex)
            {
                ShowError("エラー", "このアバターの所有者ではないため、アップロードできません。", ex);
            }
            catch (BundleExistsException ex)
            {
                ShowError(
                    "エラー",
                    $"このアバターは既に同じ内容がアップロードされています。\n\n{ex.Message}",
                    ex);
            }
            catch (UploadException ex)
            {
                ShowError("エラー", $"アバターのアップロードに失敗しました。\n\n{ex.Message}", ex);
            }
            catch (BuilderException ex)
            {
                ShowError("エラー", $"アバターのビルドに失敗しました。\n\n{ex.Message}", ex);
            }
            catch (CopyrightOwnershipAgreementException ex)
            {
                ShowError("エラー", $"ライセンス規約への同意が必要です。\n\n{ex.Message}", ex);
            }
            catch (Exception ex)
            {
                ShowError(
                    "エラー",
                    $"アバターのビルドおよびアップロード中にエラーが発生しました。\n\n{ex.Message}",
                    ex);
            }
            finally
            {
                EditorApplication.update -= ProgressTick;
                builder.OnSdkBuildProgress -= onBuildProgress;
                builder.OnSdkUploadProgress -= onUploadProgress;
            }

            if (succeeded)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("情報", "アバターのビルドおよびアップロードが完了しました。", "OK");
            }
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
            EditorUtility.DisplayDialog(title, message, "OK");
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

        private bool ChangeToActiveAvatar(AvatarDatabase.AvatarDatabaseEntry avatar, out GameObject avatarObject)
        {
            avatarObject = null;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("情報", "Play Mode実行中はアバターの切り替えは行えません", "OK");
                return false;
            }

            avatarObject = ChangeSelectingObject(avatar);
            if (avatarObject == null)
            {
                ShowError("エラー", $"アバター '{avatar.avatarObjectName}' が存在するシーンを開けませんでした。");
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
            if (e.button == (int)MouseButton.LeftMouse && e.clickCount == 2)
            {
                ChangeToActiveAvatar(avatar, out _);
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