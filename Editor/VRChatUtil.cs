using System;
using System.Threading.Tasks;
using UnityEngine;
using VRC.Core;

namespace MitarashiDango.AvatarCatalog
{
    public class VRChatUtil
    {
        public static void InitializeRemoteConfig()
        {
            if (ConfigManager.RemoteConfig.IsInitialized())
            {
                return;
            }

            API.SetOnlineMode(true);
            ConfigManager.RemoteConfig.Init();
        }

        public static async Task LogIn()
        {
            if (APIUser.IsLoggedIn)
            {
                return;
            }

            VRCSdkControlPanel.InitAccount();
            await FetchCurrentUser();
        }

        public static async Task FetchCurrentUser()
        {
            if (!ApiCredentials.Load())
            {
                return;
            }

            var tcs = new TaskCompletionSource<bool>();
            APIUser.InitialFetchCurrentUser(c =>
            {
                if (c.Model is not APIUser apiUser)
                {
                    tcs.SetException(new Exception("failed to load user, please login again with your VRChat account"));
                    return;
                }

                AnalyticsSDK.LoggedInUserChanged(apiUser);
                tcs.SetResult(true);
            }, err =>
            {
                Debug.LogError(err.Error);
                tcs.SetResult(false);
            });

            await tcs.Task;
        }
    }
}
