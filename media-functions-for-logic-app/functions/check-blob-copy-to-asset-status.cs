/*

Azure Media Services REST API v2 Function
 
This function monitor the copy of files (blobs) to a new asset previously created.

Input:
{
      "destinationContainer" : "mycontainer",
      "delay": 15000 // optional (default is 5000)
      "assetStorage" :"amsstore01" // optional. Name of attached storage where to create the asset. Please use the function setting variable MediaServicesAttachedStorageCredentials to pass the credentials
   
}
Output:
{
      "copyStatus": 2 // status
       "isRunning" : "False"
       "isSuccessful" : "False"
}

*/

using System;
using System.Net;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System.Threading.Tasks;
using System.Net.Http;

namespace media_functions_for_logic_app
{
    public static class check_blob_copy_to_asset_status
    {

        [FunctionName("check-blob-copy-to-asset-status")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);
            log.Info("Request : " + jsonContent);

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();

            var attachedstoragecred = KeyHelper.ReturnStorageCredentials();

            // Validate input objects
            int delay = 5000;
            if (data.destinationContainer == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass DestinationContainer in the input object" });
            if (data.delay != null)
                delay = data.delay;
            log.Info("Input - DestinationContainer : " + data.destinationContainer);
            //log.Info("delay : " + delay);

            log.Info($"Wait " + delay + "(ms)");
            System.Threading.Thread.Sleep(delay);


            string storname = amsCredentials.StorageAccountName;
            string storkey = amsCredentials.StorageAccountKey;
            if (data.assetStorage != null)
            {
                string assetstor = (string)data.assetStorage;
                if (assetstor != amsCredentials.StorageAccountName)
                {
                    if (attachedstoragecred.ContainsKey(assetstor)) // asset is using another storage than default but we have the key
                    {
                        storname = assetstor;
                        storkey = attachedstoragecred[storname];
                    }
                    else // we don't have the key for that storage
                    {
                        log.Info($"Face redaction Asset is in {assetstor} and key is not provided in MediaServicesAttachedStorageCredentials application settings");
                        return req.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            error = "Storage key is missing"
                        });
                    }
                }
            }

            string destinationContainerName = data.destinationContainer;
            CloudBlobContainer destinationBlobContainer = CopyBlobHelpers.GetCloudBlobContainer(storname, storkey, destinationContainerName);

            CopyStatus copyStatus = CopyStatus.Success;
            try
            {
                string blobPrefix = null;
                bool useFlatBlobListing = true;
                var destBlobList = destinationBlobContainer.ListBlobs(blobPrefix, useFlatBlobListing, BlobListingDetails.Copy);
                foreach (var dest in destBlobList)
                {
                    var destBlob = dest as CloudBlob;
                    if (destBlob.CopyState.Status == CopyStatus.Aborted || destBlob.CopyState.Status == CopyStatus.Failed)
                    {
                        // Log the copy status description for diagnostics and restart copy
                        destBlob.StartCopyAsync(destBlob.CopyState.Source);
                        copyStatus = CopyStatus.Pending;
                    }
                    else if (destBlob.CopyState.Status == CopyStatus.Pending)
                    {
                        // We need to continue waiting for this pending copy
                        // However, let us log copy state for diagnostics
                        copyStatus = CopyStatus.Pending;
                    }
                    // else we completed this pending copy
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                log.Info($"ERROR: Exception {message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                copyStatus = copyStatus,
                isRunning = (copyStatus == CopyStatus.Pending).ToString(),
                isSuccessful = (copyStatus == CopyStatus.Success).ToString()
            });
        }
    }
}