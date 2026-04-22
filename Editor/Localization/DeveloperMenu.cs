using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    /// <summary>
    /// 開発者向けメニュー項目。通常利用では使用しない。
    /// </summary>
    internal static class DeveloperMenu
    {
        [MenuItem("Tools/Avatar Catalog/Developer/Reload Localization")]
        private static void ReloadLocalization()
        {
            AcL10n.ReloadTranslations();
            Debug.Log("[AvatarCatalog] Localization strings have been reloaded.");
        }
    }
}
