using UnityEngine;
using VRC.SDKBase;

namespace MitarashiDango.AvatarCatalog.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Avatar Catalog/Avatar Thumbnail Settings")]
    public class AvatarThumbnailSettings : MonoBehaviour, IEditorOnly
    {
        public Vector3 cameraPositionOffset = new Vector3(0, 0, 0);
        public Quaternion cameraRotation = Quaternion.Euler(0, 180, 0);
    }
}