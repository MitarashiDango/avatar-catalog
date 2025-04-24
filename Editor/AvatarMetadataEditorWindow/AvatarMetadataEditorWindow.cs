using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarMetadataEditorWindow : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset mainUxmlAsset;

        [SerializeField]
        private StyleSheet mainUssAsset;

        private ObjectField _avatarObjectField;
        private VisualElement _metadataEditorArea;
        private TextField _commentField;
        private VisualElement _tagsContainer;
        private Button _addTagButton;
        private Button _createMetadataButton;
        private Button _deleteMetadataButton;
        private HelpBox _statusHelpBox;

        private GameObject _currentTargetAvatar;
        private AvatarMetadata _currentMetadata;

        [MenuItem("Tools/Avatar Catalog/Avatar Metadata Editor")]
        public static void ShowWindow()
        {
            ShowWindow(null);
        }

        [MenuItem("GameObject/Avatar Catalog/Avatar Metadata Editor", false, 0)]
        internal static void ShowWindowForObjectContextMenu()
        {
            ShowWindow(Selection.activeGameObject != null ? Selection.activeGameObject : null);
        }

        public static void ShowWindow(GameObject targetAvatar)
        {
            AvatarMetadataEditorWindow window = GetWindow<AvatarMetadataEditorWindow>("Avatar Metadata Editor");
            window.SetTargetAvatar(targetAvatar);
        }

        public void CreateGUI()
        {
            LoadUxmlAndUss();

            var preferredFontFamilyName = FontCache.GetPreferredFontFamilyName();
            if (preferredFontFamilyName != "")
            {
                var fontAsset = FontCache.GetOrCreateFontAsset(preferredFontFamilyName);
                FontCache.ApplyFont(rootVisualElement, fontAsset);
            }

            // UI要素を取得
            _avatarObjectField = rootVisualElement.Q<ObjectField>("avatarObjectField");
            _metadataEditorArea = rootVisualElement.Q<VisualElement>("metadataEditorArea");
            _commentField = rootVisualElement.Q<TextField>("commentField");
            _tagsContainer = rootVisualElement.Q<VisualElement>("tagsContainer");
            _addTagButton = rootVisualElement.Q<Button>("addTagButton");
            _createMetadataButton = rootVisualElement.Q<Button>("createMetadataButton");
            _deleteMetadataButton = rootVisualElement.Q<Button>("deleteMetadataButton");
            _statusHelpBox = rootVisualElement.Q<HelpBox>("statusHelpBox");

            // イベントハンドラを登録
            _avatarObjectField.objectType = typeof(GameObject);
            _avatarObjectField.RegisterValueChangedCallback(OnAvatarObjectChanged);
            _commentField.RegisterValueChangedCallback(OnCommentChanged);
            _addTagButton.RegisterCallback<ClickEvent>((e) => OnAddTagButtonClicked());
            _createMetadataButton.RegisterCallback<ClickEvent>((e) => OnCreateMetadataButtonClicked());
            _deleteMetadataButton.RegisterCallback<ClickEvent>((e) => OnDeleteMetadataButtonClicked());

            // 初期状態を設定
            HideHelpBox();
            UpdateUIState();

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnEditorUpdate()
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
                _currentMetadata = null;
                _currentTargetAvatar = null;
            }

            UpdateUIState();
        }

        private void LoadUxmlAndUss()
        {
            if (mainUxmlAsset != null)
            {
                mainUxmlAsset.CloneTree(rootVisualElement);
            }
            else
            {
                Debug.LogError($"Cannot load UXML file");
                return;
            }

            if (mainUssAsset != null)
            {
                rootVisualElement.styleSheets.Add(mainUssAsset);
            }
            else
            {
                Debug.LogWarning($"Cannot load USS file. Using default styles.");
            }
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
            var newTarget = evt.newValue as GameObject;
            _currentMetadata = null; // アバターが変わったらメタデータは一旦リセット

            _currentTargetAvatar = newTarget;
            if (_currentTargetAvatar != null && _currentTargetAvatar.GetComponent<VRCAvatarDescriptor>() != null)
            {
                LoadMetadataForCurrentAvatar(); // 変更後のアバターのメタデータをロード試行
            }

            UpdateUIState();
        }

        private void OnCommentChanged(ChangeEvent<string> evt)
        {
            if (_currentMetadata != null && _currentMetadata.comment != evt.newValue)
            {
                _currentMetadata.comment = evt.newValue;
                MarkDirty();
            }
        }

        private void OnAddTagButtonClicked()
        {
            if (_currentMetadata != null)
            {
                _currentMetadata.tags.Add("");
                CreateTagField("", _currentMetadata.tags.Count - 1);
                MarkDirty();
            }
        }

        private void OnCreateMetadataButtonClicked()
        {
            if (_currentTargetAvatar != null && _currentMetadata == null)
            {
                // VRC Avatar Descriptor があるか再度確認
                if (_currentTargetAvatar.GetComponent<VRCAvatarDescriptor>() == null)
                {
                    ShowHelpBoxMessage("'{_currentTargetAvatar.name}' には VRCAvatarDescriptor がありません。", HelpBoxMessageType.Error);
                    UpdateUIState();
                    return;
                }

                _currentMetadata = AvatarMetadataUtil.CreateMetadata(_currentTargetAvatar);
                if (_currentMetadata != null)
                {
                    PopulateUIWithMetadata();
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
            if (_currentTargetAvatar != null && _currentMetadata != null)
            {
                string path = AssetDatabase.GetAssetPath(_currentMetadata);
                if (EditorUtility.DisplayDialog("メタデータ削除の確認",
                                                $"アバター '{_currentTargetAvatar.name}' のメタデータファイルを削除しますか？\n({path})\nこの操作は取り消せません。",
                                                "削除", "キャンセル"))
                {
                    bool deleted = AvatarMetadataUtil.DeleteMetadata(_currentTargetAvatar);
                    if (deleted)
                    {
                        _currentMetadata = null;
                        PopulateUIWithMetadata();
                    }
                    else
                    {
                        ShowHelpBoxMessage("メタデータの削除に失敗しました。", HelpBoxMessageType.Error);
                    }
                    UpdateUIState();
                }
            }
        }

        private void LoadMetadataForCurrentAvatar()
        {
            _currentMetadata = null;
            if (_currentTargetAvatar != null)
            {
                _currentMetadata = AvatarMetadataUtil.LoadMetadata(_currentTargetAvatar);
            }

            PopulateUIWithMetadata();
        }

        private void PopulateUIWithMetadata()
        {
            if (_currentMetadata != null)
            {
                _commentField.SetValueWithoutNotify(_currentMetadata.comment);

                _tagsContainer.Clear();
                for (int i = 0; i < _currentMetadata.tags.Count; i++)
                {
                    CreateTagField(_currentMetadata.tags[i], i);
                }
            }
            else
            {
                _commentField.SetValueWithoutNotify("");
                _tagsContainer.Clear();
            }
        }

        private void CreateTagField(string tagValue, int index)
        {
            var tagRow = new VisualElement();
            tagRow.AddToClassList("tag-row");

            var tagField = new TextField();
            tagField.value = tagValue;
            tagField.AddToClassList("tag-field");
            tagField.RegisterValueChangedCallback(evt => OnTagChanged(evt, index));
            tagRow.Add(tagField);

            var deleteButton = new Button(() => DeleteTag(index));
            deleteButton.text = "x";
            deleteButton.AddToClassList("tag-delete-button");
            tagRow.Add(deleteButton);

            _tagsContainer.Add(tagRow);
        }

        private void OnTagChanged(ChangeEvent<string> evt, int index)
        {
            if (_currentMetadata != null)
            {
                _currentMetadata.tags[index] = evt.newValue;
                MarkDirty();
            }
        }

        private void DeleteTag(int index)
        {
            if (_currentMetadata != null && index < _currentMetadata.tags.Count)
            {
                _currentMetadata.tags.RemoveAt(index);
                PopulateUIWithMetadata();
                MarkDirty();
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
            bool metadataExists = _currentMetadata != null;

            if (avatarSelected && !hasDescriptor)
            {
                ShowHelpBoxMessage($"'{_currentTargetAvatar.name}' には VRCAvatarDescriptor がありません。アバターを選択し直してください。", HelpBoxMessageType.Error);
            }
            else if (!avatarSelected)
            {
                ShowHelpBoxMessage("対象となるアバターの GameObject を上のフィールドに設定してください。", HelpBoxMessageType.Info);
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

        private void MarkDirty()
        {
            if (_currentMetadata != null)
            {
                EditorUtility.SetDirty(_currentMetadata);
            }
        }

        private void OnDestroy()
        {
            if (_currentMetadata != null && EditorUtility.IsDirty(_currentMetadata))
            {
                Debug.Log("Saving metadata on window close...");
                AssetDatabase.SaveAssets(); // 閉じる際には確実に保存
            }
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