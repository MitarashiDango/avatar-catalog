using UnityEngine;
using VRC.SDKBase;

namespace MitarashiDango.AvatarCatalog.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Avatar Catalog/Avatar Thumbnail Settings")]
    public class AvatarCatalogThumbnailSettings : MonoBehaviour, IEditorOnly
    {
        [SerializeField, HideInInspector]
        public Vector3 cameraPositionOffset = new Vector3(0, 0, 0);

        [SerializeField, HideInInspector]
        public Quaternion cameraRotation = Quaternion.Euler(0, 180, 0);
    }
}