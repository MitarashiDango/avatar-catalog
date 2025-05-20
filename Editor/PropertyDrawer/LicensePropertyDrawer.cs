using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    [CustomPropertyDrawer(typeof(License))]
    public class LicensePropertyDrawer : PropertyDrawer
    {
        private static readonly string _mainUxmlGuid = "55f9ba19826194b66815405ba69ae87e";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var mainUxmlAsset = LoadMainUxmlAsset();
            if (mainUxmlAsset == null)
            {
                Debug.LogError($"Cannot load UXML file");
                return new VisualElement();
            }

            return mainUxmlAsset.CloneTree();
        }

        private VisualTreeAsset LoadMainUxmlAsset()
        {
            var path = AssetDatabase.GUIDToAssetPath(_mainUxmlGuid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        }
    }
}
