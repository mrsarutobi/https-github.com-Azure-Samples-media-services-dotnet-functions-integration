/*
Azure Media Services REST API v2 Function

This function add dynamic encryption to the asset or program. It attached the 

Input:
{
    "assetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", Id of the source asset (or you pass programId, or the values channelName and programName)
    "programId" : "nb:pgid:UUID:5d547b03-3b56-47ae-a479-88cddf21a7fa",  or program Id
    "channelName", 
    "programName",
    "contentKeyAuthorizationPolicyId": "nb:ckpid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810", // Optional, Id of the ContentKeyAuthorizationPolicy object
    "assetDeliveryPolicyId": "nb:adpid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",           // Id of the AssetDeliveryPolicy object    
    "contentKeyType": "CommonEncryption",                                                    // Name of the ContentKeyType
          // https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.mediaservices.client.contentkeytype?view=azure-dotnet
          //  CommonEncryption
          //  CommonEncryptionCbcs
          //  EnvelopeEncryption
    "contentKeyName": "Common Encryption ContentKey"                        // Optional, Name of the ContentKey object
    "keyId" : "",      Optional
    "contentKey" :"" , Optional, base64 of the content key
}

Output:
{
   "contentKeyId": "nb:kid:UUID:489a97f4-9a31-4174-ac92-0c76e8dbdc06"      // Id of the ContentKey object
   "assetId" : ""  // Id of the asset
}
*/

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client.ContentKeyAuthorization;
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption;

namespace media_functions_for_logic_app
{
    public static class add_dynamic_encryption
    {
        private static CloudMediaContext _context = null;

        [FunctionName("add-dynamic-encryption")]

        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"AMS v2 Function - CreateContentKeyAuthorizationPolicy was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            // Validate input objects
            if (data.assetId == null && data.programId == null && data.channelName == null && data.programName == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetId or programID or channelName/programName in the input object" });

            if (data.contentKeyAuthorizationPolicyId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass contentKeyAuthorizationPolicyId in the input object" });

            if (data.assetDeliveryPolicyId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetId in the input object" });

            if (data.contentKeyType == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass contentKeyType in the input object" });

            string assetId = data.assetId;
            string programId = data.programId;
            string channelName = data.channelName;
            string programName = data.programName;
            string contentKeyAuthorizationPolicyId = data.contentKeyAuthorizationPolicyId;
            string assetDeliveryPolicyId = data.assetDeliveryPolicyId;
            string contentKeyTypeName = data.contentKeyType;
            string contentKeyId = data.keyId;
            string contentKeySecret = data.contentKey;

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

                // Let's get the asset
                if (assetId != null)
                {
                    // Get the Asset, ContentKeyAuthorizationPolicy, AssetDeliveryPolicy
                    asset = _context.Assets.Where(a => a.Id == assetId).FirstOrDefault();
                    if (asset == null)
                    {
                        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });
                    }

                }
                else if (programId != null)
                {
                    var program = _context.Programs.Where(p => p.Id == programId).FirstOrDefault();
                    if (program == null)
                    {
                        log.Info("Program not found");
                        return req.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            error = "Program not found"
                        });
                    }
                    asset = program.Asset;
                }
                else // with channelName and programName
                {
                    // find the Channel, Program and Asset
                    var channel = _context.Channels.Where(c => c.Name == channelName).FirstOrDefault();
                    if (channel == null)
                    {
                        log.Info("Channel not found");
                        return req.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            error = "Channel not found"
                        });
                    }

                    var program = channel.Programs.Where(p => p.Name == programName).FirstOrDefault();
                    if (program == null)
                    {
                        log.Info("Program not found");
                        return req.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            error = "Program not found"
                        });
                    }
                    asset = program.Asset;
                }

                log.Info($"Using asset Id : {asset.Id}");

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

                if (contentKeyId != null)
                {
                    string keyiddwitprefix = "";

                    if (contentKeyId.StartsWith("nb:kid:UUID:"))
                    {
                        keyiddwitprefix = contentKeyId;
                        contentKeyId = contentKeyId.Substring(12);
                    }
                    else
                    {
                        keyiddwitprefix = "nb:kid:UUID:" + contentKeyId;
                    }

                    // let's retrieve the key if it exists already
                    contentKey = _context.ContentKeys.Where(k => k.Id == keyiddwitprefix).FirstOrDefault();
                }

                if (contentKey == null) // let's create it as it was not found or delivered to the function
                {
                    switch (contentKeyType)
                    {
                        case ContentKeyType.CommonEncryption:
                            if (contentKeyName == null) contentKeyName = "Common Encryption ContentKey";
                            contentKey = MediaServicesHelper.CreateContentKey(_context, contentKeyName, ContentKeyType.CommonEncryption, contentKeyId, contentKeySecret);
                            break;
                        case ContentKeyType.CommonEncryptionCbcs:
                            if (contentKeyName == null) contentKeyName = "Common Encryption CBCS ContentKey";
                            contentKey = MediaServicesHelper.CreateContentKey(_context, contentKeyName, ContentKeyType.CommonEncryptionCbcs, contentKeyId, contentKeySecret);
                            break;
                        case ContentKeyType.EnvelopeEncryption:
                            if (contentKeyName == null) contentKeyName = "Envelope Encryption ContentKey";
                            contentKey = MediaServicesHelper.CreateContentKey(_context, contentKeyName, ContentKeyType.EnvelopeEncryption, contentKeyId, contentKeySecret);
                            break;
                    }
                }

                asset.ContentKeys.Add(contentKey);
                contentKey.AuthorizationPolicyId = ckaPolicy.Id;
                contentKey = contentKey.UpdateAsync().Result;
                asset.DeliveryPolicies.Add(adPolicy);
            }
            catch (Exception ex)
            {
                string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                log.Info($"ERROR: Exception {message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                contentKeyId = contentKey.Id,
                assetId = asset.Id
            });

        }
    }
}
