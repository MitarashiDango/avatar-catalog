using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace MitarashiDango.AvatarCatalog
{
    internal class BuildProcess : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -999999;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return true;
            }

            var components = avatarGameObject.GetComponentsInChildren<AvatarMetadata>(true);

            foreach (var c in components)
            {
                Object.DestroyImmediate(c);
            }

            return true;
        }
    }
}
