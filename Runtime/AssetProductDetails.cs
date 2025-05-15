using System;
using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog.Runtime
{
    /// <summary>
    /// アセットの製品情報
    /// </summary>
    public class AssetProductDetails : ScriptableObject
    {
        /// <summary>
        /// 製品名
        /// </summary>
        public string productName;

        /// <summary>
        /// 作者名
        /// </summary>
        public string creatorName;

        /// <summary>
        /// 製品URL
        /// </summary>
        public string productUrl;

        /// <summary>
        /// 発売日時
        /// </summary>
        public DateTime releaseDateTime;

        /// <summary>
        /// 製品のタグ情報
        /// </summary>
        public List<string> tags;

        /// <summary>
        /// 製品説明
        /// </summary>
        public string description;

        /// <summary>
        /// ライセンス情報
        /// </summary>
        public List<License> licenses;

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
        }
    }
}
