using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace MitarashiDango.AvatarCatalog.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Avatar Catalog/Avatar Catalog Metadata")]
    public class AvatarMetadata : MonoBehaviour, IEditorOnly
    {
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