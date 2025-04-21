using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    [CustomEditor(typeof(AvatarMetadataComponent))]
    public class AvatarMetadataComponentEditor : Editor
    {
        // --- Private Member Variables ---
        private VisualElement _rootElement;
        private AvatarMetadata _metadata;
        private GlobalObjectId _globalObjectId;
        private bool _isMetadataMissing = false;

        // UI Element References
        private Label _goidLabel;
        private HelpBox _infoBox;
        private Button _createMetadataButton;
        private VisualElement _editorContainer; // Holds comment and tag fields
        private TextField _commentField;
        private VisualElement _tagsContainer;
        private TextField _newTagField;
        private Button _addTagButton;
        private Button _saveMetadataButton;

        // State Tracking
        private TextField _currentlyEditingTagField = null; // 現在編集中のタグフィールド
        private bool _hasUnsavedChanges = false; // 未保存の変更があるか

        // --- Constants ---
        private const string InfoTextStyle = "font-style: italic; color: grey;";

        /// <summary>
        /// インスペクターのUIを構築・表示します。UI Elementsを使用します。
        /// </summary>
        public override VisualElement CreateInspectorGUI()
        {
            _rootElement = new VisualElement();
            _hasUnsavedChanges = false; // Reset change state
            _currentlyEditingTagField = null; // Reset editing state

            // 標準のインスペクター表示（AvatarMetadataComponent自体に表示するプロパティがあれば）
            // InspectorElement.FillDefaultInspector(_rootElement, serializedObject, this);

            // ターゲットの情報を取得
            var identifierComponent = (AvatarMetadataComponent)target;
            if (identifierComponent == null) return _rootElement; // Safety check
            GameObject targetGo = identifierComponent.gameObject;
            _globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(targetGo);

            // GlobalObjectId 表示
            _goidLabel = new Label($"Global Object ID: {_globalObjectId}") { style = { unityFontStyleAndWeight = FontStyle.Italic, color = Color.gray, marginBottom = 5 } };
            _rootElement.Add(_goidLabel);

            // メタデータのロード試行
            _metadata = AvatarMetadataUtil.LoadMetadata(_globalObjectId);
            _isMetadataMissing = (_metadata == null);

            // --- UI Elements Setup ---
            _infoBox = new HelpBox("", HelpBoxMessageType.None) { style = { display = DisplayStyle.None, marginTop = 5, marginBottom = 5 } };
            _rootElement.Add(_infoBox);

            // メタデータ作成ボタン
            _createMetadataButton = new Button(CreateMetadataAsset) { text = "Create Metadata Asset", style = { display = _isMetadataMissing ? DisplayStyle.Flex : DisplayStyle.None } };
            _rootElement.Add(_createMetadataButton);

            // メタデータ編集コンテナ (メタデータが存在する場合のみ表示)
            _editorContainer = new VisualElement { name = "metadata-editor-container", style = { display = !_isMetadataMissing ? DisplayStyle.Flex : DisplayStyle.None } };
            _rootElement.Add(_editorContainer);

            // -- Comment Section --
            _editorContainer.Add(new Label("Comment:") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            _commentField = new TextField() { multiline = true, value = _metadata?.comment ?? "" };
            _commentField.style.minHeight = 60; // Ensure decent height for multiline
            _commentField.RegisterValueChangedCallback(OnCommentChanged);
            _editorContainer.Add(_commentField);

            // -- Tags Section --
            _editorContainer.Add(new Label("Tags:") { style = { marginTop = 10, unityFontStyleAndWeight = FontStyle.Bold } });
            _tagsContainer = new VisualElement() { name = "tags-list", style = { marginLeft = 5, marginTop = 2 } }; // Indent tag list slightly
            _editorContainer.Add(_tagsContainer);

            // -- New Tag Input --
            var tagInputContainer = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginTop = 4, marginLeft = 5 } };
            _newTagField = new TextField() { style = { flexGrow = 1 } };
            // Add tag on Enter key press in the new tag field
            _newTagField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    AddTag();
                    evt.StopPropagation(); // Prevent other actions on Enter
                }
            });
            _addTagButton = new Button(AddTag) { text = "+", style = { width = 25 } }; // Make add button slightly wider
            tagInputContainer.Add(_newTagField);
            tagInputContainer.Add(_addTagButton);
            _editorContainer.Add(tagInputContainer);

            // -- Save Button (Explicit) --
            _saveMetadataButton = new Button(SaveChanges) { text = "Save Metadata Changes", style = { marginTop = 15, display = DisplayStyle.None } }; // Initially hidden
            _editorContainer.Add(_saveMetadataButton);

            // --- Initial Population ---
            RefreshTagElements(); // Populate existing tags
            UpdateInfoBox(); // Update info/warning messages

            return _rootElement;
        }

        /// <summary>
        /// メタデータアセットが存在しない場合に、新規作成を試みます。
        /// </summary>
        private void CreateMetadataAsset()
        {
            _currentlyEditingTagField = null; // Reset editing state
            bool created;
            _metadata = AvatarMetadataUtil.LoadOrCreateMetadata(_globalObjectId, out created);

            if (_metadata != null) // Successfully created or loaded existing
            {
                _isMetadataMissing = false;
                // Update UI visibility
                _createMetadataButton.style.display = DisplayStyle.None;
                _editorContainer.style.display = DisplayStyle.Flex;

                // Reset fields if newly created, otherwise load data
                _commentField.SetValueWithoutNotify(created ? "" : (_metadata.comment ?? ""));
                RefreshTagElements(); // Refresh tags (will be empty if new)
                _saveMetadataButton.style.display = DisplayStyle.None; // Hide save button initially
                _hasUnsavedChanges = created; // Mark as changed if newly created (to enable saving empty asset)
                if (_hasUnsavedChanges) MarkAsChanged(); // Show save button if needed

                Debug.Log(created ? $"Created metadata asset for {_globalObjectId}" : $"Loaded existing metadata for {_globalObjectId}");
            }
            else
            {
                // Failed to create/load
                Debug.LogError($"Failed to create or load metadata for {_globalObjectId}. Check permissions and path.");
                _infoBox.text = "Error: Could not create or load metadata asset.";
                _infoBox.messageType = HelpBoxMessageType.Error;
            }
            UpdateInfoBox(); // Update info box state
        }

        /// <summary>
        /// コメントフィールドの値が変更されたときに呼び出されます。
        /// </summary>
        private void OnCommentChanged(ChangeEvent<string> evt)
        {
            if (_metadata != null && _metadata.comment != evt.newValue)
            {
                Undo.RecordObject(_metadata, "Modify Avatar Comment"); // Register Undo
                _metadata.comment = evt.newValue;
                MarkAsChanged();
            }
        }

        /// <summary>
        /// 指定されたタグを表示するためのUI要素を作成し、コンテナに追加します。
        /// タグのダブルクリックによる編集開始ロジックも含まれます。
        /// </summary>
        /// <param name="tag">表示するタグ文字列</param>
        private void AddTagElement(string tag)
        {
            // Tag Element Container
            var tagElement = new VisualElement()
            {
                name = "tag-element",
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2,
                      backgroundColor = new Color(0.85f, 0.85f, 0.85f, 0.4f),  } // Slightly improved style
            };
            tagElement.userData = tag; // Store current tag string

            // Label (Clickable for editing)
            var tagLabel = new Label(tag) { name = "tag-label", style = { flexGrow = 1, marginRight = 5 } };
            tagLabel.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 2 && _currentlyEditingTagField == null)
                { // Double click to edit, only one at a time
                    StartEditingTag(tagElement, tagLabel);
                    evt.StopPropagation();
                }
            });

            // Remove Button
            var removeButton = new Button(() =>
            {
                if (_currentlyEditingTagField?.parent == tagElement) { CancelEditTag(tagElement, _currentlyEditingTagField); } // Cancel edit if active
                RemoveTag(tag);
            })
            {
                name = "tag-remove-button",
                text = "x",
                tooltip = "Remove Tag",
                style = { width = 18, height = 18, paddingLeft = 0, paddingRight = 0, marginLeft = 4, alignSelf = Align.Center }
            };

            // Edit Field (Initially Hidden)
            var editField = new TextField() { name = "tag-edit-field", value = tag, style = { flexGrow = 1, display = DisplayStyle.None, marginRight = 5 } };
            editField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) { CommitEditTag(tagElement, editField); evt.StopPropagation(); }
                else if (evt.keyCode == KeyCode.Escape) { CancelEditTag(tagElement, editField); evt.StopPropagation(); }
            });
            editField.RegisterCallback<BlurEvent>(evt =>
            {
                var relatedTargetButton = (evt.relatedTarget as Button);
                bool focusMovingToRemoveButton = relatedTargetButton != null && relatedTargetButton.name == "tag-remove-button" && relatedTargetButton.parent == tagElement;
                if (_currentlyEditingTagField == editField && !focusMovingToRemoveButton) { CommitEditTag(tagElement, editField); }
            });

            // Add elements to the container
            tagElement.Add(tagLabel);
            tagElement.Add(editField);
            tagElement.Add(removeButton);
            _tagsContainer.Add(tagElement);
        }

        /// <summary>
        /// タグリストのUIをクリアし、現在のメタデータに基づいて再構築します。
        /// </summary>
        private void RefreshTagElements()
        {
            _tagsContainer?.Clear();
            _currentlyEditingTagField = null; // Ensure editing state is cleared
            if (_metadata != null && _metadata.tags != null)
            {
                // Create a copy for safe iteration, although AddTagElement shouldn't modify the list here
                var tagsCopy = new List<string>(_metadata.tags);
                foreach (string tag in tagsCopy)
                {
                    AddTagElement(tag); // Calls the modified method with editing logic
                }
            }
        }

        /// <summary>
        /// 新規タグ入力フィールドの値に基づいて新しいタグを追加します。
        /// </summary>
        private void AddTag()
        {
            // Commit any ongoing edit before adding a new tag
            if (_currentlyEditingTagField != null)
            {
                CommitEditTag(_currentlyEditingTagField.parent, _currentlyEditingTagField);
                if (_currentlyEditingTagField != null) return; // Commit might fail validation
            }


            if (_metadata == null || string.IsNullOrWhiteSpace(_newTagField.value)) return;

            string newTag = _newTagField.value.Trim();

            // Prevent adding duplicate tags (case-sensitive comparison)
            if (!_metadata.tags.Any(t => t.Equals(newTag, System.StringComparison.Ordinal)))
            {
                Undo.RecordObject(_metadata, "Add Avatar Tag"); // Register Undo
                _metadata.tags.Add(newTag);
                AddTagElement(newTag); // Add UI element for the new tag
                _newTagField.value = ""; // Clear input field
                MarkAsChanged();
                Debug.Log($"Added tag: {newTag}");
            }
            else
            {
                Debug.LogWarning($"Tag '{newTag}' already exists.");
                // Optionally provide visual feedback (e.g., flash input field red)
                EditorApplication.Beep();
                _newTagField.Focus();
                _newTagField.SelectAll();
            }
            _newTagField.Focus(); // Keep focus on input field after adding
        }

        /// <summary>
        /// 指定されたタグをメタデータとUIから削除します。
        /// </summary>
        /// <param name="tagToRemove">削除するタグ文字列</param>
        private void RemoveTag(string tagToRemove)
        {
            // If the tag being removed is currently being edited, cancel the edit first.
            if (_currentlyEditingTagField != null && _currentlyEditingTagField.parent.userData as string == tagToRemove)
            {
                CancelEditTag(_currentlyEditingTagField.parent, _currentlyEditingTagField);
            }

            if (_metadata == null || !_metadata.tags.Contains(tagToRemove)) return;

            Undo.RecordObject(_metadata, "Remove Avatar Tag"); // Register Undo
            bool removed = _metadata.tags.Remove(tagToRemove);

            if (removed)
            {
                // Remove the corresponding UI element
                VisualElement tagElementToRemove = _tagsContainer.Children()
                    .FirstOrDefault(element => element.userData as string == tagToRemove);

                if (tagElementToRemove != null)
                {
                    _tagsContainer.Remove(tagElementToRemove);
                }
                MarkAsChanged();
                Debug.Log($"Removed tag: {tagToRemove}");
            }
        }

        /// <summary>
        /// 指定されたタグ要素の編集モードを開始します。
        /// </summary>
        /// <param name="tagElement">タグ要素のルートVisualElement</param>
        /// <param name="tagLabel">編集対象のタグを表示しているLabel</param>
        private void StartEditingTag(VisualElement tagElement, Label tagLabel)
        {
            // Commit or cancel any other ongoing edit first
            if (_currentlyEditingTagField != null && _currentlyEditingTagField.parent != tagElement)
            {
                VisualElement previousTagElement = _currentlyEditingTagField.parent;
                CommitEditTag(previousTagElement, _currentlyEditingTagField); // Attempt to commit previous edit
                if (_currentlyEditingTagField != null) return; // If commit failed validation, abort starting new edit
            }

            string currentTag = tagLabel.text;
            var editField = tagElement.Q<TextField>("tag-edit-field");
            var removeButton = tagElement.Q<Button>("tag-remove-button");

            tagLabel.style.display = DisplayStyle.None; // Hide label
            removeButton.style.display = DisplayStyle.None; // Hide remove button

            editField.SetValueWithoutNotify(currentTag); // Set current value
            editField.style.display = DisplayStyle.Flex; // Show edit field
            editField.Focus(); // Focus the field
            editField.SelectAll(); // Select text

            _currentlyEditingTagField = editField; // Track this field as being edited
        }

        /// <summary>
        /// タグ編集フィールドでの変更を検証し、問題なければコミット（保存）します。
        /// </summary>
        /// <param name="tagElement">タグ要素のルートVisualElement</param>
        /// <param name="editField">編集に使用されたTextField</param>
        private void CommitEditTag(VisualElement tagElement, TextField editField)
        {
            if (_currentlyEditingTagField != editField || _metadata == null) return; // Not editing this or no metadata

            string originalTag = tagElement.userData as string;
            string newTag = editField.value.Trim();

            bool isValid = true;
            string validationError = "";

            // Validation: Not empty
            if (string.IsNullOrWhiteSpace(newTag))
            {
                isValid = false;
                validationError = "Tag cannot be empty.";
            }
            // Validation: Not duplicate (unless it's the original tag)
            else if (newTag != originalTag && _metadata.tags.Any(t => t.Equals(newTag, System.StringComparison.Ordinal)))
            {
                isValid = false;
                validationError = $"Tag '{newTag}' already exists.";
            }

            if (isValid)
            {
                bool changed = false;
                if (newTag != originalTag) // Only modify if the tag actually changed
                {
                    Undo.RecordObject(_metadata, "Edit Avatar Tag"); // Register Undo
                    int index = _metadata.tags.IndexOf(originalTag);
                    if (index != -1)
                    {
                        _metadata.tags[index] = newTag;
                        tagElement.userData = newTag; // Update userData on the element
                        changed = true;
                        Debug.Log($"Tag '{originalTag}' changed to '{newTag}'");
                    }
                    else
                    {
                        Debug.LogError($"Original tag '{originalTag}' not found in metadata list during edit commit.");
                        isValid = false; // Treat as failed if original not found
                    }
                }

                // Finish editing visually, update label text
                FinishEditingTag(tagElement, editField, newTag);
                if (changed) MarkAsChanged(); // Mark changes if data was modified
            }
            else
            {
                // Edit failed validation
                Debug.LogWarning($"Invalid tag edit: {validationError}");
                EditorApplication.Beep(); // Alert user
                                          // Keep the edit field open and focused for correction
                editField.Focus();
                // Optionally: Add temporary visual feedback (e.g., red border) to the field
                // editField.AddToClassList("validation-error"); // Requires defining this style class
            }
        }

        /// <summary>
        /// タグの編集をキャンセルし、UIを元の状態に戻します。
        /// </summary>
        /// <param name="tagElement">タグ要素のルートVisualElement</param>
        /// <param name="editField">編集に使用されていたTextField</param>
        private void CancelEditTag(VisualElement tagElement, TextField editField)
        {
            if (_currentlyEditingTagField != editField) return; // Not editing this field

            string originalTag = tagElement.userData as string; // Get original tag from userData
            FinishEditingTag(tagElement, editField, originalTag); // Revert UI to original value
        }

        /// <summary>
        /// タグの編集モードを終了し、UI（ラベル、編集フィールド、削除ボタン）の状態をリセットします。
        /// </summary>
        /// <param name="tagElement">タグ要素のルートVisualElement</param>
        /// <param name="editField">編集に使用されていたTextField</param>
        /// <param name="finalTagValue">最終的にラベルに表示するタグ文字列</param>
        private void FinishEditingTag(VisualElement tagElement, TextField editField, string finalTagValue)
        {
            if (tagElement == null || editField == null) return; // Safety check

            var tagLabel = tagElement.Q<Label>("tag-label");
            var removeButton = tagElement.Q<Button>("tag-remove-button");

            editField.style.display = DisplayStyle.None; // Hide edit field

            if (tagLabel != null)
            {
                tagLabel.text = finalTagValue; // Update label text
                tagLabel.style.display = DisplayStyle.Flex; // Show label
            }
            if (removeButton != null)
            {
                removeButton.style.display = DisplayStyle.Flex; // Show remove button
            }

            if (_currentlyEditingTagField == editField)
            {
                _currentlyEditingTagField = null; // Clear editing state only if it was this field
            }
        }


        /// <summary>
        /// メタデータに変更があったことをマークし、保存ボタンを表示します。
        /// </summary>
        private void MarkAsChanged()
        {
            _hasUnsavedChanges = true;
            if (_saveMetadataButton != null) _saveMetadataButton.style.display = DisplayStyle.Flex; // Show save button

            // Mark the ScriptableObject dirty for standard Unity save behavior (optional, if SaveChanges isn't immediate)
            // if (_metadata != null)
            // {
            //     EditorUtility.SetDirty(_metadata);
            // }
        }

        /// <summary>
        /// インスペクター上部の情報ボックスの内容と表示状態を更新します。
        /// </summary>
        private void UpdateInfoBox()
        {
            if (_infoBox == null) return;

            if (_metadata != null && !_isMetadataMissing)
            {
                string assetPath = AssetDatabase.GetAssetPath(_metadata);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    _infoBox.text = $"Metadata Asset: {assetPath}";
                    _infoBox.messageType = HelpBoxMessageType.Info;
                    _infoBox.style.display = DisplayStyle.Flex;
                }
                else
                {
                    // Metadata loaded but path couldn't be determined (shouldn't normally happen for saved assets)
                    _infoBox.text = "Metadata loaded, but asset path is unclear.";
                    _infoBox.messageType = HelpBoxMessageType.Warning;
                    _infoBox.style.display = DisplayStyle.Flex;
                }
            }
            else if (_isMetadataMissing)
            {
                _infoBox.text = "Metadata asset not found for this avatar. Click 'Create' to make one.";
                _infoBox.messageType = HelpBoxMessageType.Warning;
                _infoBox.style.display = DisplayStyle.Flex;
            }
            else // Should not happen if logic is correct, but handle defensively
            {
                _infoBox.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// 'Save Metadata Changes' ボタンが押されたときに呼び出され、
        /// 保留中の変更をアセットファイルに保存します。
        /// </summary>
        private void SaveChanges()
        {
            // Commit any active tag edit before saving
            if (_currentlyEditingTagField != null)
            {
                CommitEditTag(_currentlyEditingTagField.parent, _currentlyEditingTagField);
                // If commit failed validation, should we abort the save? For now, we continue.
                if (_currentlyEditingTagField != null)
                {
                    Debug.LogWarning("Save aborted because tag edit validation failed.");
                    return; // Abort save if tag edit is still active (meaning commit failed)
                }
            }

            if (_metadata != null) // Ensure metadata exists
            {
                if (_hasUnsavedChanges)
                {
                    Debug.Log($"Saving metadata changes for {_globalObjectId}...");
                    AvatarMetadataUtil.SaveMetadata(_metadata); // Use utility to handle SetDirty and SaveAssets
                    _hasUnsavedChanges = false; // Reset flag
                    _saveMetadataButton.style.display = DisplayStyle.None; // Hide save button
                    Debug.Log("Metadata saved successfully.");
                }
                else
                {
                    Debug.Log("No unsaved changes to save.");
                    _saveMetadataButton.style.display = DisplayStyle.None; // Ensure button is hidden if no changes
                }
            }
            else
            {
                Debug.LogWarning("Cannot save, metadata object is null.");
            }
        }

        void OnDisable()
        {
            if (_hasUnsavedChanges && _metadata != null)
            {
                if (EditorUtility.DisplayDialog("Unsaved Metadata Changes",
                    $"There are unsaved changes for avatar '{((Component)target)?.gameObject.name}'. Save now?",
                    "Save", "Discard"))
                {
                    SaveChanges();
                }
            }
            // Clear references
            _currentlyEditingTagField = null;
            _rootElement = null;
            _metadata = null;
        }
    }
}
