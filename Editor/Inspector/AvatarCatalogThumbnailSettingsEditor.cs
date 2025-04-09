using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    [CustomEditor(typeof(AvatarCatalogThumbnailSettings))]
    public class AvatarCatalogThumbnailSettingsEditor : Editor
    {
        private AvatarRenderer _avatarRenderer;
        private SerializedProperty _cameraPositionOffset;

        private void OnEnable()
        {
            InitializeAvatarRenderer();
            _cameraPositionOffset = serializedObject.FindProperty("cameraPositionOffset");
        }

        private void OnDisable()
        {
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

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var cameraPositionOffsetField = new Vector3Field();
            cameraPositionOffsetField.label = "カメラ座標オフセット";
            cameraPositionOffsetField.BindProperty(_cameraPositionOffset);

            root.Add(cameraPositionOffsetField);

            return root;
        }
    }
}
