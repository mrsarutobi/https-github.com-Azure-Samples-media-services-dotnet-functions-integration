/*
This function copy the asset files to a specific azure storage container.

Input:
{
    "assetId" : "the Id of the source asset",
    "targetStorageAccountName" : "",
    "targetStorageAccountKey": "",
    "targetContainer" : "",
    "startsWith" : "video", //optional, copy only files that start with name video
    "endsWith" : ".mp4", //optional, copy only files that end with .mp4
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
#load "../Shared/keyHelpers.csx"

using System;
using System.Net;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
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

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);
    log.Info("Request : " + jsonContent);

    log.Info(_attachedStorageCredentials);
    var attachedstoragecred = ReturnStorageCredentials();

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
    try
    {
        // Load AMS account context
        log.Info($"Using Azure Media Service Rest API Endpoint : {_RESTAPIEndpoint}");

        AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(_AADTenantDomain,
                                  new AzureAdClientSymmetricKey(_mediaservicesClientId, _mediaservicesClientSecret),
                                  AzureEnvironments.AzureCloudEnvironment);

        AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

        _context = new CloudMediaContext(new Uri(_RESTAPIEndpoint), tokenProvider);

        // Find the Asset
        asset = _context.Assets.Where(a => a.Id == assetId).FirstOrDefault();
        if (asset == null)
            return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });


        string storname = _storageAccountName;
        string storkey = _storageAccountKey;
        if (asset.StorageAccountName != _storageAccountName)
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
        CloudBlobContainer sourceBlobContainer = GetCloudBlobContainer(storname, storkey, asset.Uri.Segments[1]);
        CloudBlobContainer destinationBlobContainer = GetCloudBlobContainer(targetStorageAccountName, targetStorageAccountKey, targetContainer);
        destinationBlobContainer.CreateIfNotExists();

        var files = asset.AssetFiles.ToList().Where(f => ((string.IsNullOrEmpty(endsWith) || f.Name.EndsWith(endsWith)) && (string.IsNullOrEmpty(startsWith) || f.Name.StartsWith(startsWith))));

        foreach (var file in files)
        {
            CloudBlob sourceBlob = sourceBlobContainer.GetBlockBlobReference(file.Name);
            CloudBlob destinationBlob = destinationBlobContainer.GetBlockBlobReference(file.Name);
            CopyBlobAsync(sourceBlob, destinationBlob);
            log.Info($"Start copy of file : {file.Name}");
        }
    }
    catch (Exception ex)
    {
        log.Info("Exception " + ex);
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    return req.CreateResponse(HttpStatusCode.OK);
}
