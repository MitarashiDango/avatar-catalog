using UnityEngine;
using VRC.SDKBase;

namespace MitarashiDango.AvatarCatalog
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Avatar Catalog/Avatar Metadata")]
    public class AvatarMetadata : MonoBehaviour, IEditorOnly
    {
        public string description;
        public string note;

        public AvatarAssetMetadata[] inUseAssets;
    }
}
