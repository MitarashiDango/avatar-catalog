using System;

namespace MitarashiDango.AvatarCatalog.Runtime
{
    [Serializable]
    public class License
    {
        /// <summary>
        /// ライセンス名
        /// </summary>
        public string licenseName;

        /// <summary>
        /// ライセンス情報が掲示されているページのURL
        /// </summary>
        public string licenseUrl;

        /// <summary>
        /// ライセンス文
        /// </summary>
        public string licenseText;
    }
}
