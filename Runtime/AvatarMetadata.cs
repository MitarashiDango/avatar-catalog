using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog.Runtime
{
    [CreateAssetMenu(menuName = "Avatar Catalog/Avatar Metadata")]
    public class AvatarMetadata : ScriptableObject
    {
        /// <summary>
        /// アバターの GlobalObjectId
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
    }
}
