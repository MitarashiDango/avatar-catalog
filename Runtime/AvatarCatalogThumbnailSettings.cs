using UnityEngine;
using VRC.SDKBase;

namespace MitarashiDango.AvatarCatalog
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Avatar Catalog/Avatar Thumbnail Settings")]
    public class AvatarCatalogThumbnailSettings : MonoBehaviour, IEditorOnly
    {
        [SerializeField, HideInInspector]
        public Vector3 cameraPositionOffset = new Vector3(0, 0, 0);
    }
}