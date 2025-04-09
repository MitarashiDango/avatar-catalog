using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    [CustomEditor(typeof(AvatarCatalogThumbnailSettings))]
    public class AvatarCatalogThumbnailSettingsEditor : Editor
    {
        private SerializedProperty _cameraPositionOffset;

        private void OnEnable()
        {
            _cameraPositionOffset = serializedObject.FindProperty("cameraPositionOffset");
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
