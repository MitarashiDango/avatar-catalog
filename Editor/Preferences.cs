using System;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    [Serializable]
    public class Preferences : ScriptableObject
    {
        public static readonly float AVATAR_CATALOG_MIN_ITEM_SIZE = 128;
        public static readonly float AVATAR_CATALOG_MAX_ITEM_SIZE = 256;
        public static readonly float DEFAULT_AVATAR_CATALOG_MAX_ITEM_SIZE = 160;

        public static string ASSET_FILE_PATH = "Assets/Avatar Catalog User Data/Preferences.asset";

        [SerializeField]
        private float _avatarCatalogItemSize = DEFAULT_AVATAR_CATALOG_MAX_ITEM_SIZE;

        public float avatarCatalogItemSize
        {
            get => _avatarCatalogItemSize;
            set
            {
                if (value < AVATAR_CATALOG_MIN_ITEM_SIZE)
                {
                    _avatarCatalogItemSize = AVATAR_CATALOG_MIN_ITEM_SIZE;
                    return;
                }

                if (value > AVATAR_CATALOG_MAX_ITEM_SIZE)
                {
                    _avatarCatalogItemSize = AVATAR_CATALOG_MAX_ITEM_SIZE;
                    return;
                }

                _avatarCatalogItemSize = value;
            }
        }

        public void Save()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Preferences>(ASSET_FILE_PATH);
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

        public static Preferences LoadOrNew()
        {
            var asset = Load();
            if (asset)
            {
                return asset;
            }

            return new Preferences();
        }

        public static Preferences Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Preferences>(ASSET_FILE_PATH);
            if (asset)
            {
                return asset;
            }

            return null;
        }

        public static Preferences CreateOrLoad()
        {
            var asset = Load();
            if (!asset)
            {
                asset = ScriptableObject.CreateInstance<Preferences>();
                AssetDatabase.CreateAsset(asset, ASSET_FILE_PATH);
            }

            return asset;
        }
    }
}
