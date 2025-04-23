
using UnityEditor;

namespace MitarashiDango.AvatarCatalog
{
    public class FolderUtil
    {
        public static readonly string AvatarCatalogUserDataFolderPath = "Assets/Avatar Catalog User Data";
        public static readonly string AvatarMetadataFolderPath = "Assets/Avatar Catalog User Data/AvatarMetadata";
        public static readonly string CacheFolderPath = "Assets/Avatar Catalog User Data/Cache";
        public static readonly string AvatarThumbnailCacheFolderPath = "Assets/Avatar Catalog User Data/Cache/AvatarThumbnails";

        /// <summary>
        /// 各種ユーザーデータを保存するフォルダーを作成します
        /// </summary>
        public static void CreateUserDataFolders()
        {
            CreateUserDataFolder();
            CreateCacheFolder();
            CreateAvatarThumbnailsCacheFolder();
            CreateAvatarMetadataFolder();
        }

        /// <summary>
        /// ユーザーデータフォルダーが存在するか確認します
        /// </summary>
        /// <returns>フォルダーが存在する場合は true, それ以外の場合は false を返却する</returns>
        public static bool IsUserDataFolderExists()
        {
            return AssetDatabase.IsValidFolder(AvatarCatalogUserDataFolderPath);
        }

        /// <summary>
        /// キャッシュデータフォルダーが存在するか確認します
        /// </summary>
        /// <returns>フォルダーが存在する場合は true, それ以外の場合は false を返却する</returns>
        public static bool IsCacheFolderExists()
        {
            return AssetDatabase.IsValidFolder(CacheFolderPath);
        }

        /// <summary>
        /// アバターサムネイルキャッシュフォルダーが存在するか確認します
        /// </summary>
        /// <returns>フォルダーが存在する場合は true, それ以外の場合は false を返却する</returns>
        public static bool IsAvatarThumbnailsCacheFolderExists()
        {
            return AssetDatabase.IsValidFolder(AvatarThumbnailCacheFolderPath);
        }

        /// <summary>
        /// アバターメタデータフォルダーが存在する確認します
        /// </summary>
        /// <returns>フォルダーが存在する場合は true, それ以外の場合は false を返却する</returns>
        public static bool IsAvatarMetadataFolderExists()
        {
            return AssetDatabase.IsValidFolder(AvatarMetadataFolderPath);
        }

        /// <summary>
        /// ユーザーデータフォルダーを作成します
        /// </summary>
        public static void CreateUserDataFolder()
        {
            if (!IsUserDataFolderExists())
            {
                AssetDatabase.CreateFolder("Assets", "Avatar Catalog User Data");
            }
        }

        /// <summary>
        /// キャッシュデータフォルダーを作成します
        /// </summary>
        public static void CreateCacheFolder()
        {
            if (!IsCacheFolderExists())
            {
                AssetDatabase.CreateFolder(AvatarCatalogUserDataFolderPath, "Cache");
            }
        }

        /// <summary>
        /// アバターサムネイルキャッシュフォルダーを作成します
        /// </summary>
        public static void CreateAvatarThumbnailsCacheFolder()
        {
            if (!IsAvatarThumbnailsCacheFolderExists())
            {
                AssetDatabase.CreateFolder(CacheFolderPath, "AvatarThumbnails");
            }
        }

        /// <summary>
        /// アバターメタデータフォルダーを作成します
        /// </summary>
        public static void CreateAvatarMetadataFolder()
        {
            if (!IsAvatarMetadataFolderExists())
            {
                AssetDatabase.CreateFolder(AvatarCatalogUserDataFolderPath, "AvatarMetadata");
            }
        }
    }
}