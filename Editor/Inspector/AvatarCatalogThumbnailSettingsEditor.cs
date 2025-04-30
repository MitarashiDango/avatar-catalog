using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    [CustomEditor(typeof(AvatarCatalogThumbnailSettings))]
    public class AvatarCatalogThumbnailSettingsEditor : Editor
    {
        private SerializedProperty _cameraPositionOffset;
        private SerializedProperty _cameraRotation;

        private void OnEnable()
        {
            _cameraPositionOffset = serializedObject.FindProperty("cameraPositionOffset");
            _cameraRotation = serializedObject.FindProperty("cameraRotation");
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

            var cameraRotationField = new Vector3Field();
            cameraRotationField.value = _cameraRotation.quaternionValue.eulerAngles;
            cameraRotationField.label = "カメラ角度";
            cameraRotationField.RegisterValueChangedCallback(e =>
            {
                _cameraRotation.quaternionValue = Quaternion.Euler(e.newValue);
                _cameraRotation.serializedObject.ApplyModifiedProperties();
            });

            Undo.UndoRedoCallback undoRedoCallback = () =>
            {
                _cameraRotation.serializedObject.Update();
                cameraRotationField.SetValueWithoutNotify(_cameraRotation.quaternionValue.eulerAngles);
            };

            cameraRotationField.RegisterCallback<AttachToPanelEvent>(e =>
            {
                Undo.undoRedoPerformed += undoRedoCallback;
                undoRedoCallback();
            });

            cameraRotationField.RegisterCallback<DetachFromPanelEvent>(e =>
            {
                Undo.undoRedoPerformed -= undoRedoCallback;
            });

            root.Add(cameraRotationField);

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
