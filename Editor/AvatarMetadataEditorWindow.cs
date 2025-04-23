using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarMetadataEditorWindow : EditorWindow
    {
        private VisualElement _rootElement;
        private AvatarMetadata _currentMetadata;
        private GameObject _selectedGameObject;
        private GlobalObjectId _selectedGoid;
        private TextField _currentlyEditingTagField = null;

        private Label _headerLabel;
        private HelpBox _infoBox;
        private VisualElement _editorContainer;
        private TextField _commentField;
        private VisualElement _tagsContainer;
        private TextField _newTagField;
        private Button _addTagButton;
        private Button _saveButton;
        private Button _createButton;

        [MenuItem("Tools/Avatar Catalog/Avatar Metadata Editor")]
        public static void ShowWindow()
        {
            AvatarMetadataEditorWindow wnd = GetWindow<AvatarMetadataEditorWindow>();
            wnd.titleContent = new GUIContent("Avatar Metadata");
            wnd.minSize = new Vector2(300, 250);
        }

        public void CreateGUI()
        {
            _rootElement = rootVisualElement;

            _headerLabel = new Label("Select an Avatar in Hierarchy") { style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 10 } };
            _rootElement.Add(_headerLabel);

            _infoBox = new HelpBox("No valid avatar selected or metadata needs creation.", HelpBoxMessageType.Info) { style = { display = DisplayStyle.None } };
            _rootElement.Add(_infoBox);

            _createButton = new Button(CreateMetadataAsset) { text = "Create Metadata Asset", style = { display = DisplayStyle.None, marginTop = 5 } };
            _rootElement.Add(_createButton);

            _editorContainer = new VisualElement { name = "metadata-editor-container" };
            _editorContainer.SetEnabled(false);
            _rootElement.Add(_editorContainer);

            _editorContainer.Add(new Label("Comment:"));
            _commentField = new TextField() { multiline = true };
            _commentField.style.minHeight = 60;
            _commentField.RegisterValueChangedCallback(evt => MarkChanged());
            _editorContainer.Add(_commentField);

            _editorContainer.Add(new Label("Tags:") { style = { marginTop = 8 } });
            _tagsContainer = new VisualElement() { name = "tags-list", style = { marginLeft = 10 } };
            _editorContainer.Add(_tagsContainer);

            var tagInputContainer = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            _newTagField = new TextField() { style = { flexGrow = 1 } };
            _addTagButton = new Button(AddTag) { text = "+" };
            tagInputContainer.Add(_newTagField);
            tagInputContainer.Add(_addTagButton);
            _editorContainer.Add(tagInputContainer);

            _saveButton = new Button(SaveChanges) { text = "Save Changes", style = { marginTop = 15, display = DisplayStyle.None } }; // Start hidden
            _editorContainer.Add(_saveButton);

            Selection.selectionChanged += OnSelectionChanged;


            OnSelectionChanged();
        }

        void OnDestroy()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            CheckAndPromptSave();
        }

        private void OnSelectionChanged()
        {
            CheckAndPromptSave();

            _currentlyEditingTagField = null;
            _selectedGameObject = Selection.activeGameObject;
            _currentMetadata = null;
            _selectedGoid = default;

            if (_selectedGameObject != null && _selectedGameObject.GetComponent<AvatarMetadataComponent>() != null)
            {
                _headerLabel.text = $"Editing Metadata for: {_selectedGameObject.name}";
                _selectedGoid = GlobalObjectId.GetGlobalObjectIdSlow(_selectedGameObject);
                _currentMetadata = AvatarMetadataUtil.LoadMetadata(_selectedGoid);

                if (_currentMetadata != null)
                {
                    _infoBox.style.display = DisplayStyle.None;
                    _createButton.style.display = DisplayStyle.None;
                    _editorContainer.SetEnabled(true);
                    _commentField.SetValueWithoutNotify(_currentMetadata.comment ?? "");
                    RefreshTagElements();
                    _saveButton.style.display = DisplayStyle.None;
                }
                else
                {
                    _infoBox.text = $"Metadata asset not found for {_selectedGameObject.name}. Click below to create one.";
                    _infoBox.messageType = HelpBoxMessageType.Warning;
                    _infoBox.style.display = DisplayStyle.Flex;
                    _createButton.style.display = DisplayStyle.Flex;
                    _editorContainer.SetEnabled(false);
                    _commentField.SetValueWithoutNotify("");
                    _tagsContainer.Clear();
                    _saveButton.style.display = DisplayStyle.None;
                }
            }
            else
            {
                _headerLabel.text = "Select an Avatar in Hierarchy";
                _infoBox.text = "Select a GameObject with the 'Avatar Identifier Component' in the Hierarchy view.";
                _infoBox.messageType = HelpBoxMessageType.Info;
                _infoBox.style.display = DisplayStyle.Flex;
                _createButton.style.display = DisplayStyle.None;
                _editorContainer.SetEnabled(false);
                _commentField.SetValueWithoutNotify("");
                _tagsContainer.Clear();
                _saveButton.style.display = DisplayStyle.None;
            }
        }

        private void CreateMetadataAsset()
        {
            _currentlyEditingTagField = null;
            if (_selectedGoid.Equals(default)) return;

            bool created;
            _currentMetadata = AvatarMetadataUtil.LoadOrCreateMetadata(_selectedGoid, out created);

            if (created && _currentMetadata != null)
            {
                OnSelectionChanged();
                MarkChanged();
            }
            else if (!created && _currentMetadata != null)
            {
                Debug.LogWarning("Metadata already existed. Loaded existing.");
                OnSelectionChanged();
            }
            else
            {
                _infoBox.text = "Error: Failed to create metadata asset.";
                _infoBox.messageType = HelpBoxMessageType.Error;
                _infoBox.style.display = DisplayStyle.Flex;
                _createButton.style.display = DisplayStyle.Flex;
                _editorContainer.SetEnabled(false);
            }
        }

        private void AddTag()
        {
            if (_currentMetadata == null || string.IsNullOrWhiteSpace(_newTagField.value)) return;
            string newTag = _newTagField.value.Trim();
            if (!_currentMetadata.tags.Contains(newTag))
            {
                _currentMetadata.tags.Add(newTag);
                AddTagElementUI(newTag);
                _newTagField.value = "";
                MarkChanged();
            }
        }
        private void StartEditingTag(VisualElement tagElement, Label tagLabel)
        {
            if (_currentlyEditingTagField != null && _currentlyEditingTagField.parent != tagElement)
            {
                VisualElement previousTagElement = _currentlyEditingTagField.parent;
                CommitEditTag(previousTagElement, _currentlyEditingTagField);
                if (_currentlyEditingTagField != null) return;
            }

            string currentTag = tagLabel.text;
            var editField = tagElement.Q<TextField>("tag-edit-field");
            var removeButton = tagElement.Q<Button>("tag-remove-button");

            tagLabel.style.display = DisplayStyle.None;
            removeButton.style.display = DisplayStyle.None;

            editField.SetValueWithoutNotify(currentTag);
            editField.style.display = DisplayStyle.Flex;
            editField.Focus();
            editField.SelectAll();

            _currentlyEditingTagField = editField;
        }

        private void CommitEditTag(VisualElement tagElement, TextField editField)
        {
            if (_currentlyEditingTagField != editField || _currentMetadata == null) return;

            string originalTag = tagElement.userData as string;
            string newTag = editField.value.Trim();
            var tagLabel = tagElement.Q<Label>("tag-label");

            bool isValid = true;
            string validationError = "";

            if (string.IsNullOrWhiteSpace(newTag)) { isValid = false; validationError = "Tag cannot be empty."; }
            else if (newTag != originalTag && _currentMetadata.tags.Any(t => t.Equals(newTag, System.StringComparison.Ordinal)))
            {
                isValid = false; validationError = $"Tag '{newTag}' already exists.";
            }

            if (isValid)
            {
                if (newTag != originalTag)
                {
                    int index = _currentMetadata.tags.IndexOf(originalTag);
                    if (index != -1)
                    {
                        _currentMetadata.tags[index] = newTag;
                        tagElement.userData = newTag;
                        MarkChanged();
                        Debug.Log($"Tag '{originalTag}' changed to '{newTag}'");
                    }
                    else
                    {
                        Debug.LogError($"Original tag '{originalTag}' not found.");
                        isValid = false;
                    }
                }
                FinishEditingTag(tagElement, editField, newTag);
            }
            else
            {
                Debug.LogWarning($"Invalid tag edit: {validationError}");
                EditorApplication.Beep();
                editField.Focus();
            }
        }

        private void CancelEditTag(VisualElement tagElement, TextField editField)
        {
            if (_currentlyEditingTagField != editField) return;
            string originalTag = tagElement.userData as string;
            FinishEditingTag(tagElement, editField, originalTag);
        }

        private void FinishEditingTag(VisualElement tagElement, TextField editField, string finalTagValue)
        {
            var tagLabel = tagElement.Q<Label>("tag-label");
            var removeButton = tagElement.Q<Button>("tag-remove-button");

            editField.style.display = DisplayStyle.None;
            tagLabel.text = finalTagValue;
            tagLabel.style.display = DisplayStyle.Flex;
            removeButton.style.display = DisplayStyle.Flex;

            _currentlyEditingTagField = null;
        }

        private void RemoveTag(string tagToRemove)
        {
            if (_currentlyEditingTagField != null && _currentlyEditingTagField.parent.userData as string == tagToRemove)
            {
                CancelEditTag(_currentlyEditingTagField.parent, _currentlyEditingTagField);
            }

            if (_currentMetadata == null || !_currentMetadata.tags.Contains(tagToRemove)) return;

            _currentMetadata.tags.Remove(tagToRemove);

            VisualElement tagElementToRemove = _tagsContainer.Children()
               .FirstOrDefault(element => element.userData as string == tagToRemove);

            if (tagElementToRemove != null) _tagsContainer.Remove(tagElementToRemove);
            MarkChanged();
        }

        private void AddTagElementUI(string tag)
        {
            var tagElement = new VisualElement()
            {
                name = "tag-element",
                style = {
                flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2,
                backgroundColor = new Color(0.8f, 0.8f, 0.8f, 0.3f),
                borderLeftWidth = 2, borderLeftColor = Color.gray
            }
            };
            tagElement.userData = tag;

            var tagLabel = new Label(tag)
            {
                name = "tag-label",
                style = { flexGrow = 1, marginRight = 5 }
            };
            tagLabel.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 2 && _currentlyEditingTagField == null)
                {
                    StartEditingTag(tagElement, tagLabel);
                    evt.StopPropagation();
                }
            });

            var removeButton = new Button(() =>
            {
                if (_currentlyEditingTagField?.parent == tagElement)
                {
                    CancelEditTag(tagElement, _currentlyEditingTagField);
                }
                RemoveTag(tag);
            })
            {
                name = "tag-remove-button",
                text = "x",
                style = { width = 18, height = 18, paddingLeft = 0, paddingRight = 0, marginLeft = 2, alignSelf = Align.Center }
            };

            var editField = new TextField()
            {
                name = "tag-edit-field",
                value = tag,
                style = { flexGrow = 1, display = DisplayStyle.None, marginRight = 5 }
            };
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

            tagElement.Add(tagLabel);
            tagElement.Add(editField);
            tagElement.Add(removeButton);
            _tagsContainer.Add(tagElement);
        }

        private void RefreshTagElements()
        {
            _tagsContainer?.Clear();
            _currentlyEditingTagField = null;
            if (_currentMetadata != null && _currentMetadata.tags != null)
            {
                foreach (string tag in _currentMetadata.tags.ToList())
                {
                    AddTagElementUI(tag);
                }
            }
        }

        private bool _hasUnsavedChanges = false;

        private void MarkChanged()
        {
            if (_currentlyEditingTagField != null)
            {
                CommitEditTag(_currentlyEditingTagField.parent, _currentlyEditingTagField);
            }


            if (_currentMetadata != null && _hasUnsavedChanges)
            {
                // ...(rest of SaveChanges method)...
                SaveChanges();
            }
            else if (!_hasUnsavedChanges)
            {
                Debug.Log("No changes to save.");
            }
        }

        private void SaveChanges()
        {
            if (_currentMetadata != null && _hasUnsavedChanges)
            {
                // Update comment from text field just before saving
                _currentMetadata.comment = _commentField.value;
                // Tags should already be updated in the list

                AvatarMetadataUtil.SaveMetadata(_currentMetadata);
                _hasUnsavedChanges = false;
                _saveButton.style.display = DisplayStyle.None; // Hide save button
                Debug.Log($"Metadata saved for {_selectedGameObject?.name}");

                // Optional: Force Inspector to refresh if visible
                if (_selectedGameObject != null)
                {
                    EditorUtility.SetDirty(_selectedGameObject.GetComponent<AvatarMetadataComponent>());
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); // Force repaint
                }

            }
            else if (!_hasUnsavedChanges)
            {
                Debug.Log("No changes to save.");
            }
        }

        private void CheckAndPromptSave()
        {
            if (_currentlyEditingTagField != null)
            {
                CommitEditTag(_currentlyEditingTagField.parent, _currentlyEditingTagField);
                // Proceed even if commit failed? Or alert user? For now, proceed.
            }

            if (_hasUnsavedChanges && _currentMetadata != null)
            {
                bool save = EditorUtility.DisplayDialog(
                    "Unsaved Metadata Changes",
                    $"There are unsaved changes for '{_selectedGameObject?.name ?? "previous avatar"}'.\nDo you want to save them?",
                    "Save",
                    "Discard");

                if (save)
                {
                    SaveChanges();
                }
                else
                {
                    // Discard changes - simply reset the flag
                    _hasUnsavedChanges = false;
                    // (Optional: Reload metadata from disk if needed, but OnSelectionChanged should handle this)
                }
            }

            _hasUnsavedChanges = false;
            _currentlyEditingTagField = null;
            if (_saveButton != null) _saveButton.style.display = DisplayStyle.None;
        }
    }
}
