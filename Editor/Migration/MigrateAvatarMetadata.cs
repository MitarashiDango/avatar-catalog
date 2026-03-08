using System.Linq;
using MitarashiDango.AvatarCatalog.Runtime;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    public class MigrateAvatarMetadata
    {
        public void Do(GameObject avatarRootObject)
        {
            var avatarMetadataSettings = avatarRootObject.GetComponent<AvatarMetadataSettings>();
            if (avatarMetadataSettings == null)
            {
                return;
            }

            var legacyAvatarMetadata = avatarMetadataSettings.avatarMetadata;
            if (legacyAvatarMetadata == null)
            {
                return;
            }

            var avatarMetadata = avatarRootObject.AddComponent<AvatarMetadata>();
            avatarMetadata.comment = legacyAvatarMetadata.comment;
            avatarMetadata.assetProductDetails = legacyAvatarMetadata.assetProductDetails.Where(apd => apd != null).ToList();
            avatarMetadata.tags = legacyAvatarMetadata.tags.ToList();

            Object.DestroyImmediate(avatarMetadataSettings);
        }
    }
}
