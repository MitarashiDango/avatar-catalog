using System.Collections.Generic;
using MitarashiDango.AvatarCatalog.Runtime;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarBuildProcessor : MonoBehaviour, IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -99999;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            var components = new List<Component>();
            components.AddRange(avatarGameObject.GetComponentsInChildren<AvatarThumbnailSettings>(true));
            components.AddRange(avatarGameObject.GetComponentsInChildren<AvatarMetadataSettings>(true));

            foreach (var component in components)
            {
                DestroyImmediate(component);
            }

            return true;
        }
    }
}
