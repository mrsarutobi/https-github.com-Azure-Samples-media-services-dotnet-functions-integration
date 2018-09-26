/*

Azure Media Services REST API v2 Function
 
This function copy the asset files to a specific azure storage container.

Input:
{
    "assetId" : "the Id of the source asset",
    "targetStorageAccountName" : "",
    "targetStorageAccountKey": "",
    "targetContainer" : "",
    "targetPath" : "testing/folder1/", // optional, copy files to blobs using the targetPath
    "startsWith" : "video", //optional, copy only files that start with name video
    "endsWith" : ".mp4", //optional, copy only files that end with .mp4
}
Output:
{
}

*/

using System;
using System.Net;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs;
using System.Net.Http;
using System.Linq;

namespace media_functions_for_logic_app
{
    public static class start_asset_copy_to_container
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("start-asset-copy-to-container")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);
            log.Info("Request : " + jsonContent);

            var attachedstoragecred = KeyHelper.ReturnStorageCredentials();

            // Validate input objects
            if (data.assetId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetId in the input object" });

            if (data.targetStorageAccountName == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass targetStorageAccountName in the input object" });
            if (data.targetStorageAccountKey == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass targetStorageAccountKey in the input object" });
            if (data.targetContainer == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass targetContainer in the input object" });

            string targetStorageAccountName = data.targetStorageAccountName;
            string targetStorageAccountKey = data.targetStorageAccountKey;
            string targetContainer = data.targetContainer;
            string startsWith = data.startsWith;
            string endsWith = data.endsWith;


            log.Info("Input - targetStorageAccountName : " + targetStorageAccountName);
            log.Info("Input - targetStorageAccountKey : " + targetStorageAccountKey);
            log.Info("Input - targetContainer : " + targetContainer);
            string assetId = data.assetId;

            IAsset asset = null;
            IIngestManifest manifest = null;

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            log.Info($"Using Azure Media Service Rest API Endpoint : {amsCredentials.AmsRestApiEndpoint}");

            try
            {
                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                                                new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                                                AzureEnvironments.AzureCloudEnvironment);

                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);


                // Find the Asset
                asset = _context.Assets.Where(a => a.Id == assetId).FirstOrDefault();
                if (asset == null)
                    return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });


                string storname = amsCredentials.StorageAccountName;
                string storkey = amsCredentials.StorageAccountKey;
                if (asset.StorageAccountName != amsCredentials.StorageAccountName)
                {
                    if (attachedstoragecred.ContainsKey(asset.StorageAccountName)) // asset is using another storage than default but we have the key
                    {
                        storname = asset.StorageAccountName;
                        storkey = attachedstoragecred[storname];
                    }
                    else // we don't have the key for that storage
                    {
                        log.Info($"Face redaction Asset is in {asset.StorageAccountName} and key is not provided in MediaServicesAttachedStorageCredentials application settings");
                        return req.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            error = "Storage key is missing"
                        });
                    }
                }


                // Setup blob container
                CloudBlobContainer sourceBlobContainer = CopyBlobHelpers.GetCloudBlobContainer(storname, storkey, asset.Uri.Segments[1]);
                CloudBlobContainer destinationBlobContainer = CopyBlobHelpers.GetCloudBlobContainer(targetStorageAccountName, targetStorageAccountKey, targetContainer);
                destinationBlobContainer.CreateIfNotExists();

                var files = asset.AssetFiles.ToList().Where(f => ((string.IsNullOrEmpty(endsWith) || f.Name.EndsWith(endsWith)) && (string.IsNullOrEmpty(startsWith) || f.Name.StartsWith(startsWith))));

                string path = data.targetPath != null ? data.targetPath : string.Empty; // if user wants to use folder(s) for the output blob(s)

                foreach (var file in files)
                {
                    CloudBlob sourceBlob = sourceBlobContainer.GetBlockBlobReference(file.Name);
                    CloudBlob destinationBlob = destinationBlobContainer.GetBlockBlobReference(path + file.Name);
                    CopyBlobHelpers.CopyBlobAsync(sourceBlob, destinationBlob);
                    log.Info($"Start copy of file : {file.Name}");
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                log.Info($"ERROR: Exception {message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
            }

            return req.CreateResponse(HttpStatusCode.OK);
        } } }
