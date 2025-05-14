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

        /// <summary>
        /// アバターサムネイル画像を削除する
        /// </summary>
        /// <param name="avatarRootObject">サムネイル画像削除対象アバター</param>
        /// <returns>削除に成功した場合、true を返却する。もしファイルが存在しない、または削除に失敗した場合、false を返却する。</returns>
        public static bool DeleteAvatarThumbnailImage(GameObject avatarRootObject)
        {
            var fileName = GenerateFileName(avatarRootObject);
            var filePath = GenerateAvatarThumbnailFilePath(fileName);

            return AssetDatabase.DeleteAsset(filePath);
        }

        /// <summary>
        /// アバターサムネイル画像を現在のアバター名およびシーン名に合わせてリネームする
        /// </summary>
        /// <param name="fileGUID">リネーム対象ファイルのGUID</param>
        /// <param name="avatarRootObject">サムネイル画像の生成元アバター</param>
        /// <returns>リネーム後のファイルパス</returns>
        public static string RenameAvatarThumbnailImage(GUID fileGUID, GameObject avatarRootObject)
        {
            var fileName = GenerateFileName(avatarRootObject);
            return RenameAvatarThumbnailImage(fileGUID, fileName);
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
        /// アバターサムネイル画像を保存する。もし同名のファイルが存在している場合、上書き保存する。
        /// </summary>
        /// <param name="texture">保存するサムネイル画像</param>
        /// <param name="avatarRootObject">サムネイル画像の生成元アバター</param>
        /// <returns>保存された画像アセットファイルのGUID</returns>
        public static GUID StoreAvatarThumbnailImage(Texture2D texture, GameObject avatarRootObject)
        {
            var fileName = GenerateFileName(avatarRootObject);
            return StoreAvatarThumbnailImage(texture, fileName);
        }

        /// <summary>
        /// アバターサムネイル画像を上書き保存する。もしファイルが存在しない場合、新規作成する。
        /// </summary>
        /// <param name="fileGUID">上書き対象画像アセットファイルのGUID</param>
        /// <param name="texture">保存するサムネイル画像</param>
        /// <returns>保存された画像アセットファイルのGUID</returns>
        public static GUID StoreAvatarThumbnailImage(GUID fileGUID, Texture2D texture)
        {
            var filePath = AssetDatabase.GUIDToAssetPath(fileGUID);
            if (string.IsNullOrEmpty(filePath))
            {
                // ファイルが存在しない場合、新規作成する。
                File.WriteAllBytes(filePath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(filePath);
                return AssetDatabase.GUIDFromAssetPath(filePath);
            }

            // ファイルが存在する場合、上書きする
            File.WriteAllBytes(filePath, texture.EncodeToPNG());
            return AssetDatabase.GUIDFromAssetPath(filePath);
        }

        /// <summary>
        /// アバターサムネイル画像を保存する。もし同名のファイルが存在している場合、上書き保存する。
        /// </summary>
        /// <param name="texture">保存するサムネイル画像</param>
        /// <param name="fileNameWithoutExtension">ファイル名（拡張子なし）</param>
        /// <returns>保存された画像アセットファイルのGUID</returns>
        public static GUID StoreAvatarThumbnailImage(Texture2D texture, string fileNameWithoutExtension)
        {
            var filePath = GenerateAvatarThumbnailFilePath(fileNameWithoutExtension);

            var existingThumbnailImageGuid = AssetDatabase.GUIDFromAssetPath(filePath);
            if (!existingThumbnailImageGuid.Empty())
            {
                // ファイルが存在する場合、上書きする
                File.WriteAllBytes(filePath, texture.EncodeToPNG());
                return existingThumbnailImageGuid;
            }

            // ファイルを新規作成する
            File.WriteAllBytes(filePath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(filePath);

            return AssetDatabase.GUIDFromAssetPath(filePath);
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

        public static string GenerateFileName(GameObject avatarRootObject)
        {
            return $"{avatarRootObject.scene.name}_{avatarRootObject.name}";
        }

        public static string GenerateAvatarThumbnailFilePath(string fileName, string extension = "png")
        {
            return $"{FolderUtil.AvatarThumbnailCacheFolderPath}/{fileName}.{extension}";
        }
    }
}
