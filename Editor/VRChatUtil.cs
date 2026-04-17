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
    /// <summary>
    /// VRChat のログインセッションが切れている、または有効ではない場合にスローされる例外
    /// </summary>
    public class VRChatSessionExpiredException : Exception
    {
        public VRChatSessionExpiredException(string message) : base(message)
        {
        }

        public VRChatSessionExpiredException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class VRChatUtil
    {
        private const string AgreementCode = "content.copyright.owned";
        private const string VRCCopyrightAgreementContentListKey = "VRCSdkControlPanel.CopyrightAgreement.ContentList";

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
                try
                {
                    if (c?.Model is not APIUser apiUser)
                    {
                        // API は応答したが APIUser として解釈できなかった。セッション切れの可能性が高い。
                        tcs.TrySetException(new VRChatSessionExpiredException(
                            "VRChat のログインセッションが有効ではありません。"));
                        return;
                    }

                    AnalyticsSDK.LoggedInUserChanged(apiUser);
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }, err =>
            {
                try
                {
                    string errorMessage = null;
                    var errorAccessFailed = false;
                    try
                    {
                        errorMessage = err?.Error;
                    }
                    catch
                    {
                        // err.Error の getter が例外を投げる場合 (= SDK 側の状態異常、セッション切れ時に観測される)
                        errorAccessFailed = true;
                    }

                    if (err == null || errorAccessFailed || string.IsNullOrEmpty(errorMessage))
                    {
                        tcs.TrySetException(new VRChatSessionExpiredException(
                            "VRChat のログインセッションが有効ではありません。"));
                        return;
                    }

                    tcs.TrySetException(new Exception(errorMessage));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            await tcs.Task;
        }

        public static List<string> AgreedContentThisSession
        {
            get
            {
                var agreedContents = SessionState.GetString(VRCCopyrightAgreementContentListKey, null);
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
            SessionState.SetString(VRCCopyrightAgreementContentListKey, string.Join(";", agreedContents));

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
