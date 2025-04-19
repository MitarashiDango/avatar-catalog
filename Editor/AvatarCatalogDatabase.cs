using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    [Serializable]
    public class AvatarCatalogDatabase : ScriptableObject
    {
        public static string ASSET_FILE_PATH = "Assets/Avatar Catalog User Data/AvatarCatalogDatabase.asset";

        [SerializeField]
        private List<Avatar> _avatars = new List<Avatar>();
        public List<Avatar> avatars
        {
            get => _avatars;
            private set => _avatars = value;
        }

        public bool IsExists(GameObject avatar)
        {
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(avatar).ToString();
            return _avatars.Where(a => a.globalObjectId == gid).Count() > 0;
        }

        public void Clear()
        {
            _avatars.Clear();
        }

        public void AddAvatar(Avatar avatar)
        {
            if (_avatars.Where(a => a.globalObjectId == avatar.globalObjectId).Count() > 0)
            {
                return;
            }

            _avatars.Add(avatar);
        }

        public void Save()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AvatarCatalogDatabase>(ASSET_FILE_PATH);
            if (!asset)
            {
                AssetDatabase.CreateAsset(this, ASSET_FILE_PATH);
            }
            else
            {
                EditorUtility.CopySerialized(this, asset);
            }

            AssetDatabase.Refresh();
        }

        public static AvatarCatalogDatabase Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AvatarCatalogDatabase>(ASSET_FILE_PATH);
            if (asset)
            {
                return asset;
            }

            return null;
        }

        public static AvatarCatalogDatabase CreateOrLoad()
        {
            var asset = Load();
            if (!asset)
            {
                asset = ScriptableObject.CreateInstance<AvatarCatalogDatabase>();
                AssetDatabase.CreateAsset(asset, ASSET_FILE_PATH);
            }

            return asset;
        }

        [Serializable]
        public class Avatar
        {
            public string avatarName;
            public SceneAsset sceneAsset;
            public string globalObjectId;

            public Avatar(SceneAsset sceneAsset, GameObject avatar)
            {
                this.sceneAsset = sceneAsset;
                avatarName = avatar.name;
                globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(avatar).ToString();
            }
        }
    }
}
