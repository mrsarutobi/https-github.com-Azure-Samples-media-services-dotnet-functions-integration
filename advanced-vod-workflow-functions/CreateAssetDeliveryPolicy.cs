//
// Azure Media Services REST API v2 Functions
//
// CreateAssetDeliveryPolicy - This function creates new AssetDeliveryPolicy object in the AMS account.
//
//  Input:
//      {
//          "assetDeliveryPolicyName": "Clear Policy",              // Name of the AssetDeliveryPolicy object
//          "assetDeliveryPolicyType": "NoDynamicEncryption",       // Type of the AssetDeliveryPolicy object
//          // https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.mediaservices.client.dynamicencryption.assetdeliverypolicytype?view=azure-dotnet
//          //  NoDynamicEncryption
//          //  DynamicEnvelopeEncryption
//          //  DynamicCommonEncryption
//          //  DynamicCommonEncryptionCbcs
//          "assetDeliveryPolicyProtocol": [
//              "SmoothStreaming",
//              "Dash",
//              "HLS"
//          ],
//          // https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.mediaservices.client.dynamicencryption.assetdeliveryprotocol?view=azure-dotnet
//          //  SmoothStreaming
//          //  Dash
//          //  HLS
//          //  Hds
//          //  ProgressiveDownload
//          //  All
//          "assetDeliveryPolicyContentProtection": [               // (Optional) List of the content protection technology
//                                                                  //  value: "AESClearKey", "PlayReady", "Widevine", "FairPlay"
//              "PlayReady",
//              "Widevine"
//          ],
//          "fairPlayContentKeyAuthorizationPolicyOptionId": "nb:ckpoid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810"
//                                                          // (Optional) Id of tContent Key Authorization Policy Option for FairPlay
//      }
//  Output:
//      {
//          "assetDeliveryPolicyId": "nb:adpid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",  // Id of the AssetDeliveryPolicy object
//      }
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.MediaServices.Client.ContentKeyAuthorization;
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption;
using Microsoft.WindowsAzure.MediaServices.Client.FairPlay;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using advanced_vod_functions.SharedLibs;


namespace advanced_vod_functions
{
    public static class CreateAssetDeliveryPolicy
    {
        private static CloudMediaContext _context = null;

        [FunctionName("CreateAssetDeliveryPolicy")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"AMS v2 Function - CreateAssetDeliveryPolicy was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            // Validate input objects
            if (data.assetDeliveryPolicyName == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetDeliveryPolicyName in the input object" });
            if (data.assetDeliveryPolicyType == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetDeliveryPolicyType in the input object" });
            if (data.assetDeliveryPolicyProtocol == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetDeliveryPolicyProtocol in the input object" });
            string assetDeliveryPolicyName = data.assetDeliveryPolicyName;
            string assetDeliveryPolicyType = data.assetDeliveryPolicyType;
            if (!MediaServicesHelper.AMSAssetDeliveryPolicyType.ContainsKey(assetDeliveryPolicyType))
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass a valid assetDeliveryPolicyType in the input object" });
            List<string> assetDeliveryPolicyProtocol = ((JArray)data.assetDeliveryPolicyProtocol).ToObject<List<string>>();
            foreach (var p in assetDeliveryPolicyProtocol)
            {
                if (!MediaServicesHelper.AMSAssetDeliveryProtocol.ContainsKey(p))
                    return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass a valid assetDeliveryPolicyProtocol in the input object" });
            }
            List<string> assetDeliveryPolicyContentProtectionNames = null;
            string fairPlayPolicyId = null;
            if (data.assetDeliveryPolicyContentProtection != null)
            {
                assetDeliveryPolicyContentProtectionNames = ((JArray)data.assetDeliveryPolicyContentProtection).ToObject<List<string>>();
                foreach (var p in assetDeliveryPolicyContentProtectionNames)
                {
                    if (!MediaServicesHelper.AMSAssetDeliveryContentProtection.ContainsKey(p))
                        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass a valid assetDeliveryPolicyContentProtection in the input object" });
                    if (MediaServicesHelper.AMSAssetDeliveryContentProtection[p] == MediaServicesHelper.AssetDeliveryContentProtection.FairPlay)
                    {
                        if (fairPlayPolicyId == null)
                            return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass fairPlayContentKeyAuthorizationPolicyOptionId in the input object" });
                        fairPlayPolicyId = data.fairPlayContentKeyAuthorizationPolicyOptionId;
                    }
                }
            }


            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            IAssetDeliveryPolicy policy = null;

            try
            {
                // Load AMS account context
                log.Info($"Using AMS v2 REST API Endpoint : {amsCredentials.AmsRestApiEndpoint.ToString()}");

                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                    new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                    AzureEnvironments.AzureCloudEnvironment);
                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);
                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);

                AssetDeliveryPolicyType assetDeliveryPolicyTypeValue = MediaServicesHelper.AMSAssetDeliveryPolicyType[assetDeliveryPolicyType];
                AssetDeliveryProtocol assetDeliveryPolicyProtocolValue = AssetDeliveryProtocol.None;
                foreach (var p in assetDeliveryPolicyProtocol)
                {
                    assetDeliveryPolicyProtocolValue |= MediaServicesHelper.AMSAssetDeliveryProtocol[p];
                }
                Dictionary<Asset​Delivery​Policy​Configuration​Key, String> assetDeliveryPolicyConfigurationValue = null;
                if (assetDeliveryPolicyContentProtectionNames != null)
                {
                    IContentKeyAuthorizationPolicyOption fairplayPolicyOpiton = null;
                    if (fairPlayPolicyId != null)
                    {
                        fairplayPolicyOpiton = _context.ContentKeyAuthorizationPolicyOptions.Where(p => p.Id == fairPlayPolicyId).Single();
                        if (fairplayPolicyOpiton == null)
                        {
                            return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "FairPlay ContentKeyAuthorizationPolicyOption not found" });
                        }
                    }
                    assetDeliveryPolicyConfigurationValue = CreateDeliveryPolicyConfiguration(assetDeliveryPolicyContentProtectionNames, fairplayPolicyOpiton);
                }
                policy = _context.AssetDeliveryPolicies.Create(assetDeliveryPolicyName,
                    assetDeliveryPolicyTypeValue, assetDeliveryPolicyProtocolValue, assetDeliveryPolicyConfigurationValue);
            }
            catch (Exception e)
            {
                log.Info($"ERROR: Exception {e}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                assetDeliveryPolicyId = policy.Id
            });
        }

        private static Dictionary<Asset​Delivery​Policy​Configuration​Key, String> CreateDeliveryPolicyConfiguration(List<string> assetDeliveryPolicyContentProtectionNames, IContentKeyAuthorizationPolicyOption fairplayPolicyOpiton)
        {
            Dictionary<Asset​Delivery​Policy​Configuration​Key, String> configKeys = new Dictionary<Asset​Delivery​Policy​Configuration​Key, String>();

            foreach (var c in assetDeliveryPolicyContentProtectionNames)
            {
                MediaServicesHelper.AssetDeliveryContentProtection cp = MediaServicesHelper.AMSAssetDeliveryContentProtection[c];
                switch (cp)
                {
                    case MediaServicesHelper.AssetDeliveryContentProtection.AESClearKey:
                        //  Get the Key Delivery Base Url by removing the Query parameter.
                        //  The Dynamic Encryption service will automatically add the correct key identifier to the url
                        //  when it generates the Envelope encrypted content manifest.  Omitting the IV will also cause
                        //  the Dynamice Encryption service to generate a deterministic IV for the content automatically.
                        //  By using the EnvelopeBaseKeyAcquisitionUrl and omitting the IV, this allows the AssetDelivery
                        //  policy to be reused by more than one asset.
                        IContentKey dummyKey1 = CreateDummyContentKey(ContentKeyType.EnvelopeEncryption);
                        Uri aesKeyAcquisitionUri = dummyKey1.GetKeyDeliveryUrl(ContentKeyDeliveryType.BaselineHttp);
                        UriBuilder uriBuilder1 = new UriBuilder(aesKeyAcquisitionUri);
                        uriBuilder1.Query = String.Empty;
                        aesKeyAcquisitionUri = uriBuilder1.Uri;
                        // The following policy configuration specifies: 
                        //   key url that will have KID=<Guid> appended to the envelope and
                        //   the Initialization Vector (IV) to use for the envelope encryption.
                        configKeys.Add(AssetDeliveryPolicyConfigurationKey.EnvelopeBaseKeyAcquisitionUrl, aesKeyAcquisitionUri.ToString());
                        dummyKey1.Delete();
                        break;
                    case MediaServicesHelper.AssetDeliveryContentProtection.PlayReady:
                        IContentKey dummyKey2 = CreateDummyContentKey(ContentKeyType.CommonEncryption);
                        Uri playreadyKeyAcquisitionUrl = dummyKey2.GetKeyDeliveryUrl(ContentKeyDeliveryType.PlayReadyLicense);
                        configKeys.Add(AssetDeliveryPolicyConfigurationKey.PlayReadyLicenseAcquisitionUrl, playreadyKeyAcquisitionUrl.ToString());
                        dummyKey2.Delete();
                        break;
                    case MediaServicesHelper.AssetDeliveryContentProtection.Widevine:
                        IContentKey dummyKey3 = CreateDummyContentKey(ContentKeyType.CommonEncryption);
                        Uri widevineKeyAcquisitionUrl = dummyKey3.GetKeyDeliveryUrl(ContentKeyDeliveryType.Widevine);
                        UriBuilder uriBuilder3 = new UriBuilder(widevineKeyAcquisitionUrl);
                        uriBuilder3.Query = String.Empty;
                        widevineKeyAcquisitionUrl = uriBuilder3.Uri;
                        configKeys.Add(AssetDeliveryPolicyConfigurationKey.WidevineLicenseAcquisitionUrl, widevineKeyAcquisitionUrl.ToString());
                        dummyKey3.Delete();
                        break;
                    case MediaServicesHelper.AssetDeliveryContentProtection.FairPlay:
                        IContentKey dummyKey4 = CreateDummyContentKey(ContentKeyType.CommonEncryption);
                        Uri fairplayKeyAcquisitionUrl = dummyKey4.GetKeyDeliveryUrl(ContentKeyDeliveryType.FairPlay);
                        configKeys.Add(AssetDeliveryPolicyConfigurationKey.FairPlayLicenseAcquisitionUrl, fairplayKeyAcquisitionUrl.ToString().Replace("https://", "skd://"));
                        dummyKey4.Delete();
                        FairPlayConfiguration fairplayConfig = JsonConvert.DeserializeObject<FairPlayConfiguration>(fairplayPolicyOpiton.KeyDeliveryConfiguration);
                        configKeys.Add(AssetDeliveryPolicyConfigurationKey.CommonEncryptionIVForCbcs, fairplayConfig.ContentEncryptionIV);
                        break;
                }
            }

            return configKeys;
        }

        private static IContentKey CreateDummyContentKey(ContentKeyType keyType)
        {
            return MediaServicesHelper.CreateContentKey(_context, "Dummy ContentKey", keyType);
        }
    }
}
