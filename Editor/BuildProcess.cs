using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarBuildProcessor : MonoBehaviour, IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -99999;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            foreach (var component in avatarGameObject.GetComponentsInChildren<AvatarCatalogThumbnailSettings>(true))
            {
                DestroyImmediate(component);
            }

            return true;
        }
    }
}
