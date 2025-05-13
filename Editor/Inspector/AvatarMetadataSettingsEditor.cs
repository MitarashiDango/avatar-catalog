using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    [CustomEditor(typeof(AvatarMetadataSettings))]
    public class AvatarMetadataSettingsEditor : Editor
    {
        [SerializeField]
        private VisualTreeAsset _mainUxmlAsset;

        public override VisualElement CreateInspectorGUI()
        {
            return _mainUxmlAsset.CloneTree();
        }
    }
}
