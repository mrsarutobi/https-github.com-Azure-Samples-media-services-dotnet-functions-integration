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
