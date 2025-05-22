using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarThumbnailPreviewWindow : EditorWindow
    {
        private static readonly string _mainUxmlGuid = "1171fa6381d630845a36a5e82ec47f61";

        private ObjectField _avatarDescriptorObjectField;

        private GameObject _currentAvatarObject = null;
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
            window.minSize = new Vector2(256, 282);
            return window;
        }

        private void OnEnable()
        {
            InitializeTexture();
            InitializeAvatarRenderer();
        }

        private void OnDestroy()
        {
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

            var previewAvatarThumbnailRoot = mainUxmlAsset.CloneTree();

            _avatarDescriptorObjectField = previewAvatarThumbnailRoot.Q<ObjectField>("preview-target-avatar-object");
            _avatarDescriptorObjectField.RegisterValueChangedCallback(OnAvatarDescriptorObjectFieldValueChanged);

            var refreshButton = previewAvatarThumbnailRoot.Q<Button>("refresh-button");
            refreshButton.RegisterCallback<ClickEvent>(OnRefreshButtonClick);

            var refreshButtonIconImage = previewAvatarThumbnailRoot.Q<Image>("refresh-button-icon-image");
            refreshButtonIconImage.image = EditorGUIUtility.IconContent("d_Refresh").image;

            var avatarThumbnailImage = previewAvatarThumbnailRoot.Q<Image>("avatar-thumbnail-image");
            avatarThumbnailImage.image = _texture;

            root.Add(previewAvatarThumbnailRoot);

            UpdateAvatarThumbnailPreview();
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

            UpdateAvatarThumbnailPreview();
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
                    UpdateAvatarThumbnailPreview();
                }
            }
        }
    }
}
