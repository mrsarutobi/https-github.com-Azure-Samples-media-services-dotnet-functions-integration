/*
This function returns media analytics from an asset.

Input:
{
    "faceRedaction" : 
    {
        "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Optional, Id of the source asset that contains media analytics (face redaction)
        "deleteAsset" : true, // Optional, delete the asset once data has been read from it
        "copyToContainer" : "jpgfaces" // Optional, to copy the faces (jpg files) to a specific container in the same storage account. Use lowercases as this is the container name and there are restrictions. Used as a prefix, as date is added at the end (yyyyMMdd)
        "copyToContainerAccountName" : "jhggjgghggkj" // storage account name. optional. if not provided, ams storage account is used
        "copyToContainerAccountKey" "" // storage account key.
        },
   "motionDetection" : 
    {
        "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Optional, Id of the source asset that contains media analytics (motion detection)
        "deleteAsset" : true, // Optional, delete the asset once data has been read from it
    },
     "ocr" : 
    {
        "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Optional, Id of the source asset that contains media analytics (ocr)
        "deleteAsset" : true, // Optional, delete the asset once data has been read from it
    },
   "videoAnnotation" : 
    {
        "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Optional, Id of the source asset that contains the MES thumbnails
        "deleteAsset" : true, // Optional, delete the asset once data has been read from it
    },
   "mesThumbnails" : 
    {
        "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Optional, Id of the source asset that contains media analytics (face redaction)
        "deleteAsset" : true, // Optional, delete the asset once data has been read from it
        "copyToContainer" : "thumbnails" // Optional, to copy the thumbnails (png files) to a specific container in the same storage account. Use lowercases as this is the container name and there are restrictions. Used as a prefix, as date is added at the end (yyyyMMdd)
        "copyToContainerAccountName" : "jhggjgghggkj" // storage account name. optional. if not provided, ams storage account is used
        "copyToContainerAccountKey" "" // storage account key
     },

     "timeOffset" :"00:01:00", // optional, offset to add to data from face redaction, ocr, video annotation (used for live analytics)
 }

Output:
{
    "faceRedaction" :
        {
        "json" : "",      // the serialized json of the face redaction
        "jsonOffset" : "",      // the serialized json of the face redaction with offset
        "jpgFaces":[
                {
                    "id" :24,
                    "fileId": "nb:cid:UUID:a93464ae-cbd5-4e63-9459-a3e2cf869f0e",
                    "fileName": "ArchiveTopBitrate_video_800000_thumb000024.jpg",
                    "url" : "http://xpouyatdemo.streaming.mediaservices.windows.net/903f9261-d745-48aa-8dfe-ebcd6e6128d6/ArchiveTopBitrate_video_800000_thumb000024.jpg"
                }
                ]
        "pathUrl" : "",     // the path to the asset if asset is published
        },
        "pngThumbnails":[
                {
                    "id" :24,
                    "fileId": "nb:cid:UUID:a93464ae-cbd5-4e63-9459-a3e2cf869f0e",
                    "fileName": "ArchiveTopBitrate_video_800000_thumb000024.jpg",
                    "url" : "http://xpouyatdemo.streaming.mediaservices.windows.net/903f9261-d745-48aa-8dfe-ebcd6e6128d6/ArchiveTopBitrate_video_800000_thumb000024.jpg"
                }
                ]
        "pathUrl" : "",     // the path to the asset if asset is published
        },
    "motionDetection":
        {
        "json" : "",      // the serialized json of the face redaction
        "jsonOffset" : ""      // the serialized json of the face redaction with offset
        },
    "ocr":
        {
        "json" : "",      // the serialized json of the Ocr
        "jsonOffset" : ""      // the serialized json of Ocr with offset
        },
    "videoAnnotation":
        {
        "json" : "",      // the serialized json of the Video Annotator
        "jsonOffset" : ""      // the serialized json of Video Annotator with offset
        }
 }
*/

#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "System.Xml.Linq"
#load "../Shared/mediaServicesHelpers.csx"
#load "../Shared/copyBlobHelpers.csx"
#load "../Shared/keyHelpers.csx"

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
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

// Read values from the App.config file.
static string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
static string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");

static readonly string _AADTenantDomain = Environment.GetEnvironmentVariable("AMSAADTenantDomain");
static readonly string _RESTAPIEndpoint = Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint");

static readonly string _mediaservicesClientId = Environment.GetEnvironmentVariable("AMSClientId");
static readonly string _mediaservicesClientSecret = Environment.GetEnvironmentVariable("AMSClientSecret");

static readonly string _attachedStorageCredentials = Environment.GetEnvironmentVariable("MediaServicesAttachedStorageCredentials");

// Field for service context.
private static CloudMediaContext _context = null;
private static CloudStorageAccount _destinationStorageAccount = null;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log, Microsoft.Azure.WebJobs.ExecutionContext execContext)
{
    log.Info($"Webhook was triggered!");

    // Init variables
    string pathUrl = "";
    string jsonFaceRedaction = "";
    dynamic jpgFaces = new JArray() as dynamic;
    dynamic objFaceDetection = new JObject();
    dynamic objFaceDetectionOffset = new JObject();

    dynamic pngThumbnails = new JArray() as dynamic;
    string prefixpng = "";

    string jsonMotionDetection = "";
    dynamic objMotionDetection = new JObject();
    dynamic objMotionDetectionOffset = new JObject();

    string jsonOcr = "";
    dynamic objOcr = new JObject();
    dynamic objOcrOffset = new JObject();

    string jsonAnnotation = "";
    dynamic objAnnotation = new JObject();
    dynamic objAnnotationOffset = new JObject();

    string copyToContainer = "";
    string prefixjpg = "";
    string targetContainerUri = "";

    TimeSpan timeOffset = new TimeSpan(0);

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);

    log.Info(_attachedStorageCredentials);
    var attachedstoragecred = ReturnStorageCredentials();

    log.Info(jsonContent);

    log.Info($"Using Azure Media Service Rest API Endpoint : {_RESTAPIEndpoint}");

    try
    {
        AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(_AADTenantDomain,
                            new AzureAdClientSymmetricKey(_mediaservicesClientId, _mediaservicesClientSecret),
                            AzureEnvironments.AzureCloudEnvironment);

        AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

        _context = new CloudMediaContext(new Uri(_RESTAPIEndpoint), tokenProvider);


        // Offset value ?
        if (data.timeOffset != null) // let's store the offset
        {
            timeOffset = TimeSpan.Parse((string)data.timeOffset);
        }

        //
        // FACE REDACTION
        //
        if (data.faceRedaction != null && data.faceRedaction.assetId != null)
        {
            List<CloudBlob> listJPGCopies = new List<CloudBlob>();

            // Get the asset
            string assetid = data.faceRedaction.assetId;
            var outputAsset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

            if (outputAsset == null)
            {
                log.Info($"Asset not found {assetid}");
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Asset not found"
                });
            }

            var jsonFile = outputAsset.AssetFiles.Where(a => a.Name.ToUpper().EndsWith(".JSON")).FirstOrDefault();
            var jpgFiles = outputAsset.AssetFiles.Where(a => a.Name.ToUpper().EndsWith(".JPG"));

            Uri publishurl = GetValidOnDemandPath(outputAsset);
            if (publishurl != null)
            {
                pathUrl = publishurl.ToString();
            }
            else
            {
                log.Info($"Asset not published");
            }

            // Let's copy the JPG faces
            if (data.faceRedaction.copyToContainer != null)
            {
                copyToContainer = data.faceRedaction.copyToContainer + DateTime.UtcNow.ToString("yyyyMMdd");
                // let's copy JPG to a container
                prefixjpg = outputAsset.Uri.Segments[1] + "-";
                log.Info($"prefixjpg {prefixjpg}");

                string storname = _storageAccountName;
                string storkey = _storageAccountKey;
                if (outputAsset.StorageAccountName != _storageAccountName)
                {
                    if (attachedstoragecred.ContainsKey(outputAsset.StorageAccountName)) // asset is using another storage than default but we have the key
                    {
                        storname = outputAsset.StorageAccountName;
                        storkey = attachedstoragecred[storname];
                    }
                    else // we don't have the key for that storage
                    {
                        log.Info($"Face redaction Asset is in {outputAsset.StorageAccountName} and key is not provided in MediaServicesAttachedStorageCredentials application settings");
                        return req.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            error = "Storage key is missing"
                        });
                    }
                }

                var sourceContainer = GetCloudBlobContainer(storname, storkey, outputAsset.Uri.Segments[1]);

                CloudBlobContainer targetContainer;
                if (data.faceRedaction.copyToContainerAccountName != null)
                {
                    // copy to a specific storage account
                    targetContainer = GetCloudBlobContainer((string)data.faceRedaction.copyToContainerAccountName, (string)data.faceRedaction.copyToContainerAccountKey, copyToContainer);
                }
                else
                {
                    // copy to ams storage account
                    targetContainer = GetCloudBlobContainer(_storageAccountName, _storageAccountKey, copyToContainer);
                }

                listJPGCopies = await CopyFilesAsync(sourceContainer, targetContainer, prefixjpg, "jpg", log);
                targetContainerUri = targetContainer.Uri.ToString();
            }

            foreach (IAssetFile file in jpgFiles)
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
                        entry.url = targetContainerUri + "/" + prefixjpg + file.Name;
                    }
                    else if (!string.IsNullOrEmpty(pathUrl))
                    {
                        entry.url = pathUrl + file.Name;
                    }
                    jpgFaces.Add(entry);
                }
            }

            if (jsonFile != null)
            {
                jsonFaceRedaction = ReturnContent(jsonFile);
                objFaceDetection = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonFaceRedaction);
                objFaceDetectionOffset = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonFaceRedaction);

                if (timeOffset.Ticks != 0) // Let's add the offset
                {
                    foreach (var frag in objFaceDetectionOffset.fragments)
                    {
                        frag.start = ((long)(frag.start)) + (long)((((double)timeOffset.Ticks / (double)TimeSpan.TicksPerSecond) * (double)objFaceDetectionOffset.timescale));
                    }
                }
            }

            if (jsonFaceRedaction != "" && data.faceRedaction.deleteAsset != null && ((bool)data.faceRedaction.deleteAsset))
            // If asset deletion was asked
            {

                // let's wait for the copy to finish before deleting the asset..
                if (listJPGCopies.Count > 0)
                {
                    log.Info("JPG Copy with asset deletion was asked. Checking copy status...");
                    bool continueLoop = true;
                    while (continueLoop)
                    {
                        listJPGCopies = listJPGCopies.Where(r => r.CopyState.Status == CopyStatus.Pending).ToList();
                        if (listJPGCopies.Count == 0)
                        {
                            continueLoop = false;
                        }
                        else
                        {
                            log.Info("JPG Copy not finished. Waiting 3s...");
                            Task.Delay(TimeSpan.FromSeconds(3d)).Wait();
                            listJPGCopies.ForEach(r => r.FetchAttributes());
                        }
                    }
                }
                outputAsset.Delete();
            }
        }

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

            Uri publishurl = GetValidOnDemandPath(outputAsset);
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

                string storname = _storageAccountName;
                string storkey = _storageAccountKey;
                if (outputAsset.StorageAccountName != _storageAccountName)
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

                var sourceContainer = GetCloudBlobContainer(storname, storkey, outputAsset.Uri.Segments[1]);

                CloudBlobContainer targetContainer;
                if (data.mesThumbnails.copyToContainerAccountName != null)
                {
                    // copy to a specific storage account
                    targetContainer = GetCloudBlobContainer((string)data.mesThumbnails.copyToContainerAccountName, (string)data.mesThumbnails.copyToContainerAccountKey, copyToContainer);
                }
                else
                {
                    // copy to ams storage account
                    targetContainer = GetCloudBlobContainer(_storageAccountName, _storageAccountKey, copyToContainer);
                }

                listPNGCopies = await CopyFilesAsync(sourceContainer, targetContainer, prefixpng, "png", log);
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

        //
        // MOTION DETECTION
        //
        if (data.motionDetection != null && data.motionDetection.assetId != null)
        {
            // Get the asset
            string assetid = data.motionDetection.assetId;
            var outputAsset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

            if (outputAsset == null)
            {
                log.Info($"Asset not found {assetid}");
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Asset not found"
                });
            }

            var jsonFile = outputAsset.AssetFiles.Where(a => a.Name.ToUpper().EndsWith(".JSON")).FirstOrDefault();

            if (jsonFile != null)
            {
                jsonMotionDetection = ReturnContent(jsonFile);
                objMotionDetection = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonMotionDetection);
                objMotionDetectionOffset = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonMotionDetection);

                if (timeOffset.Ticks != 0) // Let's add the offset
                {
                    foreach (var frag in objMotionDetectionOffset.fragments)
                    {
                        frag.start = ((long)(frag.start)) + (long)((((double)timeOffset.Ticks / (double)TimeSpan.TicksPerSecond) * (double)objMotionDetectionOffset.timescale));
                    }
                }
            }

            if (jsonMotionDetection != "" && data.motionDetection.deleteAsset != null && ((bool)data.motionDetection.deleteAsset))
            // If asset deletion was asked
            {
                outputAsset.Delete();
            }
        }


        //
        // OCR
        //
        if (data.ocr != null && data.ocr.assetId != null)
        {
            // Get the asset
            string assetid = data.ocr.assetId;
            var outputAsset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

            if (outputAsset == null)
            {
                log.Info($"Asset not found {assetid}");
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Asset not found"
                });
            }

            var jsonFile = outputAsset.AssetFiles.Where(a => a.Name.ToUpper().EndsWith(".JSON")).FirstOrDefault();

            if (jsonFile != null)
            {
                jsonOcr = ReturnContent(jsonFile);
                objOcr = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonOcr);
                objOcrOffset = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonOcr);

                if (timeOffset.Ticks != 0) // Let's add the offset
                {
                    foreach (var frag in objOcrOffset.fragments)
                    {
                        frag.start = ((long)(frag.start)) + (long)((((double)timeOffset.Ticks / (double)TimeSpan.TicksPerSecond) * (double)objOcrOffset.timescale));
                    }
                }
            }

            if (jsonOcr != "" && data.ocr.deleteAsset != null && ((bool)data.ocr.deleteAsset))
            // If asset deletion was asked
            {
                outputAsset.Delete();
            }
        }

        //
        // Video Annotator
        //
        if (data.videoAnnotation != null && data.videoAnnotation.assetId != null)
        {
            // Get the asset
            string assetid = data.videoAnnotation.assetId;
            var outputAsset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

            if (outputAsset == null)
            {
                log.Info($"Asset not found {assetid}");
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Asset not found"
                });
            }

            var jsonFile = outputAsset.AssetFiles.Where(a => a.Name.ToUpper().EndsWith(".JSON")).FirstOrDefault();

            if (jsonFile != null)
            {
                jsonAnnotation = ReturnContent(jsonFile);
                objAnnotation = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonAnnotation);
                objAnnotationOffset = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonAnnotation);

                if (timeOffset.Ticks != 0) // Let's add the offset
                {
                    foreach (var frag in objAnnotationOffset.fragments)
                    {
                        frag.start = ((long)(frag.start)) + (long)((((double)timeOffset.Ticks / (double)TimeSpan.TicksPerSecond) * (double)objAnnotationOffset.timescale));
                    }
                }
            }

            if (jsonAnnotation != "" && data.videoAnnotation.deleteAsset != null && ((bool)data.videoAnnotation.deleteAsset))
            // If asset deletion was asked
            {
                outputAsset.Delete();
            }
        }

    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.InternalServerError, new
        {
            Error = ex.ToString()
        });
    }

    log.Info($"");
    return req.CreateResponse(HttpStatusCode.OK, new
    {
        faceRedaction = new
        {
            json = Newtonsoft.Json.JsonConvert.SerializeObject(objFaceDetection),
            jsonOffset = Newtonsoft.Json.JsonConvert.SerializeObject(objFaceDetectionOffset),
            jpgFaces = Newtonsoft.Json.JsonConvert.SerializeObject(jpgFaces)
        },
        mesThumbnail = new
        {
            pngThumbnails = Newtonsoft.Json.JsonConvert.SerializeObject(pngThumbnails)
        },
        motionDetection = new
        {
            json = Newtonsoft.Json.JsonConvert.SerializeObject(objMotionDetection),
            jsonOffset = Newtonsoft.Json.JsonConvert.SerializeObject(objMotionDetectionOffset)
        },
        ocr = new
        {
            json = Newtonsoft.Json.JsonConvert.SerializeObject(objOcr),
            jsonOffset = Newtonsoft.Json.JsonConvert.SerializeObject(objOcrOffset)
        },
        videoAnnotation = new
        {
            json = Newtonsoft.Json.JsonConvert.SerializeObject(objAnnotation),
            jsonOffset = Newtonsoft.Json.JsonConvert.SerializeObject(objAnnotationOffset)
        }
    });
}

static public async Task<List<CloudBlob>> CopyFilesAsync(CloudBlobContainer sourceBlobContainer, CloudBlobContainer destinationBlobContainer, string prefix, string extension, TraceWriter log)
{
    // init the list of tasks
    List<CloudBlob> mylistresults = new List<CloudBlob>();

    if (destinationBlobContainer.CreateIfNotExists())
    {
        destinationBlobContainer.SetPermissions(new BlobContainerPermissions
        {
            PublicAccess = BlobContainerPublicAccessType.Container // read-only access to container
        });
    }

    string blobPrefix = null;
    bool useFlatBlobListing = true;
    long size = 0;

    var blobList = sourceBlobContainer.ListBlobs(blobPrefix, useFlatBlobListing, BlobListingDetails.None);
    foreach (var sourceBlob in blobList)
    {
        if ((sourceBlob as CloudBlob).Name.EndsWith("." + extension))
        {
            log.Info("Source blob : " + (sourceBlob as CloudBlob).Uri.ToString());
            CloudBlob destinationBlob = destinationBlobContainer.GetBlockBlobReference(prefix + (sourceBlob as CloudBlob).Name);
            if (destinationBlob.Exists())
            {
                log.Info("Destination blob already exists. Skipping: " + destinationBlob.Uri.ToString());
            }
            else
            {
                log.Info("Copying blob " + sourceBlob.Uri.ToString() + " to " + destinationBlob.Uri.ToString());
                size = (sourceBlob as CloudBlob).Properties.Length;
                log.Info("Source Blob size: " + size.ToString());
                //mylistresults.Add(CopyBlobAsync(sourceBlob as CloudBlob, destinationBlob));

                var signature = (sourceBlob as CloudBlob).GetSharedAccessSignature(new SharedAccessBlobPolicy
                {
                    Permissions = SharedAccessBlobPermissions.Read,
                    SharedAccessExpiryTime = DateTime.UtcNow.AddHours(24)
                });

                //mylistresults.Add(destinationBlob.StartCopyAsync(new Uri(sourceBlob.Uri.AbsoluteUri + signature)));
                await destinationBlob.StartCopyAsync(new Uri(sourceBlob.Uri.AbsoluteUri + signature));

                mylistresults.Add(destinationBlob);

            }
        }
    }

    return mylistresults;
}