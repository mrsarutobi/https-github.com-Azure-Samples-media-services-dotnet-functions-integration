/*
This function declares the asset files in the AMS asset based on the blobs in the asset container.

Input:
{
    "assetId" : "the Id of the asset"
}

Output:
{}

*/

using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Web;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.Azure.WebJobs;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Azure.WebJobs.Host;

namespace media_functions_for_logic_app
{
    public static class sync_asset
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("sync-asset")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            log.Info(jsonContent);

            var attachedstoragecred =  KeyHelper.ReturnStorageCredentials();

            if (data.assetId == null)
            {
                // for test
                // data.Path = "/input/WP_20121015_081924Z.mp4";

                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass assetId in the input object"
                });
            }

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            log.Info($"Using Azure Media Service Rest API Endpoint : {amsCredentials.AmsRestApiEndpoint}");

            try
            {
                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                                                new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                                                AzureEnvironments.AzureCloudEnvironment);

                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);



                // Step 1:  Copy the Blob into a new Input Asset for the Job
                // ***NOTE: Ideally we would have a method to ingest a Blob directly here somehow. 
                // using code from this sample - https://azure.microsoft.com/en-us/documentation/articles/media-services-copying-existing-blob/

                // Get the asset
                string assetid = data.assetId;
                var asset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

                if (asset == null)
                {
                    log.Info($"Asset not found {assetid}");

                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = "Asset not found"
                    });
                }

                log.Info("Asset found, ID: " + asset.Id);


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

                CloudBlobContainer assetContainer = CopyBlobHelpers.GetCloudBlobContainer(storname, storkey, asset.Uri.Segments[1]);

                /*
                //Get a reference to the storage account that is associated with the Media Services account. 
                StorageCredentials mediaServicesStorageCredentials =
                    new StorageCredentials(_storageAccountName, _storageAccountKey);
                var _destinationStorageAccount = new CloudStorageAccount(mediaServicesStorageCredentials, false);

                CloudBlobClient destBlobStorage = _destinationStorageAccount.CreateCloudBlobClient();

                // Get the destination asset container reference
                string destinationContainerName = asset.Uri.Segments[1];
                log.Info($"destinationContainerName : {destinationContainerName}");

                CloudBlobContainer assetContainer = destBlobStorage.GetContainerReference(destinationContainerName);
                */

                log.Info($"assetContainer retrieved");

                // Get hold of the destination blobs
                var blobs = assetContainer.ListBlobs();
                log.Info($"blobs retrieved");

                log.Info($"blobs count : {blobs.Count()}");

                var aflist = asset.AssetFiles.ToList().Select(af => af.Name);

                foreach (CloudBlockBlob blob in blobs)
                {
                    if (aflist.Contains(blob.Name))
                    {
                        var assetFile = asset.AssetFiles.Where(af => af.Name == blob.Name).FirstOrDefault();
                        assetFile.ContentFileSize = blob.Properties.Length;
                        assetFile.Update();
                        log.Info($"Asset file updated : {assetFile.Name}");
                    }
                    else
                    {
                        var assetFile = asset.AssetFiles.Create(blob.Name);
                        assetFile.ContentFileSize = blob.Properties.Length;
                        assetFile.Update();
                        log.Info($"Asset file created : {assetFile.Name}");
                    }
                }

                asset.Update();
                MediaServicesHelper.SetAFileAsPrimary(asset);

                log.Info("Asset updated");
            }
            catch (Exception ex)
            {
                log.Info($"Exception {ex}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Error = ex.ToString()
                });
            }

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
