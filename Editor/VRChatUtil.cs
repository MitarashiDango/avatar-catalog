using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using VRC.Core;
using VRC.SDKBase;
using VRC.SDKBase.Editor.Api;

namespace MitarashiDango.AvatarCatalog
{
    public class VRChatUtil
    {
        private const string AgreementCode = "content.copyright.owned";
        private const string VRCCopyrightAgreementCotentListKey = "VRCSdkControlPanel.CopyrightAgreement.ContentList";

        public static void InitializeRemoteConfig()
        {
            if (ConfigManager.RemoteConfig.IsInitialized())
            {
                return;
            }

            API.SetOnlineMode(true);
            ConfigManager.RemoteConfig.Init();
        }

        public static async Task<bool> LogIn()
        {
            if (APIUser.IsLoggedIn)
            {
                return true;
            }

            if (!ApiCredentials.Load())
            {
                return APIUser.IsLoggedIn;
            }

            await FetchCurrentUser();

            return APIUser.IsLoggedIn;
        }

        private static async Task FetchCurrentUser()
        {
            if (!APIUser.IsLoggedIn && !ApiCredentials.Load())
            {
                throw new Exception("failed to load api credentials.");
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
                tcs.SetException(new Exception(string.IsNullOrEmpty(err.Error) ? "unspecified error has occurred." : err.Error));
            });

            await tcs.Task;
        }

        public static List<string> AgreedContentThisSession
        {
            get
            {
                var agreedContents = SessionState.GetString(VRCCopyrightAgreementCotentListKey, null);
                if (string.IsNullOrEmpty(agreedContents))
                {
                    return new List<string>();
                }

                return agreedContents.Split(";").ToList();
            }
        }

        public static async Task AgreeCopyrightAgreement(string contentId)
        {
            var agreedContents = AgreedContentThisSession;
            if (agreedContents.Contains(contentId))
            {
                return;
            }

            agreedContents.Add(contentId);
            SessionState.SetString(VRCCopyrightAgreementCotentListKey, string.Join(";", agreedContents));

            var vrcAgreement = new VRCAgreement
            {
                AgreementCode = AgreementCode,
                AgreementFulltext = VRCCopyrightAgreement.AgreementText,
                ContentId = contentId,
                Version = 1,
            };

            await VRCApi.ContentUploadConsent(vrcAgreement);
        }

        public static void ClearVRCSDKIssues()
        {
            if (VRCSdkControlPanel.window)
            {
                VRCSdkControlPanel.window.ResetIssues();
                VRCSdkControlPanel.window.CheckedForIssues = true;
            }
        }
    }
}
