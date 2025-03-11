using System;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    [Serializable]
    [CreateAssetMenu(fileName = "New Avatar Asset Metadata", menuName = "Avatar Catalog/Avatar Asset Metadata", order = 1)]
    public class AvatarAssetMetadata : ScriptableObject
    {
        public string assetName;
        public Texture2D thumbnail;
        public Texture2D[] images;
        public string productUrl;
        public string description;

        // TODO ライセンス関連の情報の持たせ方を検討する
    }
}
