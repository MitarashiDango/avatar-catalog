using UnityEngine;
using VRC.SDKBase;

namespace MitarashiDango.AvatarCatalog.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Avatar Catalog/Avatar Metadata Settings")]
    public class AvatarMetadataSettings : MonoBehaviour, IEditorOnly
    {
        public AvatarMetadata avatarMetadata;
    }
}
