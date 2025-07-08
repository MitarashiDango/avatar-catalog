using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarThumbnailPreviewWindow : EditorWindow
    {
        private static readonly string _mainUxmlGuid = "1171fa6381d630845a36a5e82ec47f61";

        private ObjectField _avatarDescriptorObjectField;
        private Vector3Field _cameraPositionOffsetField;
        private Vector3Field _cameraRotationField;

        private GameObject _currentAvatarObject = null;
        private SerializedObject _serializedObject = null;
        private SerializedProperty _cameraRotation;
        private RenderTexture _texture;
        private AvatarRenderer _avatarRenderer;

        [MenuItem("Tools/Avatar Catalog/Avatar Thumbnail Preview")]
        public static void ShowWindow()
        {
            ShowWindowInternal();
        }

        public static void ShowWindow(GameObject avatarObject)
        {
            var window = ShowWindowInternal();
            window.SetAvatarObject(avatarObject);
        }

        internal static AvatarThumbnailPreviewWindow ShowWindowInternal()
        {
            var window = GetWindow<AvatarThumbnailPreviewWindow>("Avatar Thumbnail Preview");
            window.minSize = new Vector2(384, 490);
            return window;
        }

        private void OnEnable()
        {
            InitializeTexture();
            InitializeAvatarRenderer();
        }

        private void OnDestroy()
        {
            if (_cameraRotationField != null)
            {
                Undo.undoRedoPerformed -= UndoRedoCallback;
            }

            if (_texture != null)
            {
                DestroyImmediate(_texture);
                _texture = null;
            }

            if (_avatarRenderer != null)
            {
                _avatarRenderer.Dispose();
                _avatarRenderer = null;
            }
        }

        private void InitializeTexture()
        {
            if (_texture == null)
            {
                _texture = new RenderTexture(512, 512, 32, DefaultFormat.LDR);
            }
        }

        private void InitializeAvatarRenderer()
        {
            if (_avatarRenderer == null)
            {
                _avatarRenderer = new AvatarRenderer();
            }
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var mainUxmlAsset = MiscUtil.LoadVisualTreeAsset(_mainUxmlGuid);
            if (mainUxmlAsset == null)
            {
                Debug.LogError($"Cannot load UXML file");
                return;
            }

            var preferredFontFamilyName = FontCache.GetPreferredFontFamilyName();
            if (preferredFontFamilyName != "")
            {
                var fontAsset = FontCache.GetOrCreateFontAsset(preferredFontFamilyName);
                FontCache.ApplyFont(rootVisualElement, fontAsset);
            }

            var previewAvatarThumbnailRoot = mainUxmlAsset.CloneTree();

            _avatarDescriptorObjectField = previewAvatarThumbnailRoot.Q<ObjectField>("preview-target-avatar-object");
            _avatarDescriptorObjectField.RegisterValueChangedCallback(OnAvatarDescriptorObjectFieldValueChanged);

            var refreshButton = previewAvatarThumbnailRoot.Q<Button>("refresh-button");
            refreshButton.RegisterCallback<ClickEvent>(OnRefreshButtonClick);

            var refreshButtonIconImage = previewAvatarThumbnailRoot.Q<Image>("refresh-button-icon-image");
            refreshButtonIconImage.image = EditorGUIUtility.IconContent("d_Refresh").image;

            var avatarThumbnailImage = previewAvatarThumbnailRoot.Q<Image>("avatar-thumbnail-image");
            avatarThumbnailImage.image = _texture;
            avatarThumbnailImage.RegisterCallback<GeometryChangedEvent>(OnAvatarThumbnailImageGeometryChanged);

            _cameraPositionOffsetField = previewAvatarThumbnailRoot.Q<Vector3Field>("camera-position-offset-field");
            _cameraPositionOffsetField.RegisterCallback<ChangeEvent<Vector3>>(OnCameraPositionOffsetFieldValueChanged);

            SetupCameraRotationField(previewAvatarThumbnailRoot);

            root.Add(previewAvatarThumbnailRoot);

            UpdateAvatarThumbnailPreview();

            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            var preferredFontFamilyName = FontCache.GetPreferredFontFamilyName();
            if (preferredFontFamilyName != "")
            {
                var fontAsset = FontCache.GetOrCreateFontAsset(preferredFontFamilyName);
                FontCache.ApplyFont(rootVisualElement, fontAsset);
            }
        }

        private void SetAvatarObject(GameObject avatarObject)
        {
            var avatarDescriptor = avatarObject.GetComponent<VRCAvatarDescriptor>();
            if (avatarDescriptor == null)
            {
                return;
            }

            _currentAvatarObject = avatarObject;
            _avatarDescriptorObjectField.SetValueWithoutNotify(avatarDescriptor);

            var avatarThumbnailSettings = _currentAvatarObject.GetComponent<AvatarThumbnailSettings>();
            if (avatarThumbnailSettings == null)
            {
                avatarThumbnailSettings = _currentAvatarObject.AddComponent<AvatarThumbnailSettings>();
            }

            SetAvatarThumbnailSettings(avatarThumbnailSettings);

            UpdateAvatarThumbnailPreview();
        }

        private void SetAvatarThumbnailSettings(AvatarThumbnailSettings ats)
        {
            if (ats == null)
            {
                _serializedObject = null;
                _cameraRotation = null;
                _cameraRotationField.value = new Vector3(0, 0, 0);
                rootVisualElement.Unbind();
                return;
            }

            _serializedObject = new SerializedObject(ats);
            _cameraRotation = _serializedObject.FindProperty("cameraRotation");

            // 初期値を設定 (QuaternionからEulerへ)
            _cameraRotationField.value = _cameraRotation.quaternionValue.eulerAngles;

            rootVisualElement.Bind(_serializedObject);
        }

        private void UpdateAvatarThumbnailPreview()
        {
            var cameraSetting = new AvatarRenderer.CameraSetting();

            if (_currentAvatarObject != null)
            {
                var thumbnailSettings = _currentAvatarObject.GetComponent<AvatarThumbnailSettings>();
                if (thumbnailSettings != null)
                {
                    cameraSetting.PositionOffset = thumbnailSettings.cameraPositionOffset;
                    cameraSetting.Rotation = thumbnailSettings.cameraRotation;
                }
                else
                {
                    cameraSetting.Rotation = Quaternion.Euler(0, 180, 0);
                }
            }
            else
            {
                cameraSetting.BackgroundColor = Color.clear;
            }

            _avatarRenderer.Render(_currentAvatarObject, cameraSetting, _texture, null, null, false);
        }

        private void SetupCameraRotationField(VisualElement root)
        {
            _cameraRotationField = root.Q<Vector3Field>("camera-rotation-field");

            if (_cameraRotation != null)
            {
                // 初期値を設定 (QuaternionからEulerへ)
                _cameraRotationField.value = _cameraRotation.quaternionValue.eulerAngles;
            }

            // 値変更時のコールバック
            _cameraRotationField.RegisterValueChangedCallback(evt =>
            {
                if (_cameraRotation != null)
                {
                    _cameraRotation.quaternionValue = Quaternion.Euler(evt.newValue);
                    _cameraRotation.serializedObject.ApplyModifiedProperties();
                }

                UpdateAvatarThumbnailPreview();
            });

            // Undo/Redo処理のためのコールバック登録
            _cameraRotationField.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            _cameraRotationField.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnRefreshButtonClick(ClickEvent ev)
        {
            UpdateAvatarThumbnailPreview();
        }

        private void OnAvatarDescriptorObjectFieldValueChanged(ChangeEvent<Object> ev)
        {
            if (ev.newValue == null)
            {
                _currentAvatarObject = null;
                UpdateAvatarThumbnailPreview();
                return;
            }

            if (ev.newValue is VRCAvatarDescriptor avatarDescriptor)
            {
                if (avatarDescriptor.gameObject != _currentAvatarObject)
                {
                    _currentAvatarObject = avatarDescriptor.gameObject;
                    var avatarThumbnailSettings = _currentAvatarObject.GetComponent<AvatarThumbnailSettings>();
                    if (avatarThumbnailSettings == null)
                    {
                        avatarThumbnailSettings = _currentAvatarObject.AddComponent<AvatarThumbnailSettings>();
                    }

                    SetAvatarThumbnailSettings(avatarThumbnailSettings);

                    UpdateAvatarThumbnailPreview();
                }
            }
        }

        private void OnAvatarThumbnailImageGeometryChanged(GeometryChangedEvent evt)
        {
            var element = evt.target as VisualElement;
            if (element == null)
            {
                return;
            }

            element.style.height = evt.newRect.width;
        }

        private void OnCameraPositionOffsetFieldValueChanged(ChangeEvent<Vector3> evt)
        {
            UpdateAvatarThumbnailPreview();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            Undo.undoRedoPerformed -= UndoRedoCallback; // 念のため一度解除
            Undo.undoRedoPerformed += UndoRedoCallback;
            UndoRedoCallback(); // 初期状態を反映
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            Undo.undoRedoPerformed -= UndoRedoCallback;
        }

        private void UndoRedoCallback()
        {
            if (_cameraRotation == null || _cameraRotationField == null)
            {
                return;
            }

            // SerializedObjectの状態を最新に更新
            _cameraRotation.serializedObject.Update();

            // UIフィールドに値を通知なしで設定 (これによりValueChangedCallbackがトリガーされるのを防ぐ)
            _cameraRotationField.SetValueWithoutNotify(_cameraRotation.quaternionValue.eulerAngles);
        }
    }
}
