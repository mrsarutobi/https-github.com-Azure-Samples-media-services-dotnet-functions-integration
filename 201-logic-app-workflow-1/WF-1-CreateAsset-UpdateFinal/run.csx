#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"

#load "../Shared/mediaServicesHelpers.csx"
#load "../Shared/copyBlobHelpers.csx"
#load "../Shared/ingestAssetConfigHelpers.csx"

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
    if (data.AssetId == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass AssetId in the input object" });
    if (data.IngestAssetConfigJson == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass IngestAssetConfigJson in the input object" });
    log.Info("Input - Asset Id : " + data.AssetId);
    log.Info("Input - IngestAssetConfigJson : " + data.IngestAssetConfigJson);

    string assetid = data.AssetId;
    string ingestAssetConfigJson = data.IngestAssetConfigJson;
    IngestAssetConfig config = ParseIngestAssetConfig(ingestAssetConfigJson);
    if (!ValidateIngestAssetConfig(config))
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass a valid IngestAssetConfig as FileContent" });
    log.Info("Input - Valid IngestAssetConfig was loaded.");

    // Media Process is required: Encoding, Indexing, etc.
    int mediaProcessRequired = 0;
    try
    {
        // Load AMS account context
        log.Info($"Using Azure Media Service Rest API Endpoint : {_RESTAPIEndpoint}");

        AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(_AADTenantDomain,
                                  new AzureAdClientSymmetricKey(_mediaservicesClientId, _mediaservicesClientSecret),
                                  AzureEnvironments.AzureCloudEnvironment);

        AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

        _context = new CloudMediaContext(new Uri(_RESTAPIEndpoint), tokenProvider);

        // Get the Asset
        var asset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();
        if (asset == null)
        {
            log.Info("Asset not found - " + assetid);
            return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });
        }
        log.Info("Asset found, Asset ID : " + asset.Id);

        // Add AssetFiles to the Asset
        CloudBlobContainer destinationBlobContainer = GetCloudBlobContainer(_storageAccountName, _storageAccountKey, asset.Uri.Segments[1]);
        foreach (AssetFile srcAssetFile in config.IngestAsset.AssetFiles)
        {
            IAssetFile assetFile = asset.AssetFiles.Create(srcAssetFile.FileName);
            CloudBlockBlob blob = destinationBlobContainer.GetBlockBlobReference(srcAssetFile.FileName);
            blob.FetchAttributes();
            assetFile.ContentFileSize = blob.Properties.Length;
            assetFile.IsPrimary = srcAssetFile.IsPrimary;
            assetFile.Update();
            log.Info("Asset file updated : " + assetFile.Name);
        }

        if (config.IngestAssetEncoding.Encoding)
        {
            mediaProcessRequired = 1;
        }
    }
    catch (Exception ex)
    {
        log.Info("Exception " + ex);
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    return req.CreateResponse(HttpStatusCode.OK, new
    {
        MediaProcessRequired = mediaProcessRequired
    });
}
