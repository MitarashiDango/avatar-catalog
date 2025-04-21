using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    [CreateAssetMenu(menuName = "Avatar Catalog/Avatar Metadata")]
    public class AvatarMetadata : ScriptableObject
    {
        public string avatarGlobalObjectId = "";
        public string comment = "";
        public List<string> tags = new List<string>();
    }
}
