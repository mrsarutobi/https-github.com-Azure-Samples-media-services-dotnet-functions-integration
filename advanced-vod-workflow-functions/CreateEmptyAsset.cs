//
// Azure Media Services REST API v2 Functions
//
// CreateEmptyAsset - This function creates an empty asset.
//
//  Input:
//      {
//          "assetName":            "Asset Name",       // Name of the asset
//          "assetCreationOption":  "None",             // (Optional) Name of asset creation option
//              // https://docs.microsoft.com/en-us/rest/api/media/operations/asset#asset_entity_properties
//              //      None                            Normal asset type (no encryption)
//              //      StorageEncrypted                Storage Encryption encrypted asset type
//              //      CommonEncryptionProtected       Common Encryption encrypted asset type
//              //      EnvelopeEncryptionProtected     Envelope Encryption encrypted asset type
//          "assetStorageAccount":  "storage01"         // (Optional) Name of attached storage account where to create the asset
//      }
//  Output:
//      {
//          "assetId": "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810"   // Id of the asset created
//          "destinationContainer": "asset-2e26fd08-1436-44b1-8b92-882a757071dd"
//                                                                          // Container Name of the asset for copy destination
//      }
//

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;

using Newtonsoft.Json;

using advanced_vod_functions.SharedLibs;


namespace advanced_vod_functions
{
    public static class CreateEmptyAsset
    {
        private static CloudMediaContext _context = null;

        [FunctionName("CreateEmptyAsset")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"AMS v2 Function - CreateEmptyAsset was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            // parameter handling
            if (data.assetName == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetName in the input object" });
            string assetName = data.assetName;
            AssetCreationOptions assetCreationOption = AssetCreationOptions.None;
            string assetCreationOptionName = data.assetCreationOption;
            if (assetCreationOptionName != null)
                if (MediaServicesHelper.AMSAssetCreationOptions.ContainsKey(assetCreationOptionName))
                    assetCreationOption = MediaServicesHelper.AMSAssetCreationOptions[assetCreationOptionName];
            string assetStorageAccount = null;
            if (data.assetStorageAccount != null)
                assetStorageAccount = data.assetStorageAccount;


            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            IAsset newAsset = null;

            try
            {
                // Load AMS account context
                log.Info($"Using AMS v2 REST API Endpoint : {amsCredentials.AmsRestApiEndpoint.ToString()}");

                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                    new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                    AzureEnvironments.AzureCloudEnvironment);
                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);
                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);

                // Create Asset
                newAsset = _context.Assets.Create(assetName, assetCreationOption);
                log.Info("Created Azure Media Services Asset : ");
            }
            catch (Exception e)
            {
                log.Info($"Exception {e}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                assetId = newAsset.Id,
                destinationContainer = newAsset.Uri.Segments[1]
            });
        }
    }
}
