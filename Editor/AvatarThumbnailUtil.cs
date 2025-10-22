using System.IO;
using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarThumbnailUtil
    {
        private static readonly int ThumbnailImageSize = 512;

        public static Texture2D RenderAvatarThumbnail(AvatarRenderer avatarRenderer, GameObject avatarRootObject)
        {
            return avatarRenderer.Render(avatarRootObject, GetCameraSetting(avatarRootObject), ThumbnailImageSize, ThumbnailImageSize, null, null, false);
        }

        /// <summary>
        /// アバターサムネイル画像を削除する
        /// </summary>
        /// <param name="fileGUID">削除対象ファイルのGUID</param>
        /// <returns>削除に成功した場合、true を返却する。もしファイルが存在しない、または削除に失敗した場合、false を返却する。</returns>
        public static bool DeleteAvatarThumbnailImage(GUID fileGUID)
        {
            var filePath = AssetDatabase.GUIDToAssetPath(fileGUID);
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            return AssetDatabase.DeleteAsset(filePath);
        }

        public static bool DeleteAvatarThumbnailImage(string fileGUID)
        {
            return DeleteAvatarThumbnailImage(new GUID(fileGUID));
        }

        /// <summary>
        /// 画像ファイルの名前を当該ファイルのGUIDへリネームする
        /// </summary>
        /// <param name="fileGUID">リネーム対象ファイルのGUID</param>
        /// <returns>リネーム後のファイルパス</returns>
        public static string RenameToGUID(GUID fileGUID)
        {
            return RenameAvatarThumbnailImage(fileGUID, fileGUID.ToString());
        }

        /// <summary>
        /// アバターサムネイル画像をリネームする
        /// </summary>
        /// <param name="fileGUID">リネーム対象ファイルのGUID</param>
        /// <param name="newFileNameWithoutExtension">リネーム後の名前（拡張子なし）</param>
        /// <returns>リネーム後のファイルパス</returns>
        public static string RenameAvatarThumbnailImage(GUID fileGUID, string newFileNameWithoutExtension)
        {
            var filePath = AssetDatabase.GUIDToAssetPath(fileGUID);
            if (string.IsNullOrEmpty(filePath))
            {
                return filePath;
            }

            // ファイル名が変更されていない場合、何も行わない
            if (Path.GetFileNameWithoutExtension(filePath) == newFileNameWithoutExtension)
            {
                return filePath;
            }

            return AssetDatabase.RenameAsset(filePath, newFileNameWithoutExtension);
        }

        /// <summary>
        /// アバターサムネイル画像を保存する。
        /// </summary>
        /// <param name="texture">保存するサムネイル画像</param>
        /// <returns>保存された画像アセットファイルのGUID</returns>
        public static GUID StoreAvatarThumbnailImage(Texture2D texture)
        {
            // 一時ファイルのパスを生成
            var tempFilePath = AssetDatabase.GenerateUniqueAssetPath(GenerateAvatarThumbnailFilePath($"tmp_{GUID.Generate()}"));

            // ファイルを新規作成する
            File.WriteAllBytes(tempFilePath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(tempFilePath);

            var guid = AssetDatabase.GUIDFromAssetPath(tempFilePath);

            // 一時ファイルをリネームする
            RenameToGUID(guid);

            return guid;
        }

        public static AvatarRenderer.CameraSetting GetCameraSetting(GameObject avatarRootObject)
        {
            var avatarCatalogThumbnailSettings = avatarRootObject.GetComponent<AvatarThumbnailSettings>();

            var cameraSetting = new AvatarRenderer.CameraSetting();
            cameraSetting.BackgroundColor = Color.clear;
            cameraSetting.PositionOffset = avatarCatalogThumbnailSettings != null && avatarCatalogThumbnailSettings.cameraPositionOffset != null ? avatarCatalogThumbnailSettings.cameraPositionOffset : new Vector3();
            cameraSetting.Rotation = avatarCatalogThumbnailSettings != null && avatarCatalogThumbnailSettings.cameraRotation != null ? avatarCatalogThumbnailSettings.cameraRotation : Quaternion.Euler(0, 180, 0);
            cameraSetting.Scale = new Vector3(1, 1, 1);

            return cameraSetting;
        }

        public static string GenerateAvatarThumbnailFilePath(string fileName, string extension = "png")
        {
            return $"{FolderUtil.AvatarThumbnailCacheFolderPath}/{fileName}.{extension}";
        }
    }
}
