//
// Azure Media Services REST API v2 - Functions
//
// MonitorBlobContainerCopyStatus - This function monitors blob copy.
//
//  Input:
//      {
//          "storageAccountName": "amsstorage",                                     // Storage account name of the asset for copy destination
//          "destinationContainer": "asset-2e26fd08-1436-44b1-8b92-882a757071dd",   // Container Name of the asset for copy destination
//          "fileNames":  [ "filename.mp4" , "filename2.mp4"],                      // File names of copy target contents
//      }
//  Output:
//      {
//          "copyStatus": true|false, // Return Blob Copy Status: true or false
//          "blobCopyStatusList": [
//              {
//                  "blobName": "Name of blob",
//                  "blobCopyStatus": 2  // Return Blob CopyStatus (see below)
//              }
//          ]
//          // https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.storage.blob.copystatus?view=azure-dotnet
//          //      Invalid     0	The copy status is invalid.
//          //      Pending     1	The copy operation is pending.
//          //      Success     2	The copy operation succeeded.
//          //      Aborted     3	The copy operation has been aborted.
//          //      Failed      4	The copy operation encountered an error.
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
//using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using advanced_vod_functions.SharedLibs;


namespace advanced_vod_functions
{
    public static class MonitorBlobContainerCopyStatus
    {
        [FunctionName("MonitorBlobContainerCopyStatus")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"AMS v2 Function - MonitorBlobContainerCopyStatus was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            // Validate input objects
            if (data.assetId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetId in the input object" });
            if (data.destinationContainer == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass destinationContainer in the input object" });
            string assetId = data.assetId;
            string destinationContainerName = data.destinationContainer;
            List<string> fileNames = null;
            if (data.fileNames != null)
            {
                fileNames = ((JArray)data.fileNames).ToObject<List<string>>();
            }

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            bool copyStatus = true;
            JArray blobCopyStatusList = new JArray();

            try
            {
                // Setup blob container
                /*
                // Azure AD Storage API access
                string accessToken = BlobStorageHelper.GetUserOAuthToken(amsCredentials.AmsAadTenantDomain, amsCredentials.AmsClientId, amsCredentials.AmsClientSecret);
                TokenCredential tokenCredential = new TokenCredential(accessToken);
                StorageCredentials storageCredentials = new StorageCredentials(tokenCredential);
                CloudBlobContainer destinationBlobContainer = BlobStorageHelper.GetCloudBlobContainer(storageCredentials, storageAccountName, destinationContainerName);
                */
                CloudBlobContainer destinationBlobContainer = BlobStorageHelper.GetCloudBlobContainer(BlobStorageHelper.AmsStorageAccountName, BlobStorageHelper.AmsStorageAccountKey, destinationContainerName);

                string blobPrefix = null;
                bool useFlatBlobListing = true;

                log.Info("Checking CopyStatus of all blobs in the source container...");
                var destBlobList = destinationBlobContainer.ListBlobs(blobPrefix, useFlatBlobListing, BlobListingDetails.Copy);
                foreach (var dest in destBlobList)
                {
                    var destBlob = dest as CloudBlob;
                    bool found = false;
                    foreach (var fileName in fileNames)
                    {
                        if (fileName == destBlob.Name)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found == false) break;

                    if (destBlob.CopyState.Status == CopyStatus.Aborted || destBlob.CopyState.Status == CopyStatus.Failed)
                    {
                        // Log the copy status description for diagnostics and restart copy
                        await destBlob.StartCopyAsync(destBlob.CopyState.Source);
                        copyStatus = false;
                    }
                    else if (destBlob.CopyState.Status == CopyStatus.Pending)
                    {
                        // We need to continue waiting for this pending copy
                        // However, let us log copy state for diagnostics
                        copyStatus = false;
                    }
                    // else we completed this pending copy

                    string blobName = destBlob.Name as string;
                    int blobCopyStatus = (int)(destBlob.CopyState.Status);
                    JObject o = new JObject();
                    o["blobName"] = blobName;
                    o["blobCopyStatus"] = blobCopyStatus;
                    blobCopyStatusList.Add(o);
                }
            }
            catch (Exception e)
            {
                log.Info($"ERROR: Exception {e}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            JObject result = new JObject();
            result["copyStatus"] = copyStatus;
            result["blobCopyStatusList"] = blobCopyStatusList;

            return req.CreateResponse(HttpStatusCode.OK, result);
        }
    }
}
