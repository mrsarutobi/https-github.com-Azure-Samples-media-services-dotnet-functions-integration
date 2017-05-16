/*
This function copy the asset files to a specific azure storage container.

Input:
{
    "assetId" : "the Id of the source asset",
    "targetStorageAccountName" : "",
    "targetStorageAccountKey": "",
    "targetContainer" : ""
}
Output:
{
}

*/

#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"

#load "../Shared/copyBlobHelpers.csx"
#load "../Shared/ingestAssetConfigHelpers.csx"
#load "../Shared/mediaServicesHelpers.csx"

using System;
using System.Net;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;

// Read values from the App.config file.
private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");
private static readonly string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
private static readonly string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");

// Field for service context.
private static CloudMediaContext _context = null;
private static CloudStorageAccount _destinationStorageAccount = null;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);
    log.Info("Request : " + jsonContent);

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

    log.Info("Input - targetStorageAccountName : " + targetStorageAccountName);
    log.Info("Input - targetStorageAccountKey : " + targetStorageAccountKey);
    log.Info("Input - targetContainer : " + targetContainer);
    string assetId = data.assetId;

    IAsset asset = null;
    IIngestManifest manifest = null;
    try
    {
        // Load AMS account context
        log.Info("Using Azure Media Services account : " + _mediaServicesAccountName);
        _context = new CloudMediaContext(new MediaServicesCredentials(_mediaServicesAccountName, _mediaServicesAccountKey));

        // Find the Asset
        asset = _context.Assets.Where(a => a.Id == assetId).FirstOrDefault();
        if (asset == null)
            return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });

        // Setup blob container
        CloudBlobContainer sourceBlobContainer = GetCloudBlobContainer(_storageAccountName, _storageAccountKey, asset.Uri.Segments[1]);
        CloudBlobContainer destinationBlobContainer = GetCloudBlobContainer(targetStorageAccountName, targetStorageAccountKey, targetContainer);
        destinationBlobContainer.CreateIfNotExists();

        foreach (var file in asset.AssetFiles)
        {
            CloudBlob sourceBlob = sourceBlobContainer.GetBlockBlobReference(file.Name);
            CloudBlob destinationBlob = destinationBlobContainer.GetBlockBlobReference(file.Name);
            CopyBlobAsync(sourceBlob, destinationBlob);
        }
    }
    catch (Exception ex)
    {
        log.Info("Exception " + ex);
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    return req.CreateResponse(HttpStatusCode.OK);
}
