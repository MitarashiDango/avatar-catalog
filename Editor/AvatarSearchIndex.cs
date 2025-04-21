using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarSearchIndex : ScriptableObject
    {
        public List<AvatarSearchIndexEntry> entries = new List<AvatarSearchIndexEntry>();

        [Serializable]
        public class AvatarSearchIndexEntry
        {
            public string globalObjectId = "";
            public AvatarMetadata avatarMetadata;

            public bool IsMatchAvatarMetadata(string[] searchWords)
            {
                if (!avatarMetadata)
                {
                    return false;
                }

                foreach (var searchWord in searchWords)
                {
                    if (avatarMetadata.comment.IndexOf(searchWord) > -1)
                    {
                        continue;
                    }

                    if (avatarMetadata.tags.Where(tag => tag == searchWord).Count() == 0)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
