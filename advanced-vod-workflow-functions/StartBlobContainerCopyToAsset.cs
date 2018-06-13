//
// Azure Media Services REST API v2 Functions
//
// StartBlobContainerCopyToAsset - This function starts copying blob container to the asset.
//
//  Input:
//      {
//          "assetId": "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",  // Id of the asset for copy destination
//          "sourceStorageAccountName":  "mediaimports",                    // Name of the storage account for copy source
//          "sourceStorageAccountKey":  "xxxkey==",                         // Key of the storage account for copy source
//          "sourceContainer":  "movie-trailer",                            // Blob container name of the storage account for copy source
//          "fileNames":  [ "filename.mp4" , "filename2.mp4" ]              // File names of source contents
//      }
//  Output:
//      {
//          "destinationContainer": "asset-2e26fd08-1436-44b1-8b92-882a757071dd"
//                                                                          // Container Name of the asset for copy destination
//      }
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using advanced_vod_functions.SharedLibs;


namespace advanced_vod_functions
{
    public static class StartBlobContainerCopyToAsset
    {
        private static CloudMediaContext _context = null;

        [FunctionName("StartBlobContainerCopyToAsset")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"AMS v2 Function - StartBlobContainerCopyToAsset was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            // Validate input objects
            if (data.assetId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetId in the input object" });
            if (data.sourceStorageAccountName == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass sourceStorageAccountName in the input object" });
            if (data.sourceStorageAccountKey == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass sourceStorageAccountKey in the input object" });
            if (data.sourceContainer == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass sourceContainer in the input object" });
            //if (data.destinationContainer == null)
            //    return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass destinationContainer in the input object" });
            string assetId = data.assetId;
            string _sourceStorageAccountName = data.sourceStorageAccountName;
            string _sourceStorageAccountKey = data.sourceStorageAccountKey;
            string sourceContainerName = data.sourceContainer;
            //string destinationContainerName = data.destinationContainer;
            List<string> fileNames = null;
            if (data.fileNames != null)
            {
                fileNames = ((JArray)data.fileNames).ToObject<List<string>>();
            }

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            IAsset asset = null;

            try
            {
                // Load AMS account context
                log.Info($"Using AMS v2 REST API Endpoint : {amsCredentials.AmsRestApiEndpoint.ToString()}");

                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                    new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                    AzureEnvironments.AzureCloudEnvironment);
                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);
                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);

                // Get the Asset
                asset = _context.Assets.Where(a => a.Id == assetId).FirstOrDefault();
                if (asset == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });
                }

                // Setup blob container
                CloudBlobContainer sourceBlobContainer = BlobStorageHelper.GetCloudBlobContainer(_sourceStorageAccountName, _sourceStorageAccountKey, sourceContainerName);
                sourceBlobContainer.CreateIfNotExists();

                /*
                // Azure AD Storage API access
                string accessToken = BlobStorageHelper.GetUserOAuthToken(amsCredentials.AmsAadTenantDomain, amsCredentials.AmsClientId, amsCredentials.AmsClientSecret);
                TokenCredential tokenCredential = new TokenCredential(accessToken);
                StorageCredentials storageCredentials = new StorageCredentials(tokenCredential);
                string[] uri = asset.Uri.Host.Split('.');
                string storageAccountName = uri[0];
                CloudBlobContainer destinationBlobContainer = BlobStorageHelper.GetCloudBlobContainer(storageCredentials, storageAccountName, asset.Uri.Segments[1]);
                */
                CloudBlobContainer destinationBlobContainer = BlobStorageHelper.GetCloudBlobContainer(BlobStorageHelper.AmsStorageAccountName, BlobStorageHelper.AmsStorageAccountKey, asset.Uri.Segments[1]);

                // Copy Source Blob container into Destination Blob container that is associated with the asset.
                BlobStorageHelper.CopyBlobsAsync(sourceBlobContainer, destinationBlobContainer, fileNames, log);
            }
            catch (Exception e)
            {
                log.Info($"Exception {e}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                destinationContainer = asset.Uri.Segments[1]
            });
        }
    }
}
