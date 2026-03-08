using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    [Serializable]
    public class AvatarDatabase : ScriptableObject
    {
        public static readonly string AssetFilePath = "Assets/Avatar Catalog User Data/AvatarDatabase.asset";

        public List<SceneEntry> orderedScenes = new List<SceneEntry>();

        public List<AvatarDatabaseEntry> avatars = new List<AvatarDatabaseEntry>();

        public Dictionary<string, AvatarDatabaseEntry> GetMappedAvatarCatalogEntries()
        {
            var dic = new Dictionary<string, AvatarDatabaseEntry>();

            foreach (var entry in avatars)
            {
                dic.Add(entry.avatarGlobalObjectId, entry);
            }

            return dic;
        }

        public static void Save(AvatarDatabase asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        public static AvatarDatabase LoadOrCreateFile()
        {
            var asset = Load();
            if (asset != null)
            {
                return asset;
            }

            // フォルダー作成
            FolderUtil.CreateUserDataFolder();

            asset = CreateInstance<AvatarDatabase>();
            AssetDatabase.CreateAsset(asset, AssetFilePath);

            return asset;
        }

        public static AvatarDatabase Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AvatarDatabase>(AssetFilePath);
            if (asset != null)
            {
                return asset;
            }

            return null;
        }

        public static bool IsDatabaseFileExists()
        {
            return AssetDatabase.LoadAssetAtPath<AvatarDatabase>(AssetFilePath) != null;
        }

        [Serializable]
        public class SceneEntry
        {
            public string sceneName = "";
            public string sceneAssetGuid = "";
        }

        [Serializable]
        public class AvatarDatabaseEntry
        {
            public string avatarGlobalObjectId;
            public string avatarObjectName;
            public string sceneAssetGuid;
            public string thumbnailImageGuid = "";

            public AvatarDatabaseEntry()
            {
            }

            public AvatarDatabaseEntry(AvatarDatabaseEntry ace)
            {
                avatarGlobalObjectId = ace.avatarGlobalObjectId;
                avatarObjectName = ace.avatarObjectName;
                sceneAssetGuid = ace.sceneAssetGuid;
                thumbnailImageGuid = ace.thumbnailImageGuid;
            }
        }
    }
}
