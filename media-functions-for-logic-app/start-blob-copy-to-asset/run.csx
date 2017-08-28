/*
This function copy a file (blob) or several blobs to a new asset previously created.

Input:
{
    "assetId" : "the Id of the asset where the file must be copied",
    "fileName" : "filename.mp4", // use fileName if one file, or FileNames if several files
    "fileNames" : [ "filename.mp4" , "filename2.mp4"],
    "sourceStorageAccountName" : "",
    "sourceStorageAccountKey": "",
    "sourceContainer" : ""
}
Output:
{
 "destinationContainer": "" // container of asset
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

        // Find the Asset
        newAsset = _context.Assets.Where(a => a.Id == assetId).FirstOrDefault();
        if (newAsset == null)
            return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });

        // Setup blob container
        CloudBlobContainer sourceBlobContainer = GetCloudBlobContainer(_sourceStorageAccountName, _sourceStorageAccountKey, (string)data.sourceContainer);
        CloudBlobContainer destinationBlobContainer = GetCloudBlobContainer(_storageAccountName, _storageAccountKey, newAsset.Uri.Segments[1]);
        sourceBlobContainer.CreateIfNotExists();

        if (data.fileName != null)
        {
            string fileName = (string)data.fileName;

            CloudBlob sourceBlob = sourceBlobContainer.GetBlockBlobReference(fileName);
            CloudBlob destinationBlob = destinationBlobContainer.GetBlockBlobReference(fileName);

            if (destinationBlobContainer.CreateIfNotExists())
            {
                log.Info("container created");
                destinationBlobContainer.SetPermissions(new BlobContainerPermissions
                {
                    PublicAccess = BlobContainerPublicAccessType.Blob
                });
            }
            CopyBlobAsync(sourceBlob, destinationBlob);
        }

        if (data.fileNames != null)
        {
            foreach (var file in data.fileNames)
            {
                string fileName = (string)file;
                CloudBlob sourceBlob = sourceBlobContainer.GetBlockBlobReference(fileName);
                CloudBlob destinationBlob = destinationBlobContainer.GetBlockBlobReference(fileName);

                if (destinationBlobContainer.CreateIfNotExists())
                {
                    log.Info("container created");
                    destinationBlobContainer.SetPermissions(new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    });
                }
                CopyBlobAsync(sourceBlob, destinationBlob);
            }
        }
    }
    catch (Exception ex)
    {
        log.Info("Exception " + ex);
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }


    return req.CreateResponse(HttpStatusCode.OK, new
    {
        destinationContainer = newAsset.Uri.Segments[1]
    });
}
