using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog.Runtime
{
    [CreateAssetMenu(menuName = "Avatar Catalog/Avatar Metadata")]
    public class AvatarMetadata : ScriptableObject
    {
        /// <summary>
        /// メタデータが紐付いているアバターオブジェクトのグローバルオブジェクトID
        /// </summary>
        public string avatarGlobalObjectId = "";

        /// <summary>
        /// コメント
        /// </summary>
        public string comment = "";

        /// <summary>
        /// タグ情報
        /// </summary>
        public List<string> tags = new List<string>();

        /// <summary>
        /// アセット製品情報
        /// </summary>
        public List<AssetProductDetail> assetProductDetails = new List<AssetProductDetail>();
    }
}
