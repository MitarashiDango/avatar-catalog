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
        private static readonly int IMAGE_SIZE = 192;
        private static readonly int ROW_SPACING = 10;
        private static readonly int MIN_COLUMN_SPACING = 10;
        private static readonly float DOUBLE_CLICK_THRESHOLD = 0.3f;
        private static readonly char[] SEARCH_WORDS_DELIMITER_CHARS = { ' ' };

        private AvatarRenderer _avatarRenderer;
        private AvatarCatalog _avatarCatalog;
        private AvatarThumbnailCacheDatabase _avatarThumbnailCacheDatabase;

        private VisualElement _avatarsListViewContainer;
        private VisualElement _firstSetupContainer;
        private VisualElement _playModeContainer;
        private VisualElement _gridContainer;
        private List<string> _searchWords = new List<string>();

        private float _lastClickTime = 0f;

        [MenuItem("Tools/Avatar Catalog/Avatars List")]
        public static void ShowWindow()
        {
            var window = GetWindow<AvatarCatalogWindow>("Avatars List");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
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
                }
                else if (!avatarObject.activeSelf)
                {
                    avatarObject.SetActive(true);
                }
            }

            return targetAvatarObject;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();

            _avatarsListViewContainer = new VisualElement();
            root.Add(_avatarsListViewContainer);

            _firstSetupContainer = new VisualElement();
            root.Add(_firstSetupContainer);

            _playModeContainer = new VisualElement();
            root.Add(_playModeContainer);

            CreateAvatarsListViewContainer();
            CreateFirstSetupContainer();
            CreatePlayModeContainer();

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ShowPlayModeContainer();
            }
            else if (_avatarCatalog != null)
            {
                ShowAvatarsListViewContainer();
            }
            else
            {
                ShowFirstSetupContainer();
            }
        }

        private void CreatePlayModeContainer()
        {
            var root = _playModeContainer;
            root.Add(new Label("Play Modeまたはビルド中は操作できません"));
        }

        private void ShowPlayModeContainer()
        {
            _avatarsListViewContainer.style.display = DisplayStyle.None;
            _firstSetupContainer.style.display = DisplayStyle.None;
            _playModeContainer.style.display = DisplayStyle.Flex;
        }

        private void CreateFirstSetupContainer()
        {
            var root = _firstSetupContainer;
            root.Add(new Label("初回セットアップが完了していません"));
            var initialSetupButton = new Button(() => OnFirstTimeSetupButton_Click())
            {
                tooltip = "初回セットアップ処理を実行します",
                text = "初回セットアップする",
            };
            root.Add(initialSetupButton);
        }

        private void ShowFirstSetupContainer()
        {
            _avatarsListViewContainer.style.display = DisplayStyle.None;
            _firstSetupContainer.style.display = DisplayStyle.Flex;
            _playModeContainer.style.display = DisplayStyle.None;
        }

        private void CreateAvatarsListViewContainer()
        {
            var root = _avatarsListViewContainer;
            root.style.flexDirection = FlexDirection.Column;

            if (_avatarCatalog == null)
            {
                LoadAssetFiles();
            }

            var headerContainer = new VisualElement();
            headerContainer.style.flexDirection = FlexDirection.Row;
            headerContainer.style.justifyContent = Justify.SpaceBetween;
            headerContainer.style.paddingTop = 2;
            headerContainer.style.paddingBottom = 2;

            var searchIcon = new Image();
            searchIcon.image = EditorGUIUtility.IconContent("Search On Icon").image;
            searchIcon.style.width = 16;
            searchIcon.style.height = 16;
            headerContainer.Add(searchIcon);

            var searchTextField = new TextField();
            searchTextField.style.flexGrow = 1;
            searchTextField.RegisterValueChangedCallback(evt =>
            {
                _searchWords.Clear();
                if (evt.newValue.Length > 0)
                {
                    var value = evt.newValue.ToLower();
                    _searchWords.AddRange(value.Split(SEARCH_WORDS_DELIMITER_CHARS));
                }
                UpdateGridLayout();
            });

            headerContainer.Add(searchTextField);

            var reloadButton = new Button(() =>
            {
                OnReloadAvatarListButton_Click();
            })
            {
                tooltip = "アバター一覧を更新"
            };

            reloadButton.style.width = 16;
            reloadButton.style.height = 16;
            reloadButton.style.borderBottomWidth = 0;
            reloadButton.style.borderTopWidth = 0;
            reloadButton.style.borderLeftWidth = 0;
            reloadButton.style.borderRightWidth = 0;
            reloadButton.style.paddingBottom = 0;
            reloadButton.style.paddingTop = 0;
            reloadButton.style.paddingLeft = 0;
            reloadButton.style.paddingRight = 0;
            reloadButton.style.backgroundColor = new StyleColor(Color.clear);
            reloadButton.style.alignSelf = Align.FlexEnd;

            var reloadIcon = new Image();
            reloadIcon.image = EditorGUIUtility.IconContent("Refresh").image;
            reloadIcon.style.width = 16;
            reloadIcon.style.height = 16;

            reloadButton.Add(reloadIcon);
            headerContainer.Add(reloadButton);

            root.Add(headerContainer);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            root.Add(scrollView);

            _gridContainer = new VisualElement();
            _gridContainer.style.flexDirection = FlexDirection.Row;
            _gridContainer.style.flexWrap = Wrap.Wrap;
            _gridContainer.style.justifyContent = Justify.SpaceAround;
            scrollView.Add(_gridContainer);

            root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void UpdateGridLayout()
        {
            _gridContainer.Clear();

            var scrollViewWidth = rootVisualElement.contentRect.width;
            if (float.IsNaN(scrollViewWidth) || scrollViewWidth <= 0)
            {
                return;
            }

            var avatars = _avatarCatalog.avatars;
            if (_searchWords.Count > 0)
            {
                avatars = avatars.Where(avatar => _searchWords.All(word => avatar.avatarName.ToLower().IndexOf(word) != -1)).ToList();
            }

            var maxColumns = Mathf.Max(1, Mathf.FloorToInt((scrollViewWidth - (MIN_COLUMN_SPACING * 2)) / IMAGE_SIZE));
            var totalItems = avatars.Count;
            var rows = Mathf.CeilToInt((float)totalItems / maxColumns);

            if (totalItems == 0)
            {
                _gridContainer.Add(new Label("アバターが見つかりません"));
                return;
            }

            var totalRowWidth = maxColumns * 192;
            var spaceBetween = (scrollViewWidth - totalRowWidth) / (maxColumns + 1);

            for (var row = 0; row < rows; row++)
            {
                var startIndex = row * maxColumns;
                var endIndex = Mathf.Min(startIndex + maxColumns, totalItems);
                var itemCountInRow = endIndex - startIndex;

                var rowContainer = new VisualElement();
                rowContainer.style.flexDirection = FlexDirection.Row;
                rowContainer.style.flexWrap = Wrap.NoWrap;
                rowContainer.style.width = scrollViewWidth - (MIN_COLUMN_SPACING * 2) - spaceBetween;
                rowContainer.style.justifyContent = Justify.SpaceBetween;
                rowContainer.style.marginBottom = ROW_SPACING;

                for (var i = startIndex; i < endIndex; i++)
                {
                    var currentAvatar = avatars[i];
                    if (!GlobalObjectId.TryParse(currentAvatar.globalObjectId, out var avatarGlobalObjectId))
                    {
                        Debug.LogWarning("failed to try parse avatar GlobalObjectId");
                        continue;
                    }

                    var itemContainer = new VisualElement();
                    itemContainer.style.width = IMAGE_SIZE;
                    itemContainer.style.height = IMAGE_SIZE;
                    itemContainer.style.marginTop = ROW_SPACING;
                    itemContainer.style.alignItems = Align.Center;
                    itemContainer.style.justifyContent = Justify.Center;

                    var thumbnailTexture = _avatarThumbnailCacheDatabase.TryGetCachedAvatarThumbnailImage(avatarGlobalObjectId);

                    var image = new Image();
                    if (thumbnailTexture != null)
                    {
                        image.image = thumbnailTexture;
                    }
                    image.scaleMode = ScaleMode.ScaleToFit;
                    image.style.width = IMAGE_SIZE;
                    image.style.height = IMAGE_SIZE;
                    itemContainer.Add(image);

                    var label = new Label(currentAvatar.avatarName);
                    label.style.marginTop = 5;
                    label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    label.style.whiteSpace = WhiteSpace.Normal;
                    itemContainer.Add(label);

                    itemContainer.RegisterCallback<ClickEvent>((e) =>
                    {
                        if (e.button == (int)MouseButton.LeftMouse)
                        {
                            var currentTime = Time.realtimeSinceStartup;
                            if (currentTime - _lastClickTime < DOUBLE_CLICK_THRESHOLD)
                            {
                                OnAvatarItem_Click(currentAvatar);
                            }

                            _lastClickTime = currentTime;
                        }
                    });

                    rowContainer.Add(itemContainer);
                }

                // ダミー要素を追加
                if (row == rows - 1)
                {
                    int emptySlots = maxColumns - itemCountInRow;
                    for (int j = 0; j < emptySlots; j++)
                    {
                        var dummyItem = new VisualElement();
                        dummyItem.style.width = 192;
                        dummyItem.style.height = 192;
                        rowContainer.Add(dummyItem);
                    }
                }

                _gridContainer.Add(rowContainer);
            }
        }

        private void ShowAvatarsListViewContainer()
        {
            _avatarsListViewContainer.style.display = DisplayStyle.Flex;
            _firstSetupContainer.style.display = DisplayStyle.None;
            _playModeContainer.style.display = DisplayStyle.None;
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
            UpdateGridLayout();

            if (_avatarCatalog != null)
            {
                ShowAvatarsListViewContainer();
            }
            else
            {
                ShowFirstSetupContainer();
            }
        }

        private void OnReloadAvatarListButton_Click()
        {
            RefreshAvatars();
            UpdateGridLayout();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ShowPlayModeContainer();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                if (_avatarCatalog != null)
                {
                    ShowAvatarsListViewContainer();
                }
                else
                {
                    ShowFirstSetupContainer();
                }
            }
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
