/*
This function create the asset files based on the blobs in the asset container.

Input:
{
    "assetId" : "the Id of the asset"
}

*/

#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "System.Web"
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

    log.Info(jsonContent);

    if (data.assetId == null)
    {
        // for test
        // data.Path = "/input/WP_20121015_081924Z.mp4";

        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass assetId in the input object"
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


        // Step 1:  Copy the Blob into a new Input Asset for the Job
        // ***NOTE: Ideally we would have a method to ingest a Blob directly here somehow. 
        // using code from this sample - https://azure.microsoft.com/en-us/documentation/articles/media-services-copying-existing-blob/

        // Get the asset
        string assetid = data.assetId;
        var asset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

        if (asset == null)
        {
            log.Info($"Asset not found {assetid}");

            return req.CreateResponse(HttpStatusCode.BadRequest, new
            {
                error = "Asset not found"
            });
        }

        log.Info("Asset found, ID: " + asset.Id);

        //Get a reference to the storage account that is associated with the Media Services account. 
        StorageCredentials mediaServicesStorageCredentials =
            new StorageCredentials(_storageAccountName, _storageAccountKey);
        var _destinationStorageAccount = new CloudStorageAccount(mediaServicesStorageCredentials, false);

        CloudBlobClient destBlobStorage = _destinationStorageAccount.CreateCloudBlobClient();

        // Get the destination asset container reference
        string destinationContainerName = asset.Uri.Segments[1];
        log.Info($"destinationContainerName : {destinationContainerName}");

        CloudBlobContainer assetContainer = destBlobStorage.GetContainerReference(destinationContainerName);
        log.Info($"assetContainer retrieved");

        // Get hold of the destination blobs
        var blobs = assetContainer.ListBlobs();
        log.Info($"blobs retrieved");

        log.Info($"blobs count : {blobs.Count()}");

        var aflist = asset.AssetFiles.ToList().Select(af => af.Name);

        foreach (CloudBlockBlob blob in blobs)
        {
            if (aflist.Contains(blob.Name))
            {
                var assetFile = asset.AssetFiles.Where(af => af.Name == blob.Name).FirstOrDefault();
                assetFile.ContentFileSize = blob.Properties.Length;
                assetFile.Update();
                log.Info($"Asset file updated : {assetFile.Name}");
            }
            else
            {
                var assetFile = asset.AssetFiles.Create(blob.Name);
                assetFile.ContentFileSize = blob.Properties.Length;
                assetFile.Update();
                log.Info($"Asset file created : {assetFile.Name}");
            }
        }

        asset.Update();
        SetAFileAsPrimary(asset);

        log.Info("Asset updated");
    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.InternalServerError, new
        {
            Error = ex.ToString()
        });
    }


    return req.CreateResponse(HttpStatusCode.OK);
}


static public void SetAFileAsPrimary(IAsset asset)
{
    var files = asset.AssetFiles.ToList();
    var ismAssetFiles = files.
        Where(f => f.Name.EndsWith(".ism", StringComparison.OrdinalIgnoreCase)).ToArray();

    var mp4AssetFiles = files.
    Where(f => f.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)).ToArray();

    if (ismAssetFiles.Count() != 0)
    {
        if (ismAssetFiles.Where(af => af.IsPrimary).ToList().Count == 0) // if there is a primary .ISM file
        {
            try
            {
                ismAssetFiles.First().IsPrimary = true;
                ismAssetFiles.First().Update();
            }
            catch
            {
                throw;
            }
        }
    }
    else if (mp4AssetFiles.Count() != 0)
    {
        if (mp4AssetFiles.Where(af => af.IsPrimary).ToList().Count == 0) // if there is a primary .ISM file
        {
            try
            {
                mp4AssetFiles.First().IsPrimary = true;
                mp4AssetFiles.First().Update();
            }
            catch
            {
                throw;
            }
        }
    }
    else
    {
        if (files.Where(af => af.IsPrimary).ToList().Count == 0) // if there is a primary .ISM file
        {
            try
            {
                files.First().IsPrimary = true;
                files.First().Update();
            }
            catch
            {
                throw;
            }
        }
    }
}



