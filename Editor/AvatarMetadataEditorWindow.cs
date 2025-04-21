using System.Collections;
using System.Collections.Generic;
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

        // --- UI Element References ---
        private Label _headerLabel;
        private HelpBox _infoBox;
        private VisualElement _editorContainer;
        private TextField _commentField;
        private VisualElement _tagsContainer;
        private TextField _newTagField;
        private Button _addTagButton;
        private Button _saveButton;
        private Button _createButton;

        [MenuItem("Window/Avatar Metadata Editor")]
        public static void ShowWindow()
        {
            AvatarMetadataEditorWindow wnd = GetWindow<AvatarMetadataEditorWindow>();
            wnd.titleContent = new GUIContent("Avatar Metadata");
            wnd.minSize = new Vector2(300, 250); // ウィンドウの最小サイズ
        }

        public void CreateGUI()
        {
            _rootElement = rootVisualElement;

            // Header
            _headerLabel = new Label("Select an Avatar in Hierarchy") { style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 10 } };
            _rootElement.Add(_headerLabel);

            // Info Box
            _infoBox = new HelpBox("No valid avatar selected or metadata needs creation.", HelpBoxMessageType.Info) { style = { display = DisplayStyle.None } };
            _rootElement.Add(_infoBox);

            // Create Button (initially hidden)
            _createButton = new Button(CreateMetadataAsset) { text = "Create Metadata Asset", style = { display = DisplayStyle.None, marginTop = 5 } };
            _rootElement.Add(_createButton);

            // Editor Area (initially hidden or disabled)
            _editorContainer = new VisualElement { name = "metadata-editor-container" };
            _editorContainer.SetEnabled(false); // Start disabled
            _rootElement.Add(_editorContainer);

            // Comment
            _editorContainer.Add(new Label("Comment:"));
            _commentField = new TextField() { multiline = true };
            _commentField.style.minHeight = 60;
            _commentField.RegisterValueChangedCallback(evt => MarkChanged());
            _editorContainer.Add(_commentField);

            // Tags
            _editorContainer.Add(new Label("Tags:") { style = { marginTop = 8 } });
            _tagsContainer = new VisualElement() { name = "tags-list", style = { marginLeft = 10 } };
            _editorContainer.Add(_tagsContainer);

            var tagInputContainer = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            _newTagField = new TextField() { style = { flexGrow = 1 } };
            _addTagButton = new Button(AddTag) { text = "+" };
            tagInputContainer.Add(_newTagField);
            tagInputContainer.Add(_addTagButton);
            _editorContainer.Add(tagInputContainer);

            // Save Button
            _saveButton = new Button(SaveChanges) { text = "Save Changes", style = { marginTop = 15, display = DisplayStyle.None } }; // Start hidden
            _editorContainer.Add(_saveButton);


            // --- Event Handling ---
            // Subscribe to selection changes
            Selection.selectionChanged += OnSelectionChanged;

            // --- Initial Update ---
            OnSelectionChanged(); // Call once to initialize based on current selection
        }

        void OnDestroy()
        {
            // Unsubscribe when the window is closed
            Selection.selectionChanged -= OnSelectionChanged;
            // Prompt to save if changes are pending?
            CheckAndPromptSave();
        }

        private void OnSelectionChanged()
        {
            // Check if there were unsaved changes before changing target
            CheckAndPromptSave();

            _currentlyEditingTagField = null;
            _selectedGameObject = Selection.activeGameObject;
            _currentMetadata = null; // Reset current metadata
            _selectedGoid = default;

            if (_selectedGameObject != null && _selectedGameObject.GetComponent<AvatarMetadataComponent>() != null)
            {
                _headerLabel.text = $"Editing Metadata for: {_selectedGameObject.name}";
                _selectedGoid = GlobalObjectId.GetGlobalObjectIdSlow(_selectedGameObject);
                _currentMetadata = AvatarMetadataUtil.LoadMetadata(_selectedGoid);

                if (_currentMetadata != null)
                {
                    // Metadata exists, enable editor and populate fields
                    _infoBox.style.display = DisplayStyle.None;
                    _createButton.style.display = DisplayStyle.None;
                    _editorContainer.SetEnabled(true);
                    _commentField.SetValueWithoutNotify(_currentMetadata.comment ?? "");
                    RefreshTagElements();
                    _saveButton.style.display = DisplayStyle.None; // Hide save button until changes are made
                }
                else
                {
                    // Metadata doesn't exist for this valid avatar
                    _infoBox.text = $"Metadata asset not found for {_selectedGameObject.name}. Click below to create one.";
                    _infoBox.messageType = HelpBoxMessageType.Warning;
                    _infoBox.style.display = DisplayStyle.Flex;
                    _createButton.style.display = DisplayStyle.Flex; // Show create button
                    _editorContainer.SetEnabled(false); // Disable editor fields
                    _commentField.SetValueWithoutNotify("");
                    _tagsContainer.Clear();
                    _saveButton.style.display = DisplayStyle.None;
                }
            }
            else
            {
                // No valid avatar selected
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
            if (_selectedGoid.Equals(default(GlobalObjectId))) return;

            bool created;
            _currentMetadata = AvatarMetadataUtil.LoadOrCreateMetadata(_selectedGoid, out created);

            if (created && _currentMetadata != null)
            {
                // Successfully created, update UI
                OnSelectionChanged(); // Re-run selection logic to load the new data
                                      // MarkChanged(); // Optionally mark as changed immediately if you want save button active
            }
            else if (!created && _currentMetadata != null)
            {
                Debug.LogWarning("Metadata already existed. Loaded existing.");
                OnSelectionChanged(); // Reload UI
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


        // --- Tag Handling (Similar to Inspector Editor) ---
        private void AddTag()
        {
            if (_currentMetadata == null || string.IsNullOrWhiteSpace(_newTagField.value)) return;
            string newTag = _newTagField.value.Trim();
            if (!_currentMetadata.tags.Contains(newTag))
            {
                _currentMetadata.tags.Add(newTag); // Directly modify the list
                AddTagElementUI(newTag);
                _newTagField.value = "";
                MarkChanged();
            }
        }
        private void StartEditingTag(VisualElement tagElement, Label tagLabel)
        {
            // Ensure no other tag is being edited
            if (_currentlyEditingTagField != null && _currentlyEditingTagField.parent != tagElement)
            {
                VisualElement previousTagElement = _currentlyEditingTagField.parent;
                CommitEditTag(previousTagElement, _currentlyEditingTagField);
                if (_currentlyEditingTagField != null) return; // Abort if previous commit failed
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
                    // Directly modify _currentMetadata (Undo isn't directly applicable in EditorWindow like in Editor)
                    int index = _currentMetadata.tags.IndexOf(originalTag);
                    if (index != -1)
                    {
                        _currentMetadata.tags[index] = newTag;
                        tagElement.userData = newTag; // Update userData
                        MarkChanged(); // Mark window as having changes
                        Debug.Log($"Tag '{originalTag}' changed to '{newTag}'");
                    }
                    else
                    {
                        Debug.LogError($"Original tag '{originalTag}' not found.");
                        isValid = false; // Treat as failed
                    }
                }
                FinishEditingTag(tagElement, editField, newTag);
            }
            else
            {
                Debug.LogWarning($"Invalid tag edit: {validationError}");
                EditorApplication.Beep();
                editField.Focus();
                // Keep the edit field open for correction
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
            // --- Tag Element Container ---
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

            // --- Label (Clickable) ---
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

            // --- Remove Button ---
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

            // --- Edit Field (Initially Hidden) ---
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

            // Add elements
            tagElement.Add(tagLabel);
            tagElement.Add(editField);
            tagElement.Add(removeButton);
            _tagsContainer.Add(tagElement);
        }

        private void RefreshTagElements()
        {
            _tagsContainer?.Clear();
            _currentlyEditingTagField = null; // Clear editing state on refresh
            if (_currentMetadata != null && _currentMetadata.tags != null)
            {
                // Use ToList() to create a copy for safe iteration during UI rebuild
                foreach (string tag in _currentMetadata.tags.ToList())
                {
                    AddTagElementUI(tag); // Calls the modified method
                }
            }
        }


        // --- Change Tracking & Saving ---
        private bool _hasUnsavedChanges = false;

        private void MarkChanged()
        {
            // If a tag is currently being edited, commit it before saving
            if (_currentlyEditingTagField != null)
            {
                CommitEditTag(_currentlyEditingTagField.parent, _currentlyEditingTagField);
                // We might want to check if commit succeeded before proceeding
            }


            if (_currentMetadata != null && _hasUnsavedChanges)
            {
                // ...(rest of SaveChanges method)...
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
            // Ensure save button state is reset if changes were discarded or target changed
            _hasUnsavedChanges = false;
            _currentlyEditingTagField = null;
            if (_saveButton != null) _saveButton.style.display = DisplayStyle.None;
        }
    }
}
