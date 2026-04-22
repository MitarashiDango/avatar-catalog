using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    /// <summary>
    /// 設定ページを提供する Provider<br />
    /// 配置場所は Edit &gt; Preferences &gt; Avatar Catalog
    /// </summary>
    internal static class AvatarCatalogSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Preferences/Avatar Catalog", SettingsScope.User)
            {
                label = AcL10n.Tr("preferences.category"),
                guiHandler = _ =>
                {
                    EditorGUI.BeginChangeCheck();
                    var newLang = (Language)EditorGUILayout.EnumPopup(
                        AcL10n.Tr("preferences.language.label"),
                        AcL10n.Current);
                    if (EditorGUI.EndChangeCheck())
                    {
                        AcL10n.SetLanguage(newLang);
                    }
                },
                keywords = new HashSet<string>(new[]
                {
                    "Avatar", "Catalog", "Language", "言語",
                }),
            };
        }
    }
}
