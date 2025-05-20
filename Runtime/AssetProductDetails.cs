using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog.Runtime
{
    /// <summary>
    /// アセットの製品情報
    /// </summary>
    [CreateAssetMenu(menuName = "Avatar Catalog/Asset Product Details")]
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
        public string releaseDateTime;

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
    }
}
