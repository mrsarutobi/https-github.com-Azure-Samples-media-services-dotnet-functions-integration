/*

Azure Media Services REST API v2 Function
 
This function monitor the copy of files (blobs) to a storage container.

Input:
{
    "targetStorageAccountName" : "",
    "targetStorageAccountKey": "",
    "targetContainer" : ""
    "delay": 15000 // optional (default is 5000)
    "fileNamePrefix" :"video.mp4" // optional. To monitor only for a specific file name copy. Can be the full name or a prefix
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
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Host;

namespace media_functions_for_logic_app
{
    public static class check_blob_copy_to_container_status
    {

        [FunctionName("check-blob-copy-to-container-status")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);
            log.Info("Request : " + jsonContent);

            // Validate input objects
            int delay = 5000;
            if (data.targetStorageAccountName == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass targetStorageAccountName in the input object" });
            if (data.targetStorageAccountKey == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass targetStorageAccountKey in the input object" });
            if (data.targetContainer == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass targetContainer in the input object" });
            if (data.delay != null)
                delay = data.delay;
            log.Info("Input - DestinationContainer : " + data.targetContainer);
            //log.Info("delay : " + delay);

            log.Info($"Wait " + delay + "(ms)");
            System.Threading.Thread.Sleep(delay);

            string targetStorageAccountName = data.targetStorageAccountName;
            string targetStorageAccountKey = data.targetStorageAccountKey;
            string targetContainer = data.targetContainer;


            CopyStatus copyStatus = CopyStatus.Success;
            try
            {

                CloudBlobContainer destinationBlobContainer = CopyBlobHelpers.GetCloudBlobContainer(targetStorageAccountName, targetStorageAccountKey, targetContainer);

                string blobPrefix = null;

                if (!string.IsNullOrWhiteSpace((string)data.fileNamePrefix))
                {
                    blobPrefix = (string)data.fileNamePrefix;
                    log.Info($"{blobPrefix}");
                }

                bool useFlatBlobListing = true;
                var destBlobList = destinationBlobContainer.ListBlobs(blobPrefix, useFlatBlobListing, BlobListingDetails.Copy);
                foreach (var dest in destBlobList)
                {
                    var destBlob = dest as CloudBlob;
                    if (destBlob.CopyState.Status == CopyStatus.Aborted || destBlob.CopyState.Status == CopyStatus.Failed || destBlob.CopyState.Status == CopyStatus.Invalid)
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
