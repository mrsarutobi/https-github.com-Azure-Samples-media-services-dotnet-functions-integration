/*

Azure Media Services REST API v2 Function
 
This function returns media analytics from an asset.

Input:
{
    "mesThumbnails" : 
    {
        "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Id of the source asset 
        "deleteAsset" : true, // Optional, delete the asset once data has been read from it
        "copyToContainer" : "thumbnails" // Optional, to copy the thumbnails (png files) to a specific container in the same storage account. Use lowercases as this is the container name and there are restrictions. Used as a prefix, as date is added at the end (yyyyMMdd)
        "copyToContainerAccountName" : "jhggjgghggkj" // storage account name. optional. if not provided, ams storage account is used
        "copyToContainerAccountKey" "" // storage account key
     }
 }

Output:
{
 
    "mesThumbnail":
        {
        "pngThumbnails" : "",      // the serialized list of thumbnails
        }
   
 }
*/


using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.WebJobs.Host;

namespace media_functions_for_logic_app
{
    public static class return_analytics
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("return-analytics")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            {
                log.Info($"Webhook was triggered!");

                // Init variables
                string pathUrl = "";
                dynamic pngThumbnails = new JArray() as dynamic;
                string prefixpng = "";
                string copyToContainer = "";
                string targetContainerUri = "";

                TimeSpan timeOffset = new TimeSpan(0);

                string jsonContent = await req.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(jsonContent);

                var attachedstoragecred = KeyHelper.ReturnStorageCredentials();

                log.Info(jsonContent);

                MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
                log.Info($"Using Azure Media Service Rest API Endpoint : {amsCredentials.AmsRestApiEndpoint}");

                try
                {
                    AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                                                    new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                                                    AzureEnvironments.AzureCloudEnvironment);

                    AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

                    _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);


                    //
                    // MES Thumbnails
                    //
                    if (data.mesThumbnails != null && data.mesThumbnails.assetId != null)
                    {
                        List<CloudBlob> listPNGCopies = new List<CloudBlob>();

                        // Get the asset
                        string assetid = data.mesThumbnails.assetId;
                        var outputAsset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

                        if (outputAsset == null)
                        {
                            log.Info($"Asset not found {assetid}");
                            return req.CreateResponse(HttpStatusCode.BadRequest, new
                            {
                                error = "Asset not found"
                            });
                        }

                        var pngFiles = outputAsset.AssetFiles.Where(a => a.Name.ToUpper().EndsWith(".PNG"));

                        Uri publishurl = MediaServicesHelper.GetValidOnDemandPath(_context, outputAsset);
                        if (publishurl != null)
                        {
                            pathUrl = publishurl.ToString();
                        }
                        else
                        {
                            log.Info($"Asset not published");
                        }

                        // Let's copy the PNG Thumbnails
                        if (data.mesThumbnails.copyToContainer != null)
                        {
                            copyToContainer = data.mesThumbnails.copyToContainer + DateTime.UtcNow.ToString("yyyyMMdd");
                            // let's copy PNG to a container
                            prefixpng = outputAsset.Uri.Segments[1] + "-";
                            log.Info($"prefixpng {prefixpng}");

                            string storname = amsCredentials.StorageAccountName;
                            string storkey = amsCredentials.StorageAccountKey;
                            if (outputAsset.StorageAccountName != amsCredentials.StorageAccountName)
                            {
                                if (attachedstoragecred.ContainsKey(outputAsset.StorageAccountName)) // asset is using another storage than default but we have the key
                                {
                                    storname = outputAsset.StorageAccountName;
                                    storkey = attachedstoragecred[storname];
                                }
                                else // we don't have the key for that storage
                                {
                                    log.Info($"MES Thumbnails Asset is in {outputAsset.StorageAccountName} and key is not provided in MediaServicesAttachedStorageCredentials application settings");
                                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                                    {
                                        error = "Storage key is missing"
                                    });
                                }
                            }

                            var sourceContainer = CopyBlobHelpers.GetCloudBlobContainer(storname, storkey, outputAsset.Uri.Segments[1]);

                            CloudBlobContainer targetContainer;
                            if (data.mesThumbnails.copyToContainerAccountName != null)
                            {
                                // copy to a specific storage account
                                targetContainer = CopyBlobHelpers.GetCloudBlobContainer((string)data.mesThumbnails.copyToContainerAccountName, (string)data.mesThumbnails.copyToContainerAccountKey, copyToContainer);
                            }
                            else
                            {
                                // copy to ams storage account
                                targetContainer = CopyBlobHelpers.GetCloudBlobContainer(amsCredentials.StorageAccountName, amsCredentials.StorageAccountKey, copyToContainer);
                            }

                            listPNGCopies = await CopyBlobHelpers.CopyFilesAsync(sourceContainer, targetContainer, prefixpng, "png", log);
                            targetContainerUri = targetContainer.Uri.ToString();
                        }

                        foreach (IAssetFile file in pngFiles)
                        {
                            string index = file.Name.Substring(file.Name.Length - 10, 6);
                            int index_i = 0;
                            if (int.TryParse(index, out index_i))
                            {
                                dynamic entry = new JObject();
                                entry.id = index_i;
                                entry.fileId = file.Id;
                                entry.fileName = file.Name;
                                if (copyToContainer != "")
                                {
                                    entry.url = targetContainerUri + "/" + prefixpng + file.Name;
                                }
                                else if (!string.IsNullOrEmpty(pathUrl))
                                {
                                    entry.url = pathUrl + file.Name;
                                }
                                pngThumbnails.Add(entry);
                            }
                        }

                        if (data.mesThumbnails.deleteAsset != null && ((bool)data.mesThumbnails.deleteAsset))
                        {
                            // If asset deletion was asked
                            // let's wait for the copy to finish before deleting the asset..
                            if (listPNGCopies.Count > 0)
                            {
                                log.Info("PNG Copy with asset deletion was asked. Checking copy status...");
                                bool continueLoop = true;
                                while (continueLoop)
                                {
                                    listPNGCopies = listPNGCopies.Where(r => r.CopyState.Status == CopyStatus.Pending).ToList();
                                    if (listPNGCopies.Count == 0)
                                    {
                                        continueLoop = false;
                                    }
                                    else
                                    {
                                        log.Info("PNG Copy not finished. Waiting 3s...");
                                        Task.Delay(TimeSpan.FromSeconds(3d)).Wait();
                                        listPNGCopies.ForEach(r => r.FetchAttributes());
                                    }
                                }
                            }
                            outputAsset.Delete();
                        }
                    }

                }
                catch (Exception ex)
                {
                    string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                    log.Info($"ERROR: Exception {message}");
                    return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
                }

                log.Info($"");
                return req.CreateResponse(HttpStatusCode.OK, new
                {
                    mesThumbnail = new
                    {
                        pngThumbnails = Newtonsoft.Json.JsonConvert.SerializeObject(pngThumbnails)
                    }
                });
            }
        }
    }
}

