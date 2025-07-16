using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using VRC.SDK3.Avatars.Components;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using MitarashiDango.AvatarCatalog.Runtime;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarMetadataEditorWindow : EditorWindow
    {
        private static readonly string _mainUxmlGuid = "b0a3fc0b034f9a14190f7e869708fcca";

        private ObjectField _avatarObjectField;
        private VisualElement _metadataEditorArea;
        private Button _createMetadataButton;
        private Button _deleteMetadataButton;
        private HelpBox _statusHelpBox;
        private HelpBox _warningMessageBox;
        private Button _syncAvatarGlobalIdButton;
        private Button _copyAvatarMetadataFileButton;

        private GameObject _currentTargetAvatar;
        private SerializedObject _serializedObject;

        [MenuItem("Tools/Avatar Catalog/Avatar Metadata Editor")]
        public static void ShowWindow()
        {
            ShowWindow(null);
        }

        [MenuItem("GameObject/Avatar Catalog/Open Avatar Metadata Editor", false, 0)]
        internal static void ShowWindowForObjectContextMenu()
        {
            ShowWindow(Selection.activeGameObject != null ? Selection.activeGameObject : null);
        }

        public static void ShowWindow(GameObject targetAvatar)
        {
            AvatarMetadataEditorWindow window = GetWindow<AvatarMetadataEditorWindow>("Avatar Metadata Editor");
            window.SetTargetAvatar(targetAvatar);
        }

        public void OnLostFocus()
        {
            if (_currentTargetAvatar != null && _serializedObject != null)
            {
                var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(_currentTargetAvatar);
                DatabaseBuilder.RefreshIndexes(globalObjectId);
            }
        }

        public void OnFocus()
        {
            var avatarMetadataSettings = _currentTargetAvatar?.GetComponent<AvatarMetadataSettings>();
            if (avatarMetadataSettings == null || avatarMetadataSettings.avatarMetadata == null)
            {
                HideFilenameMismatchUI();
                return;
            }

            SetMetadata(avatarMetadataSettings.avatarMetadata);

            if (IsLinkedAvatarObjectDifferent())
            {
                ShowFilenameMismatchUI();
            }
            else
            {
                HideFilenameMismatchUI();
            }
        }

        public void CreateGUI()
        {
            var mainUxmlAsset = MiscUtil.LoadVisualTreeAsset(_mainUxmlGuid);
            if (mainUxmlAsset == null)
            {
                Debug.LogError($"Cannot load UXML file");
                return;
            }

            mainUxmlAsset.CloneTree(rootVisualElement);

            var preferredFontFamilyName = FontCache.GetPreferredFontFamilyName();
            if (preferredFontFamilyName != "")
            {
                var fontAsset = FontCache.GetOrCreateFontAsset(preferredFontFamilyName);
                FontCache.ApplyFont(rootVisualElement, fontAsset);
            }

            // UI要素を取得
            _avatarObjectField = rootVisualElement.Q<ObjectField>("avatar-object-field");
            _metadataEditorArea = rootVisualElement.Q<VisualElement>("metadata-editor-area");
            _createMetadataButton = rootVisualElement.Q<Button>("create-metadata-button");
            _deleteMetadataButton = rootVisualElement.Q<Button>("delete-metadata-button");
            _statusHelpBox = rootVisualElement.Q<HelpBox>("status-helpbox");
            _warningMessageBox = rootVisualElement.Q<HelpBox>("warning-message-helpbox");
            _syncAvatarGlobalIdButton = rootVisualElement.Q<Button>("sync-avatar-global-id-button");
            _copyAvatarMetadataFileButton = rootVisualElement.Q<Button>("copy-avatar-metadata-file-button");

            // イベントハンドラを登録
            _avatarObjectField.objectType = typeof(GameObject);
            _avatarObjectField.RegisterValueChangedCallback(OnAvatarObjectChanged);
            _createMetadataButton.RegisterCallback<ClickEvent>((e) => OnCreateMetadataButtonClicked());
            _deleteMetadataButton.RegisterCallback<ClickEvent>((e) => OnDeleteMetadataButtonClicked());
            _syncAvatarGlobalIdButton.RegisterCallback<ClickEvent>(OnSyncAvatarGlobalIdButtonClick);
            _copyAvatarMetadataFileButton.RegisterCallback<ClickEvent>(OnCopyAvatarMetadataFileButtonClick);

            // 初期状態を設定
            if (_currentTargetAvatar != null)
            {
                SetTargetAvatar(_currentTargetAvatar);
            }

            HideHelpBox();
            UpdateUIState();

            if (IsLinkedAvatarObjectDifferent())
            {
                ShowFilenameMismatchUI();
            }
            else
            {
                HideFilenameMismatchUI();
            }

            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private void ShowFilenameMismatchUI()
        {
            _warningMessageBox.style.display = DisplayStyle.Flex;
            _syncAvatarGlobalIdButton.style.display = DisplayStyle.Flex;
            _copyAvatarMetadataFileButton.style.display = DisplayStyle.Flex;
        }

        private void HideFilenameMismatchUI()
        {
            _warningMessageBox.style.display = DisplayStyle.None;
            _syncAvatarGlobalIdButton.style.display = DisplayStyle.None;
            _copyAvatarMetadataFileButton.style.display = DisplayStyle.None;
        }

        private bool IsLinkedAvatarObjectDifferent()
        {
            if (_serializedObject == null || _currentTargetAvatar == null)
            {
                return false;
            }

            var avatarMetadataSettings = _currentTargetAvatar.GetComponent<AvatarMetadataSettings>();
            if (avatarMetadataSettings == null || avatarMetadataSettings.avatarMetadata == null)
            {
                return false;
            }

            var filePath = AssetDatabase.GetAssetPath(_serializedObject.targetObject);
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            var currentAvatarObjectId = GlobalObjectId.GetGlobalObjectIdSlow(_currentTargetAvatar);

            return currentAvatarObjectId.ToString() != avatarMetadataSettings.avatarMetadata.avatarGlobalObjectId;
        }

        private void OnSyncAvatarGlobalIdButtonClick(ClickEvent evt)
        {
            if (_serializedObject == null)
            {
                Debug.LogError("AvatarMetadata is not assigned.");
                return;
            }

            var avatarMetadataSettings = _currentTargetAvatar.GetComponent<AvatarMetadataSettings>();
            if (avatarMetadataSettings == null || avatarMetadataSettings.avatarMetadata == null)
            {
                return;
            }

            var avatarGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(_currentTargetAvatar);
            avatarMetadataSettings.avatarMetadata.avatarGlobalObjectId = avatarGlobalObjectId.ToString();

            EditorUtility.SetDirty(avatarMetadataSettings.avatarMetadata);
            AssetDatabase.SaveAssetIfDirty(avatarMetadataSettings.avatarMetadata);

            if (IsLinkedAvatarObjectDifferent())
            {
                ShowFilenameMismatchUI();
            }
            else
            {
                HideFilenameMismatchUI();
            }
        }

        private void OnCopyAvatarMetadataFileButtonClick(ClickEvent evt)
        {
            var avatarMetadataSettings = _currentTargetAvatar.GetComponent<AvatarMetadataSettings>();

            // 複製対象のアバターメタデータ
            if (_serializedObject == null)
            {
                Debug.LogError("AvatarMetadata is not assigned.");
                return;
            }

            var originalPath = AssetDatabase.GetAssetPath(_serializedObject.targetObject);
            if (string.IsNullOrEmpty(originalPath))
            {
                Debug.LogWarning("Original asset path is invalid.");
                return;
            }

            var newFilePath = AssetDatabase.GenerateUniqueAssetPath(AvatarMetadataUtil.GetMetadataPath($"tmp_{GUID.Generate()}"));

            AssetDatabase.CopyAsset(originalPath, newFilePath);
            AssetDatabase.Refresh();

            var fileGuid = AssetDatabase.AssetPathToGUID(newFilePath);
            AssetDatabase.RenameAsset(newFilePath, fileGuid);
            newFilePath = AssetDatabase.GUIDToAssetPath(fileGuid);

            var copiedAsset = AssetDatabase.LoadAssetAtPath<AvatarMetadata>(newFilePath);

            var avatarGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(_currentTargetAvatar);
            copiedAsset.avatarGlobalObjectId = avatarGlobalObjectId.ToString();

            EditorUtility.SetDirty(copiedAsset);
            AssetDatabase.SaveAssetIfDirty(copiedAsset);

            avatarMetadataSettings.avatarMetadata = copiedAsset;

            _serializedObject.ApplyModifiedProperties();

            SetMetadata(copiedAsset);

            if (IsLinkedAvatarObjectDifferent())
            {
                ShowFilenameMismatchUI();
            }
            else
            {
                HideFilenameMismatchUI();
            }
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            var preferredFontFamilyName = FontCache.GetPreferredFontFamilyName();
            if (preferredFontFamilyName != "")
            {
                var fontAsset = FontCache.GetOrCreateFontAsset(preferredFontFamilyName);
                FontCache.ApplyFont(rootVisualElement, fontAsset);
            }

            var avatarObject = _avatarObjectField.value as GameObject;
            if (avatarObject == null)
            {
                _currentTargetAvatar = null;
                UnsetMetadata();
            }

            UpdateUIState();
        }

        private void SetTargetAvatar(GameObject targetAvatar)
        {
            // ウィンドウ表示直後など、UI要素がまだ準備できていない場合への対応
            if (_avatarObjectField == null)
            {
                // 少し遅延させて再度実行を試みる
                EditorApplication.delayCall += () => SetTargetAvatar(targetAvatar);
                return;
            }

            _currentTargetAvatar = targetAvatar;
            _avatarObjectField.SetValueWithoutNotify(_currentTargetAvatar);
            LoadMetadataForCurrentAvatar();
            UpdateUIState();
        }

        private void OnAvatarObjectChanged(ChangeEvent<Object> evt)
        {
            if (_currentTargetAvatar != null && _serializedObject != null)
            {
                var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(_currentTargetAvatar);
                DatabaseBuilder.RefreshIndexes(globalObjectId);
            }

            var newTarget = evt.newValue as GameObject;

            UnsetMetadata(); // アバターが変わったらメタデータは一旦リセット

            _currentTargetAvatar = newTarget;
            if (_currentTargetAvatar != null && _currentTargetAvatar.GetComponent<VRCAvatarDescriptor>() != null)
            {
                LoadMetadataForCurrentAvatar(); // 変更後のアバターのメタデータをロード試行
            }

            UpdateUIState();
        }

        private void OnCreateMetadataButtonClicked()
        {
            if (_currentTargetAvatar != null && _serializedObject == null)
            {
                // VRC Avatar Descriptor があるか再度確認
                if (_currentTargetAvatar.GetComponent<VRCAvatarDescriptor>() == null)
                {
                    ShowHelpBoxMessage($"'{_currentTargetAvatar.name}' には VRC Avatar Descriptor がありません。", HelpBoxMessageType.Error);
                    UpdateUIState();
                    return;
                }

                var avatarMetadata = AvatarMetadataUtil.CreateMetadata(_currentTargetAvatar);
                if (avatarMetadata != null)
                {
                    SetMetadata(avatarMetadata);

                    var avatarCatalogDatabase = AvatarCatalogDatabase.Load();
                    if (avatarCatalogDatabase != null && avatarCatalogDatabase.IsExists(_currentTargetAvatar))
                    {
                        var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(_currentTargetAvatar);
                        var entry = avatarCatalogDatabase.Get(globalObjectId);
                        if (entry != null)
                        {
                            var path = AssetDatabase.GetAssetPath(avatarMetadata);
                            entry.avatarMetadataGuid = AssetDatabase.GUIDFromAssetPath(path).ToString();
                            AvatarCatalogDatabase.Save(avatarCatalogDatabase);
                        }
                    }
                }
                else
                {
                    ShowHelpBoxMessage("メタデータの作成に失敗しました。", HelpBoxMessageType.Error);
                }

                UpdateUIState();
            }
        }

        private void OnDeleteMetadataButtonClicked()
        {
            if (_currentTargetAvatar != null && _serializedObject != null)
            {
                var avatarMetadataSetting = _currentTargetAvatar.GetComponent<AvatarMetadataSettings>();
                if (avatarMetadataSetting.avatarMetadata == null)
                {
                    return;
                }

                string path = AssetDatabase.GetAssetPath(avatarMetadataSetting.avatarMetadata);
                if (EditorUtility.DisplayDialog("メタデータ削除の確認",
                                                $"アバター '{_currentTargetAvatar.name}' のメタデータファイルを削除しますか？\n({path})\nこの操作は取り消せません。",
                                                "削除", "キャンセル"))
                {
                    bool deleted = AvatarMetadataUtil.DeleteMetadata(_currentTargetAvatar);
                    if (deleted)
                    {
                        UnsetMetadata();

                        var avatarCatalogDatabase = AvatarCatalogDatabase.Load();
                        if (avatarCatalogDatabase != null && avatarCatalogDatabase.IsExists(_currentTargetAvatar))
                        {
                            var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(_currentTargetAvatar);
                            var entry = avatarCatalogDatabase.Get(globalObjectId);
                            if (entry != null)
                            {
                                entry.avatarMetadataGuid = "";
                                AvatarCatalogDatabase.Save(avatarCatalogDatabase);
                                AssetDatabase.Refresh();
                                DatabaseBuilder.RefreshIndexes(globalObjectId);
                            }
                        }
                    }
                    else
                    {
                        ShowHelpBoxMessage("メタデータの削除に失敗しました。", HelpBoxMessageType.Error);
                    }
                    UpdateUIState();
                }
            }
        }

        private void UnsetMetadata()
        {
            rootVisualElement.Unbind();
            _serializedObject = null;
            HideFilenameMismatchUI();
        }

        private void SetMetadata(AvatarMetadata avatarMetadata)
        {
            rootVisualElement.Unbind();
            if (avatarMetadata != null)
            {
                _serializedObject = new SerializedObject(avatarMetadata);
                rootVisualElement.Bind(_serializedObject);

                if (IsLinkedAvatarObjectDifferent())
                {
                    ShowFilenameMismatchUI();
                }
                else
                {
                    HideFilenameMismatchUI();
                }
            }
            else
            {
                HideFilenameMismatchUI();
            }
        }

        private void LoadMetadataForCurrentAvatar()
        {
            UnsetMetadata();

            if (_currentTargetAvatar != null)
            {
                var avatarMetadata = AvatarMetadataUtil.LoadMetadata(_currentTargetAvatar);
                if (avatarMetadata != null)
                {
                    SetMetadata(avatarMetadata);
                }
            }
        }

        private void UpdateUIState()
        {
            if (_avatarObjectField == null || _metadataEditorArea == null || _createMetadataButton == null || _deleteMetadataButton == null || _statusHelpBox == null)
            {
                return;
            }

            bool avatarSelected = _currentTargetAvatar != null;
            bool hasDescriptor = avatarSelected && _currentTargetAvatar.GetComponent<VRCAvatarDescriptor>() != null;
            bool metadataExists = _serializedObject != null;

            if (avatarSelected && !hasDescriptor)
            {
                ShowHelpBoxMessage($"'{_currentTargetAvatar.name}' には VRC Avatar Descriptor がありません。アバターを選択し直してください。", HelpBoxMessageType.Error);
            }
            else if (!avatarSelected)
            {
                ShowHelpBoxMessage("メタデータ編集対象となるアバターの GameObject を上のフィールドに設定してください。", HelpBoxMessageType.Info);
            }
            else if (avatarSelected && hasDescriptor && !metadataExists)
            {
                ShowHelpBoxMessage("このアバターのメタデータはまだ作成されていません。「メタデータ作成」ボタンを押してください。", HelpBoxMessageType.Info);
            }
            else
            {
                HideHelpBox();
            }

            // アバターが選択され、Descriptorがあり、メタデータも存在する場合のみ表示
            bool showMetadataContent = avatarSelected && hasDescriptor && metadataExists;
            _metadataEditorArea.style.display = showMetadataContent ? DisplayStyle.Flex : DisplayStyle.None;

            // 作成ボタン: アバター選択済み & Descriptorあり & メタデータなし
            bool canCreate = avatarSelected && hasDescriptor && !metadataExists;
            _createMetadataButton.style.display = canCreate ? DisplayStyle.Flex : DisplayStyle.None;
            _createMetadataButton.SetEnabled(canCreate);

            // 削除ボタン: メタデータが存在する (かつアバター選択済み & Descriptorありも暗黙的に満たされるはず)
            bool canDelete = metadataExists;
            _deleteMetadataButton.style.display = canDelete ? DisplayStyle.Flex : DisplayStyle.None;
            _deleteMetadataButton.SetEnabled(canDelete);
        }

        private void ShowHelpBoxMessage(string message, HelpBoxMessageType type)
        {
            if (_statusHelpBox != null)
            {
                _statusHelpBox.text = message;
                _statusHelpBox.messageType = type;
                _statusHelpBox.style.display = DisplayStyle.Flex;
            }
        }

        private void HideHelpBox()
        {
            if (_statusHelpBox != null)
            {
                _statusHelpBox.style.display = DisplayStyle.None;
            }
        }
    }
}