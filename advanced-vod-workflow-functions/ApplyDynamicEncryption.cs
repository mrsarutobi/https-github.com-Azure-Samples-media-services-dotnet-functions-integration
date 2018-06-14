//
// Azure Media Services REST API v2 Functions
//
// ApplyDynamicEncryption - This function applies Dynamic Encryption to the asset.
//
//  Input:
//      {
//          "assetId": "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",          // Id of the asset for copy destination
//          "contentKeyAuthorizationPolicyId": "nb:ckpid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",
//                                                                                  // Id of the ContentKeyAuthorizationPolicy object
//          "assetDeliveryPolicyId": "nb:adpid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",
//                                                                                  // Id of the AssetDeliveryPolicy object
//          "contentKeyType": "CommonEncryption",                                   // Name of the ContentKeyType
//          // https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.mediaservices.client.contentkeytype?view=azure-dotnet
//          //  CommonEncryption
//          //  CommonEncryptionCbcs
//          //  EnvelopeEncryption
//          "contentKeyName": "Common Encryption ContentKey"                        // (Optional) Name of the ContentKey object
//      }
//  Output:
//      {
//          "contentKeyId": "nb:kid:UUID:489a97f4-9a31-4174-ac92-0c76e8dbdc06"      // Id of the ContentKey object
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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using advanced_vod_functions.SharedLibs;


namespace advanced_vod_functions
{
    public static class ApplyDynamicEncryption
    {
        private static CloudMediaContext _context = null;

        [FunctionName("ApplyDynamicEncryption")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"AMS v2 Function - CreateContentKeyAuthorizationPolicy was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            // Validate input objects
            if (data.assetId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetId in the input object" });
            if (data.contentKeyAuthorizationPolicyId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass contentKeyAuthorizationPolicyId in the input object" });
            if (data.assetDeliveryPolicyId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetDeliveryPolicyId in the input object" });
            if (data.contentKeyType == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass contentKeyType in the input object" });
            string assetId = data.assetId;
            string contentKeyAuthorizationPolicyId = data.contentKeyAuthorizationPolicyId;
            string assetDeliveryPolicyId = data.assetDeliveryPolicyId;
            string contentKeyTypeName = data.contentKeyType;
            if (!MediaServicesHelper.AMSContentKeyType.ContainsKey(contentKeyTypeName))
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass a valid contentKeyType in the input object" });
            ContentKeyType contentKeyType = MediaServicesHelper.AMSContentKeyType[contentKeyTypeName];
            if (contentKeyType != ContentKeyType.CommonEncryption && contentKeyType != ContentKeyType.CommonEncryptionCbcs && contentKeyType != ContentKeyType.EnvelopeEncryption)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass a valid contentKeyType in the input object" });
            string contentKeyName = null;
            if (data.contentKeyName != null) contentKeyName = data.contentKeyName;

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            IAsset asset = null;
            IContentKeyAuthorizationPolicy ckaPolicy = null;
            IAssetDeliveryPolicy adPolicy = null;
            IContentKey contentKey = null;

            try
            {
                // Load AMS account context
                log.Info($"Using AMS v2 REST API Endpoint : {amsCredentials.AmsRestApiEndpoint.ToString()}");

                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                    new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                    AzureEnvironments.AzureCloudEnvironment);
                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);
                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);

                // Get the Asset, ContentKeyAuthorizationPolicy, AssetDeliveryPolicy
                asset = _context.Assets.Where(a => a.Id == assetId).FirstOrDefault();
                if (asset == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });
                }
                ckaPolicy = _context.ContentKeyAuthorizationPolicies.Where(p => p.Id == contentKeyAuthorizationPolicyId).Single();
                if (ckaPolicy == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "ContentKeyAuthorizationPolicy not found" });
                }
                adPolicy = _context.AssetDeliveryPolicies.Where(p => p.Id == assetDeliveryPolicyId).Single();
                if (adPolicy == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "AssetDeliveryPolicy not found" });
                }
                switch (contentKeyType)
                {
                    case ContentKeyType.CommonEncryption:
                        if (contentKeyName == null) contentKeyName = "Common Encryption ContentKey";
                        contentKey = MediaServicesHelper.CreateContentKey(_context, contentKeyName, ContentKeyType.CommonEncryption);
                        break;
                    case ContentKeyType.CommonEncryptionCbcs:
                        if (contentKeyName == null) contentKeyName = "Common Encryption CBCS ContentKey";
                        contentKey = MediaServicesHelper.CreateContentKey(_context, contentKeyName, ContentKeyType.CommonEncryptionCbcs);
                        break;
                    case ContentKeyType.EnvelopeEncryption:
                        if (contentKeyName == null) contentKeyName = "Envelope Encryption ContentKey";
                        contentKey = MediaServicesHelper.CreateContentKey(_context, contentKeyName, ContentKeyType.EnvelopeEncryption);
                        break;
                }
                asset.ContentKeys.Add(contentKey);
                contentKey.AuthorizationPolicyId = ckaPolicy.Id;
                contentKey = contentKey.UpdateAsync().Result;
                asset.DeliveryPolicies.Add(adPolicy);
            }
            catch (Exception e)
            {
                log.Info($"ERROR: Exception {e}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                contentKeyId = contentKey.Id
            });
        }
    }
}
