/*
This function returns media analytics from an asset.

Input:
{
    "assetFaceRedactionId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Id of the source asset that contains media analytics (face redaction)
    "assetMotionDetectionId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b",  // Id of the source asset that contains media analytics (motion detection)
    "assetOcrId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b",  // Id of the source asset that contains media analytics (OCR)
    "assetVideoAnnotationId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b",  // Id of the source asset that contains media analytics (video annotation)
    "assetMesThumbnailsId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b",  // Id of the source asset that contains the mes thumbnails
    "timeOffset" :"00:01:00", // optional, offset to add to subtitles (used for live analytics)
    "copyToContainer" : "jpgfaces" // Optional, to copy jpg files to a specific container in the same storage account. Use lowercases as this is the container name and there are restrictions. Used as a prefix, as date is added at the end (yyyyMMdd)
    "copyToContainerThumbnail" : "thumbnails" // Optional, to copy png files to a specific container in the same storage account. Use lowercases as this is the container name and there are restrictions. Used as a prefix, as date is added at the end (yyyyMMdd)
    "copyToContainerAccountName" : "jhggjgghggkj" // storage account name. optional. if not provided, ams storage account is used
    "copyToContainerAccountKey" "" // storage account key
    "deleteAsset" : true // Optional, delete the asset(s) once data has been read from it
 }

Output:
{
    "faceRedaction" :
        {
        "json" : "",      // the json of the face redaction
        "jsonOffset" : "",      // the json of the face redaction with offset
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
        "json" : "",      // the json of the face redaction
        "jsonOffset" : ""      // the json of the face redaction with offset
        },
    "ocr":
        {
        "json" : "",      // the json of the Ocr
        "jsonOffset" : ""      // the json of Ocr with offset
        },
    "videoAnnotation":
        {
        "json" : "",      // the json of the Video Annotator
        "jsonOffset" : ""      // the json of Video Annotator with offset
        }
 }
*/

#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "System.Xml.Linq"
#load "../Shared/mediaServicesHelpers.csx"
#load "../Shared/copyBlobHelpers.csx"

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

// Field for service context.
private static CloudMediaContext _context = null;
private static CloudStorageAccount _destinationStorageAccount = null;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
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

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);

    log.Info(jsonContent);

    if (data.assetFaceRedactionId == null && data.assetMotionDetectionId == null && data.assetOcrId == null && data.assetVideoAnnotationId == null)
    {
        // for test
        // data.assetId = "nb:cid:UUID:d9496372-32f5-430d-a4c6-d21ec3e01525";

        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass asset ID in the input object (assetFaceRedactionId and/or assetMotionDetectionId and/or assetOcrId and/or assetVideoAnnotationId)"
        });
    }

    log.Info($"Using Azure Media Service Rest API Endpoint : {_RESTAPIEndpoint}");

    try
    {
        AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(_AADTenantDomain,
                            new AzureAdClientSymmetricKey(_mediaservicesClientId, _mediaservicesClientSecret),
                            AzureEnvironments.AzureCloudEnvironment);

        AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

        _context = new CloudMediaContext(new Uri(_RESTAPIEndpoint), tokenProvider);


        //
        // FACE REDACTION
        //
        if (data.assetFaceRedactionId != null)
        {
            // Get the asset
            string assetid = data.assetFaceRedactionId;
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
            if (data.copyToContainer != null)
            {
                copyToContainer = data.copyToContainer + DateTime.UtcNow.ToString("yyyyMMdd");
                // let's copy JPG to a container
                prefixjpg = outputAsset.Uri.Segments[1] + "-";
                log.Info($"prefixjpg {prefixjpg}");
                var sourceContainer = GetCloudBlobContainer(_storageAccountName, _storageAccountKey, outputAsset.Uri.Segments[1]);

                CloudBlobContainer targetContainer;
                if (data.copyToContainerAccountName != null)
                {
                    // copy to a specific storage account
                    targetContainer = GetCloudBlobContainer((string)data.copyToContainerAccountName, (string)data.copyToContainerAccountKey, copyToContainer);
                }
                else
                {
                    // copy to ams storage account
                    targetContainer = GetCloudBlobContainer(_storageAccountName, _storageAccountKey, copyToContainer);
                }

                CopyFilesAsync(sourceContainer, targetContainer, prefixjpg, "jpg", log);
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

                if (data.timeOffset != null) // let's update the json with new timecode
                {
                    var tsoffset = TimeSpan.Parse((string)data.timeOffset);
                    foreach (var frag in objFaceDetectionOffset.fragments)
                    {
                        frag.start = ((long)(frag.start / objFaceDetectionOffset.timescale) * 10000000) + tsoffset.Ticks;
                    }
                }
            }

            if (jsonFaceRedaction != "" && data.deleteAsset != null && ((bool)data.deleteAsset))
            // If asset deletion was asked
            {
                outputAsset.Delete();
            }
        }

        //
        // MES Thumbnails
        //
        if (data.assetMesThumbnailsId != null)
        {
            // Get the asset
            string assetid = data.assetMesThumbnailsId;
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
            if (data.copyToContainerThumbnail != null)
            {
                copyToContainer = data.copyToContainerThumbnail + DateTime.UtcNow.ToString("yyyyMMdd");
                // let's copy PNG to a container
                prefixpng = outputAsset.Uri.Segments[1] + "-";
                log.Info($"prefixpng {prefixpng}");
                var sourceContainer = GetCloudBlobContainer(_storageAccountName, _storageAccountKey, outputAsset.Uri.Segments[1]);

                CloudBlobContainer targetContainer;
                if (data.copyToContainerAccountName != null)
                {
                    // copy to a specific storage account
                    targetContainer = GetCloudBlobContainer((string)data.copyToContainerAccountName, (string)data.copyToContainerAccountKey, copyToContainer);
                }
                else
                {
                    // copy to ams storage account
                    targetContainer = GetCloudBlobContainer(_storageAccountName, _storageAccountKey, copyToContainer);
                }

                CopyFilesAsync(sourceContainer, targetContainer, prefixpng, "png", log);
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

            if (data.deleteAsset != null && ((bool)data.deleteAsset))
            // If asset deletion was asked
            {
                outputAsset.Delete();
            }
        }

        //
        // MOTION DETECTION
        //
        if (data.assetMotionDetectionId != null)
        {
            // Get the asset
            string assetid = data.assetMotionDetectionId;
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

                if (data.timeOffset != null) // let's update the json with new timecode
                {
                    var tsoffset2 = TimeSpan.Parse((string)data.timeOffset);
                    foreach (var frag in objMotionDetectionOffset.fragments)
                    {
                        frag.start = ((long)(frag.start / objMotionDetectionOffset.timescale) * 10000000) + tsoffset2.Ticks;
                    }
                }
            }

            if (jsonMotionDetection != "" && data.deleteAsset != null && ((bool)data.deleteAsset))
            // If asset deletion was asked
            {
                outputAsset.Delete();
            }
        }


        //
        // OCR
        //
        if (data.assetOcrId != null)
        {
            // Get the asset
            string assetid = data.assetOcrId;
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

                if (data.timeOffset != null) // let's update the json with new timecode
                {
                    var tsoffset = TimeSpan.Parse((string)data.timeOffset);
                    foreach (var frag in objOcrOffset.fragments)
                    {
                        frag.start = ((long)(frag.start / objOcrOffset.timescale) * 10000000) + tsoffset.Ticks;
                    }
                }
            }

            if (jsonOcr != "" && data.deleteAsset != null && ((bool)data.deleteAsset))
            // If asset deletion was asked
            {
                outputAsset.Delete();
            }
        }

        //
        // Video Annotator
        //
        if (data.assetVideoAnnotationId != null)
        {
            // Get the asset
            string assetid = data.assetVideoAnnotationId;
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

                if (data.timeOffset != null) // let's update the json with new timecode
                {
                    var tsoffset = TimeSpan.Parse((string)data.timeOffset);
                    foreach (var frag in objAnnotationOffset.fragments)
                    {
                        frag.start = ((long)(frag.start / objAnnotationOffset.timescale) * 10000000) + tsoffset.Ticks;
                    }
                }
            }

            if (jsonAnnotation != "" && data.deleteAsset != null && ((bool)data.deleteAsset))
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

static public void CopyFilesAsync(CloudBlobContainer sourceBlobContainer, CloudBlobContainer destinationBlobContainer, string prefix, string extension, TraceWriter log)
{
    if (destinationBlobContainer.CreateIfNotExists())
    {
        destinationBlobContainer.SetPermissions(new BlobContainerPermissions
        {
            PublicAccess = BlobContainerPublicAccessType.Container // read-only access to container
        });
    }

    string blobPrefix = null;
    bool useFlatBlobListing = true;
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
                CopyBlobAsync(sourceBlob as CloudBlob, destinationBlob);
            }
        }
    }
}