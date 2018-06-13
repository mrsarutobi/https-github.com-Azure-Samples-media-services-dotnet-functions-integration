//
// Azure Media Services REST API v2 Functions
//
// AddAssetFiles - This function adds asset files to the asset.
//
//  Input:
//      {
//          "assetId": "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",          // Id of the asset for copy destination
//          "primaryFileName": "filename.mp4",                                      // File name of the primary AssetFile in the asset
//          "fileNames": [ "filename.mp4" , "filename2.mp4" ]                       // (Optional) File names of copy target contents
//      }
//  Output:
//      {
//          "assetId": "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",          // Id of the asset for copy destination
//      }
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;
//using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using advanced_vod_functions.SharedLibs;


namespace advanced_vod_functions
{
    public static class AddAssetFiles
    {
        private static CloudMediaContext _context = null;

        [FunctionName("AddAssetFiles")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"AMS v2 Function - AddAssetFiles was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            // Validate input objects
            if (data.assetId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetId in the input object" });
            if (data.primaryFileName == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass primaryFileName in the input object" });
            string assetId = data.assetId;
            string primaryFileName = data.primaryFileName;
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

                string destinationContainerName = asset.Uri.Segments[1];
                /*
                // Azure AD Storage API access
                string[] uri = asset.Uri.Host.Split('.');
                string storageAccountName = uri[0];

                string accessToken = BlobStorageHelper.GetUserOAuthToken(amsCredentials.AmsAadTenantDomain, amsCredentials.AmsClientId, amsCredentials.AmsClientSecret);
                TokenCredential tokenCredential = new TokenCredential(accessToken);
                StorageCredentials storageCredentials = new StorageCredentials(tokenCredential);
                CloudBlobContainer destinationBlobContainer = BlobStorageHelper.GetCloudBlobContainer(storageCredentials, storageAccountName, destinationContainerName);
                */
                CloudBlobContainer destinationBlobContainer = BlobStorageHelper.GetCloudBlobContainer(BlobStorageHelper.AmsStorageAccountName, BlobStorageHelper.AmsStorageAccountKey, destinationContainerName);

                foreach (var fileName in fileNames)
                {
                    IAssetFile assetFile = asset.AssetFiles.Create(fileName);
                    CloudBlockBlob blob = destinationBlobContainer.GetBlockBlobReference(fileName);
                    blob.FetchAttributes();
                    assetFile.ContentFileSize = blob.Properties.Length;
                    assetFile.IsPrimary = false;
                    if (fileName == primaryFileName)
                        assetFile.IsPrimary = true;
                    assetFile.Update();
                }
            }
            catch (Exception e)
            {
                log.Info($"ERROR: Exception {e}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                assetId = assetId
            });
        }
    }
}
