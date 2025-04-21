
using UnityEditor;

namespace MitarashiDango.AvatarCatalog
{
    public class DirectoryUtil
    {
        public static bool IsUserDataFolderExists()
        {
            return AssetDatabase.IsValidFolder("Assets/Avatar Catalog User Data");
        }

        public static bool IsCacheFolderExists()
        {
            return AssetDatabase.IsValidFolder("Assets/Avatar Catalog User Data/Cache");
        }

        public static bool IsAvatarThumbnailsCacheFolderExists()
        {
            return AssetDatabase.IsValidFolder("Assets/Avatar Catalog User Data/Cache/AvatarThumbnails");
        }

        public static bool IsAvatarMetadataFolderExists()
        {
            return AssetDatabase.IsValidFolder("Assets/Avatar Catalog User Data/AvatarMetadata");
        }

        public static void CreateUserDataFolder()
        {
            if (!IsUserDataFolderExists())
            {
                AssetDatabase.CreateFolder("Assets", "Avatar Catalog User Data");
            }
        }

        public static void CreateCacheFolder()
        {
            if (!IsCacheFolderExists())
            {
                AssetDatabase.CreateFolder("Assets/Avatar Catalog User Data", "Cache");
            }
        }

        public static void CreateAvatarThumbnailsCacheFolder()
        {
            if (!IsAvatarThumbnailsCacheFolderExists())
            {
                AssetDatabase.CreateFolder("Assets/Avatar Catalog User Data/Cache", "AvatarThumbnails");
            }
        }

        public static void CreateAvatarMetadataFolder()
        {
            if (!IsAvatarMetadataFolderExists())
            {
                AssetDatabase.CreateFolder("Assets/Avatar Catalog User Data", "AvatarMetadata");
            }
        }
    }
}