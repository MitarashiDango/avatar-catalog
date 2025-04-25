using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine.SceneManagement;
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

        private void OnDestroy()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var preferredFontFamilyName = FontCache.GetPreferredFontFamilyName();
            if (preferredFontFamilyName != "")
            {
                var fontAsset = FontCache.GetOrCreateFontAsset(preferredFontFamilyName);
                FontCache.ApplyFont(root, fontAsset);
            }

            var cameraPositionOffsetField = new Vector3Field();
            cameraPositionOffsetField.label = "カメラ座標オフセット";
            cameraPositionOffsetField.BindProperty(_cameraPositionOffset);

            root.Add(cameraPositionOffsetField);

            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;

            return root;
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            Repaint();
        }
    }
}
