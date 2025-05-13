using System;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    [Serializable]
    public class Preferences : ScriptableObject
    {
        public static readonly float AvatarCatalogMinItemSize = 128;
        public static readonly float AvatarCatalogMaxItemSize = 256;
        public static readonly float DefaultAvatarCatalogMaxItemSize = 160;

        public static string AssetFilePath = "Assets/Avatar Catalog User Data/Preferences.asset";

        [SerializeField]
        private float _avatarCatalogItemSize = DefaultAvatarCatalogMaxItemSize;

        public float avatarCatalogItemSize
        {
            get => _avatarCatalogItemSize;
            set
            {
                if (value < AvatarCatalogMinItemSize)
                {
                    _avatarCatalogItemSize = AvatarCatalogMinItemSize;
                    return;
                }

                if (value > AvatarCatalogMaxItemSize)
                {
                    _avatarCatalogItemSize = AvatarCatalogMaxItemSize;
                    return;
                }

                _avatarCatalogItemSize = value;
            }
        }

        public static void Save(Preferences asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        public static Preferences LoadOrCreateFile()
        {
            var asset = Load();
            if (asset != null)
            {
                return asset;
            }

            asset = CreateInstance<Preferences>();
            AssetDatabase.CreateAsset(asset, AssetFilePath);

            return asset;
        }

        public static Preferences Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Preferences>(AssetFilePath);
            if (asset != null)
            {
                return asset;
            }

            return null;
        }
    }
}
