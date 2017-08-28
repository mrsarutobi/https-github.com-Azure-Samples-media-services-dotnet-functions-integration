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
    if (data.FileName == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass FileName in the input object" });
    if (data.FileContent == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass FileContent in the input object" });
    if (data.SourceStorageAccountName == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass SourceStorageAccountName in the input object" });
    if (data.SourceStorageAccountKey == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass SourceStorageAccountKey in the input object" });
    log.Info("Input - File Name : " + data.FileName);
    log.Info("Input - File Content : " + data.FileContent);
    log.Info("Input - SourceStorageAccountName : " + data.SourceStorageAccountName);
    log.Info("Input - SourceStorageAccountKey : " + data.SourceStorageAccountKey);
    string _sourceStorageAccountName = data.SourceStorageAccountName;
    string _sourceStorageAccountKey = data.SourceStorageAccountKey;

    // Validate IngestAssetConfig with FileContent
    string ingestAssetConfigJson = data.FileContent;
    IngestAssetConfig config = ParseIngestAssetConfig(ingestAssetConfigJson);
    if (!ValidateIngestAssetConfig(config))
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass a valid IngestAssetConfig as FileContent" });
    log.Info("Input - Valid IngestAssetConfig was loaded.");

    IAsset newAsset = null;
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

        // Create Asset
        newAsset = _context.Assets.Create(config.IngestAsset.AssetName, config.IngestAsset.CreationOption);
        log.Info("Created Azure Media Services Asset : ");
        log.Info("  - Asset Name = " + config.IngestAsset.AssetName);
        log.Info("  - Asset Creation Option = " + config.IngestAsset.CreationOption);

        // Setup blob container
        CloudBlobContainer sourceBlobContainer = GetCloudBlobContainer(_sourceStorageAccountName, _sourceStorageAccountKey, config.IngestSource.SourceContainerName);
        CloudBlobContainer destinationBlobContainer = GetCloudBlobContainer(_storageAccountName, _storageAccountKey, newAsset.Uri.Segments[1]);
        sourceBlobContainer.CreateIfNotExists();
        // Copy Source Blob container into Destination Blob container that is associated with the asset.
        CopyBlobsAsync(sourceBlobContainer, destinationBlobContainer, log);
    }
    catch (Exception ex)
    {
        log.Info("Exception " + ex);
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    log.Info("Asset ID : " + newAsset.Id);
    log.Info("Source Container : " + config.IngestSource.SourceContainerName);

    return req.CreateResponse(HttpStatusCode.OK, new
    {
        AssetId = newAsset.Id,
        SourceContainer = config.IngestSource.SourceContainerName,
        DestinationContainer = newAsset.Uri.Segments[1]
    });
}
