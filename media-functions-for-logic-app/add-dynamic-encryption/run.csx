/*
This function add dynamic encryption to the asset or program. It attached the 

Input:
{
    "assetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", Id of the source asset
    "programId" : "nb:pgid:UUID:5d547b03-3b56-47ae-a479-88cddf21a7fa",  or program Id
    "contentKeyAuthorizationPolicyId": "nb:ckpid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810", // Optional, Id of the ContentKeyAuthorizationPolicy object
    "assetDeliveryPolicyId": "nb:adpid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",           // Id of the AssetDeliveryPolicy object    
    "contentKeyType": "CommonEncryption",                                                    // Name of the ContentKeyType
          // https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.mediaservices.client.contentkeytype?view=azure-dotnet
          //  CommonEncryption
          //  CommonEncryptionCbcs
          //  EnvelopeEncryption
    "contentKeyName": "Common Encryption ContentKey"                        // Optional, Name of the ContentKey object
    "keyId" : "",      Optional
    "contentKey" :"" , Optional, base64 of the content key
}

Output:
{
   "contentKeyId": "nb:kid:UUID:489a97f4-9a31-4174-ac92-0c76e8dbdc06"      // Id of the ContentKey object
}
*/

#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
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
using Microsoft.WindowsAzure.MediaServices.Client.ContentKeyAuthorization;
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption;

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

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log, Microsoft.Azure.WebJobs.ExecutionContext execContext)
{
    log.Info($"Webhook was triggered!");

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);

    log.Info(jsonContent);

    if (data.assetId == null && data.programId == null)
    {
        // for test
        // data.assetId = "nb:cid:UUID:c0d770b4-1a69-43c4-a4e6-bc60d20ab0b2";
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass asset ID or program Id in the input object (assetId or programId)"
        });
    }



    string contentKeyAuthorizationPolicyId = data.contentKeyAuthorizationPolicyId;
    string assetDeliveryPolicyId = data.assetDeliveryPolicyId;
    string contentKeyTypeName = data.contentKeyType;
    string keyId = data.keyId;
    string contentKeySecret = data.contentKey;
    if (!AMSContentKeyType.ContainsKey(contentKeyTypeName))
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass a valid contentKeyType in the input object" });
    ContentKeyType contentKeyType = AMSContentKeyType[contentKeyTypeName];
    if (contentKeyType != ContentKeyType.CommonEncryption && contentKeyType != ContentKeyType.CommonEncryptionCbcs && contentKeyType != ContentKeyType.EnvelopeEncryption)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass a valid contentKeyType in the input object" });
    string contentKeyName = data.contentKeyName;

    IContentKeyAuthorizationPolicy ckaPolicy = null;
    IAssetDeliveryPolicy adPolicy = null;
    IContentKey contentKey = null;


    log.Info($"Using Azure Media Service Rest API Endpoint : {_RESTAPIEndpoint}");

    try
    {
        AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(_AADTenantDomain,
                            new AzureAdClientSymmetricKey(_mediaservicesClientId, _mediaservicesClientSecret),
                            AzureEnvironments.AzureCloudEnvironment);

        AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

        _context = new CloudMediaContext(new Uri(_RESTAPIEndpoint), tokenProvider);

        // Get the asset
        string assetid = null;

        if (data.assetId != null)
        {
            assetid = data.assetId;
        }
        else
        {
            var program = _context.Programs.Where(a => a.Id == data.programId).FirstOrDefault();
            if (program == null)
            {
                log.Info($"Program not found {data.programId}");

                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Program not found"
                });
            }
            assetid = program.AssetId;
        }
        var outputAsset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

        if (outputAsset == null)
        {
            log.Info($"Asset not found {assetid}");

            return req.CreateResponse(HttpStatusCode.BadRequest, new
            {
                error = "Asset not found"
            });
        }


        // Key creation or retrieval
        if (keyId != null)
        {
            contentKey = _context.ContentKeys.Where(k => k.Id == "keyId").FirstOrDefault();
        }
        if (contentKey == null)
        {
            switch (contentKeyType)
            {
                case ContentKeyType.CommonEncryption:
                    if (contentKeyName == null) contentKeyName = "Common Encryption ContentKey";
                    contentKey = MediaServicesHelper.CreateContentKey(_context, contentKeyName, ContentKeyType.CommonEncryption, keyId, contentKeySecret);
                    break;
                case ContentKeyType.CommonEncryptionCbcs:
                    if (contentKeyName == null) contentKeyName = "Common Encryption CBCS ContentKey";
                    contentKey = MediaServicesHelper.CreateContentKey(_context, contentKeyName, ContentKeyType.CommonEncryptionCbcs, keyId, contentKeySecret);
                    break;
                case ContentKeyType.EnvelopeEncryption:
                    if (contentKeyName == null) contentKeyName = "Envelope Encryption ContentKey";
                    contentKey = MediaServicesHelper.CreateContentKey(_context, contentKeyName, ContentKeyType.EnvelopeEncryption, keyId, contentKeySecret);
                    break;
            }

        }
        outputAsset.ContentKeys.Add(contentKey);

        // Authorization policy
        if (contentKeyAuthorizationPolicyId != null)
        {
            ckaPolicy = _context.ContentKeyAuthorizationPolicies.Where(p => p.Id == contentKeyAuthorizationPolicyId).FirstOrDefault();
            if (ckaPolicy == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "ContentKeyAuthorizationPolicy not found" });
            }
         
            contentKey.AuthorizationPolicyId = ckaPolicy.Id;
            contentKey = contentKey.UpdateAsync().Result;
        }


        // Delivery policy
        if (assetDeliveryPolicyId != null)
        {
            adPolicy = _context.AssetDeliveryPolicies.Where(p => p.Id == assetDeliveryPolicyId).FirstOrDefault();
            if (adPolicy == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Delivery policy not found not found" });
            }
            outputAsset.DeliveryPolicies.Add(adPolicy);
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
        contentKeyId = contentKey.Id
    });
}
