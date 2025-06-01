using System.IO;
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
        private HelpBox _nameDifferenceWarningMessageBox;
        private Button _syncFileNameButton;
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
            _nameDifferenceWarningMessageBox = root.Q<HelpBox>("name-difference-warning-message-helpbox");
            _syncFileNameButton = root.Q<Button>("sync-filename-button");
            _copyAvatarMetadataFileButton = root.Q<Button>("copy-avatar-metadata-file-button");

            _avatarMetadataObjectField.RegisterValueChangedCallback(OnAvatarMetadataObjectFieldChanged);
            _syncFileNameButton.RegisterCallback<ClickEvent>(OnSyncFileNameButtonClick);
            _copyAvatarMetadataFileButton.RegisterCallback<ClickEvent>(OnCopyAvatarMetadataFileButtonClick);

            if (IsFileNameDifferent(_avatarMetadataProperty.objectReferenceValue as AvatarMetadata))
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
            _nameDifferenceWarningMessageBox.style.display = DisplayStyle.Flex;
            _syncFileNameButton.style.display = DisplayStyle.Flex;
            _copyAvatarMetadataFileButton.style.display = DisplayStyle.Flex;
        }

        private void HideFilenameMismatchUI()
        {
            _nameDifferenceWarningMessageBox.style.display = DisplayStyle.None;
            _syncFileNameButton.style.display = DisplayStyle.None;
            _copyAvatarMetadataFileButton.style.display = DisplayStyle.None;
        }

        private bool IsFileNameDifferent(AvatarMetadata avatarMetadata)
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

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);

            var avatarMetadataSettings = (AvatarMetadataSettings)target;

            return fileNameWithoutExtension != AvatarMetadataUtil.GenerateFileName(avatarMetadataSettings.gameObject);
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            Repaint();
        }

        private void OnAvatarMetadataObjectFieldChanged(ChangeEvent<Object> evt)
        {
            var newAvatarMetadata = evt.newValue as AvatarMetadata;

            if (IsFileNameDifferent(newAvatarMetadata))
            {
                ShowFilenameMismatchUI();
            }
            else
            {
                HideFilenameMismatchUI();
            }
        }

        private void OnSyncFileNameButtonClick(ClickEvent evt)
        {
            var avatarMetadata = _avatarMetadataProperty.objectReferenceValue as AvatarMetadata;
            if (avatarMetadata == null)
            {
                Debug.LogError("AvatarMetadata is not assigned.");
                return;
            }

            var filePath = AssetDatabase.GetAssetPath(avatarMetadata);
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("Failed to get the asset path of AvatarMetadata.");
                return;
            }

            var objectGuid = AssetDatabase.GUIDFromAssetPath(filePath);
            if (objectGuid.Empty())
            {
                Debug.LogError("The GUID of AvatarMetadata is invalid.");
                return;
            }

            var avatarMetadataSettings = (AvatarMetadataSettings)target;

            // 同名のファイルが存在していないかチェックする
            var newFilePath = AvatarMetadataUtil.GetMetadataPath(AvatarMetadataUtil.GenerateFileName(avatarMetadataSettings.gameObject));
            if (!AssetDatabase.GUIDFromAssetPath(newFilePath).Empty())
            {
                // 存在していたら警告メッセージを出して処理終了
                Debug.LogWarning($"A file with the same name already exists: {newFilePath}. Sync aborted.");
                EditorUtility.DisplayDialog("警告", "同名のファイルが既に存在しているため、ファイル名の同期を中止しました。", "OK");
                return;
            }

            AvatarMetadataUtil.RenameAvatarMetadataFile(objectGuid, avatarMetadataSettings.gameObject);

            if (IsFileNameDifferent(avatarMetadata))
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

            if (!IsFileNameDifferent(avatarMetadata))
            {
                Debug.LogWarning("Filename is matched.");
                return;
            }

            var originalPath = AssetDatabase.GetAssetPath(avatarMetadata);
            if (string.IsNullOrEmpty(originalPath))
            {
                Debug.LogWarning("Original asset path is invalid.");
                return;
            }

            // 同名のファイルが存在していないかチェックする
            var newFilePath = AvatarMetadataUtil.GetMetadataPath(AvatarMetadataUtil.GenerateFileName(avatarMetadataSettings.gameObject));
            if (!AssetDatabase.GUIDFromAssetPath(newFilePath).Empty())
            {
                // 存在していたら警告メッセージを出して処理終了
                Debug.LogWarning($"A file with the same name already exists: {newFilePath}. Copy aborted.");
                EditorUtility.DisplayDialog("警告", "同名のファイルが既に存在しているため、ファイルの複製を中止しました。", "OK");
                return;
            }

            AssetDatabase.CopyAsset(originalPath, newFilePath);
            AssetDatabase.Refresh();

            var copiedAsset = AssetDatabase.LoadAssetAtPath<AvatarMetadata>(newFilePath);
            _avatarMetadataProperty.objectReferenceValue = copiedAsset;

            serializedObject.ApplyModifiedProperties();
        }
    }
}
