using UnityEditor;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    /// <summary>
    /// ごった煮
    /// </summary>
    public class MiscUtil
    {
        public static VisualTreeAsset LoadVisualTreeAsset(string guid)
        {
            if (GUID.TryParse(guid, out var parsedGuid))
            {
                return LoadVisualTreeAsset(parsedGuid);
            }

            return null;
        }

        public static VisualTreeAsset LoadVisualTreeAsset(GUID guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
        }
    }
}
