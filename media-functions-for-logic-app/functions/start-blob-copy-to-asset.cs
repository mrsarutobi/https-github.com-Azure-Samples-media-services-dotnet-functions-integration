/*

Azure Media Services REST API v2 Function
 
This function copy a file (blob) or several blobs to a new asset previously created.

Input:
{
    "assetId" : "the Id of the asset where the file must be copied",
    "fileName" : "filename.mp4", // use fileName if one file, or FileNames if several files
    "fileNames" : [ "filename.mp4" , "filename2.mp4"],
    "sourceStorageAccountName" : "",
    "sourceStorageAccountKey": "",
    "sourceContainer" : "",
    "wait" : true // optional. Set this parameter if you want the function to wait up to 15s if the fileName blob is missing. Otherwise it does not wait. I applies to fileName, not for fileNames 
    "flattenPath" : true // optional. Set this parameter if you want the function to remove the path from the source blob when doing the copy, to avoid creating the folders in the target asset container
}

Output:
{
    "destinationContainer": "asset-2e26fd08-1436-44b1-8b92-882a757071dd" // container of asset
    "missingBob" : "True" // True if one of the source blob(s) is missing
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
using System.IO;

namespace media_functions_for_logic_app
{
    public static class start_blob_copy_to_asset
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("start-blob-copy-to-asset")]
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

            if (data.fileName == null && data.fileNames == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass fileName or fileNames in the input object" });
            if (data.sourceStorageAccountName == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass sourceStorageAccountName in the input object" });
            if (data.sourceStorageAccountKey == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass sourceStorageAccountKey in the input object" });
            if (data.sourceContainer == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass sourceContainer in the input object" });

            log.Info("Input - sourceStorageAccountName : " + data.sourceStorageAccountName);
            log.Info("Input - sourceStorageAccountKey : " + data.sourceStorageAccountKey);
            log.Info("Input - sourceContainer : " + data.sourceContainer);

            string _sourceStorageAccountName = data.sourceStorageAccountName;
            string _sourceStorageAccountKey = data.sourceStorageAccountKey;
            string assetId = data.assetId;
            bool missingBlob = false;

            IAsset newAsset = null;
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
                newAsset = _context.Assets.Where(a => a.Id == assetId).FirstOrDefault();
                if (newAsset == null)
                    return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });


                // Setup blob container
                CloudBlobContainer sourceBlobContainer = CopyBlobHelpers.GetCloudBlobContainer(_sourceStorageAccountName, _sourceStorageAccountKey, (string)data.sourceContainer);

                string storname = amsCredentials.StorageAccountName;
                string storkey = amsCredentials.StorageAccountKey;
                if (newAsset.StorageAccountName != amsCredentials.StorageAccountName)
                {
                    if (attachedstoragecred.ContainsKey(newAsset.StorageAccountName)) // asset is using another storage than default but we have the key
                    {
                        storname = newAsset.StorageAccountName;
                        storkey = attachedstoragecred[storname];
                    }
                    else // we don't have the key for that storage
                    {
                        log.Info($"Face redaction Asset is in {newAsset.StorageAccountName} and key is not provided in MediaServicesAttachedStorageCredentials application settings");
                        return req.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            error = "Storage key is missing"
                        });
                    }
                }

                CloudBlobContainer destinationBlobContainer = CopyBlobHelpers.GetCloudBlobContainer(storname, storkey, newAsset.Uri.Segments[1]);

                sourceBlobContainer.CreateIfNotExists();

                if (data.fileName != null)
                {
                    string fileName = (string)data.fileName;

                    CloudBlob sourceBlob = sourceBlobContainer.GetBlockBlobReference(fileName);

                    if (data.wait != null && (bool)data.wait)
                    {
                        for (int i = 1; i <= 3; i++) // let's wait 3 times 5 seconds (15 seconds)
                        {
                            if (sourceBlob.Exists())
                            {
                                break;
                            }

                            log.Info("Waiting 5 s...");
                            System.Threading.Thread.Sleep(5 * 1000);
                            sourceBlob = sourceBlobContainer.GetBlockBlobReference(fileName);
                        }
                    }

                    if (sourceBlob.Exists())
                    {
                        if (data.flattenPath != null && (bool)data.flattenPath)
                        {
                            fileName = Path.GetFileName(fileName);
                        }
                        CloudBlob destinationBlob = destinationBlobContainer.GetBlockBlobReference(fileName);

                        if (destinationBlobContainer.CreateIfNotExists())
                        {
                            log.Info("container created");
                            destinationBlobContainer.SetPermissions(new BlobContainerPermissions
                            {
                                PublicAccess = BlobContainerPublicAccessType.Blob
                            });
                        }
                        CopyBlobHelpers.CopyBlobAsync(sourceBlob, destinationBlob);
                    }
                    else
                    {
                        missingBlob = true;
                    }
                }

                if (data.fileNames != null)
                {
                    foreach (var file in data.fileNames)
                    {
                        string fileName = (string)file;
                        CloudBlob sourceBlob = sourceBlobContainer.GetBlockBlobReference(fileName);
                        if (sourceBlob.Exists())
                        {
                            if (data.flattenPath != null && (bool)data.flattenPath)
                            {
                                fileName = Path.GetFileName(fileName);
                            }
                            CloudBlob destinationBlob = destinationBlobContainer.GetBlockBlobReference(fileName);

                            if (destinationBlobContainer.CreateIfNotExists())
                            {
                                log.Info("container created");
                                destinationBlobContainer.SetPermissions(new BlobContainerPermissions
                                {
                                    PublicAccess = BlobContainerPublicAccessType.Blob
                                });
                            }
                            CopyBlobHelpers.CopyBlobAsync(sourceBlob, destinationBlob);
                        }
                        else
                        {
                            missingBlob = true;
                        }
                    }
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
                destinationContainer = newAsset.Uri.Segments[1],
                missingBlob = missingBlob.ToString()
            });
        }
    }
}
