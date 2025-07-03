using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    [CustomEditor(typeof(AvatarMetadataSettings))]
    public class AvatarMetadataSettingsEditor : Editor
    {
        private static readonly string _mainUxmlGuid = "d66338f5f30a861429ec54f2a9272fae";

        private ObjectField _avatarMetadataObjectField;
        private HelpBox _warningMessageBox;
        private Button _syncAvatarGlobalIdButton;
        private Button _copyAvatarMetadataFileButton;

        private SerializedProperty _avatarMetadataProperty;

        public void OnEnable()
        {
            _avatarMetadataProperty = serializedObject.FindProperty("avatarMetadata");

            // イベント登録
            EditorSceneManager.sceneOpened -= OnSceneOpened; // 念のため一度解除
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        public void OnDestroy()
        {
            // イベント解除
            EditorSceneManager.sceneOpened -= OnSceneOpened;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var mainUxmlAsset = MiscUtil.LoadVisualTreeAsset(_mainUxmlGuid);
            if (mainUxmlAsset == null)
            {
                return new Label($"Cannot load UXML file");
            }

            var root = mainUxmlAsset.CloneTree();

            ApplyCustomFont(root);

            _avatarMetadataObjectField = root.Q<ObjectField>("avatar-metadata");
            _warningMessageBox = root.Q<HelpBox>("warning-message-helpbox");
            _syncAvatarGlobalIdButton = root.Q<Button>("sync-avatar-global-id-button");
            _copyAvatarMetadataFileButton = root.Q<Button>("copy-avatar-metadata-file-button");

            _avatarMetadataObjectField.RegisterValueChangedCallback(OnAvatarMetadataObjectFieldChanged);
            _syncAvatarGlobalIdButton.RegisterCallback<ClickEvent>(OnSyncAvatarGlobalIdButtonClick);
            _copyAvatarMetadataFileButton.RegisterCallback<ClickEvent>(OnCopyAvatarMetadataFileButtonClick);

            if (IsLinkedAvatarObjectDifferent(_avatarMetadataProperty.objectReferenceValue as AvatarMetadata))
            {
                ShowFilenameMismatchUI();
            }
            else
            {
                HideFilenameMismatchUI();
            }

            return root;
        }

        /// <summary>
        /// カスタムフォントを適用します
        /// </summary>
        private void ApplyCustomFont(VisualElement root)
        {
            var preferredFontFamilyName = FontCache.GetPreferredFontFamilyName();
            if (!string.IsNullOrEmpty(preferredFontFamilyName))
            {
                var fontAsset = FontCache.GetOrCreateFontAsset(preferredFontFamilyName);
                if (fontAsset != null) // FontAssetが取得できた場合のみ適用
                {
                    FontCache.ApplyFont(root, fontAsset);
                }
            }
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

        private bool IsLinkedAvatarObjectDifferent(AvatarMetadata avatarMetadata)
        {
            if (avatarMetadata == null)
            {
                return false;
            }

            var filePath = AssetDatabase.GetAssetPath(avatarMetadata);
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            var avatarMetadataSettings = (AvatarMetadataSettings)target;
            var currentAvatarObjectId = GlobalObjectId.GetGlobalObjectIdSlow(avatarMetadataSettings.gameObject);

            return currentAvatarObjectId.ToString() != avatarMetadata.avatarGlobalObjectId;
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            Repaint();
        }

        private void OnAvatarMetadataObjectFieldChanged(ChangeEvent<Object> evt)
        {
            var newAvatarMetadata = evt.newValue as AvatarMetadata;

            if (IsLinkedAvatarObjectDifferent(newAvatarMetadata))
            {
                ShowFilenameMismatchUI();
            }
            else
            {
                HideFilenameMismatchUI();
            }
        }

        private void OnSyncAvatarGlobalIdButtonClick(ClickEvent evt)
        {
            var avatarMetadata = _avatarMetadataProperty.objectReferenceValue as AvatarMetadata;
            if (avatarMetadata == null)
            {
                Debug.LogError("AvatarMetadata is not assigned.");
                return;
            }

            var avatarMetadataSettings = (AvatarMetadataSettings)target;
            var avatarGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(avatarMetadataSettings.gameObject);
            avatarMetadata.avatarGlobalObjectId = avatarGlobalObjectId.ToString();

            EditorUtility.SetDirty(avatarMetadata);
            AssetDatabase.SaveAssetIfDirty(avatarMetadata);

            if (IsLinkedAvatarObjectDifferent(avatarMetadata))
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
            var avatarMetadataSettings = (AvatarMetadataSettings)target;

            // 複製対象のアバターメタデータ
            var avatarMetadata = _avatarMetadataProperty.objectReferenceValue as AvatarMetadata;
            if (avatarMetadata == null)
            {
                Debug.LogError("AvatarMetadata is not assigned.");
                return;
            }

            var originalPath = AssetDatabase.GetAssetPath(avatarMetadata);
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

            var avatarGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(avatarMetadataSettings.gameObject);
            copiedAsset.avatarGlobalObjectId = avatarGlobalObjectId.ToString();

            EditorUtility.SetDirty(copiedAsset);
            AssetDatabase.SaveAssetIfDirty(copiedAsset);

            _avatarMetadataProperty.objectReferenceValue = copiedAsset;

            serializedObject.ApplyModifiedProperties();

            if (IsLinkedAvatarObjectDifferent(copiedAsset))
            {
                ShowFilenameMismatchUI();
            }
            else
            {
                HideFilenameMismatchUI();
            }
        }
    }
}
