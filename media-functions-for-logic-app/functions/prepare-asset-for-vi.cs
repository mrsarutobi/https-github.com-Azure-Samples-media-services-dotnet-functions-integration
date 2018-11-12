/*

Azure Media Services REST API v2 Function
 
This function prepares the asset for indexing with Video Indexer v2. It delete all the non mp4 files from the asset & blob container

Input:
{
    "assetId" : "the Id of the asset"
}
Output:
{
    "mp4FileName":"" // name of the mp4 file
}

*/


using System;
using System.Net;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Blob;

namespace media_functions_for_logic_app
{
    public static class prepare_asset_for_vi
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("prepare-asset-for-vi")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)


        {
            log.Info($"Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);
            log.Info("Request : " + jsonContent);

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            log.Info($"Using Azure Media Service Rest API Endpoint : {amsCredentials.AmsRestApiEndpoint}");

            // Validate input objects
            if (data.assetId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetId in the input object" });

            string assetId = data.assetId;

            var attachedstoragecred = KeyHelper.ReturnStorageCredentials();
            string mp4FileName = "";

            IAsset asset = null;
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

                var files = asset.AssetFiles.ToList().Where(f => !f.Name.EndsWith(".mp4"));

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

                foreach (var file in asset.AssetFiles.ToList())
                {
                    if (file.Name.EndsWith(".mp4"))
                    {
                        file.IsPrimary = true;
                        file.Update();
                        mp4FileName = file.Name;
                    }
                    else
                    {

                        file.Delete();
                        CloudBlob sourceBlob = sourceBlobContainer.GetBlockBlobReference(file.Name);
                        sourceBlob.DeleteIfExists();
                        log.Info($"Start copy of file : {file.Name}");
                    }
                }
                asset.Update();
                MediaServicesHelper.SetAFileAsPrimary(asset);

            }
            catch (Exception ex)
            {
                string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                log.Info($"ERROR: Exception {message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                mp4FileName
            });
        }
    }
}
