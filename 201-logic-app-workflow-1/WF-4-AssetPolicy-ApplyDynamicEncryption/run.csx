#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"

using System;
using System.Net;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.MediaServices.Client.ContentKeyAuthorization;
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption;
using Microsoft.WindowsAzure.MediaServices.Client.Widevine;
using Microsoft.WindowsAzure.MediaServices.Client.FairPlay;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

private static CloudMediaContext _context = null;
private static readonly string _amsAADTenantDomain = Environment.GetEnvironmentVariable("AMSAADTenantDomain");
private static readonly string _amsRestApiEndpoint = Environment.GetEnvironmentVariable("AMSRestApiEndpoint");
private static readonly string _amsClientId = Environment.GetEnvironmentVariable("AMSClientId");
private static readonly string _amsClientSecret = Environment.GetEnvironmentVariable("AMSClientSecret");
private static readonly string _amsStorageAccountName = Environment.GetEnvironmentVariable("AMSStorageAccountName");
private static readonly string _amsStorageAccountKey = Environment.GetEnvironmentVariable("AMSStorageAccountKey");

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);
    log.Info("Request : " + jsonContent);
    
    // Validate input objects
    if (data.AssetId == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass AssetId in the input object" });
    log.Info("Input - AssetId : " + data.AssetId);
    if (data.CencAuthPolicyId == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass CencAuthPolicyId in the input object" });
    log.Info("Input - CencAuthPolicyId : " + data.CencAuthPolicyId);
    if (data.CencAssetDeliveryPolicyId == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass CencAssetDeliveryPolicyId in the input object" });
    log.Info("Input - CencAssetDeliveryPolicyId : " + data.CencAssetDeliveryPolicyId);
    
    string assetid = data.AssetId;
    string cencAuthPolicyId = data.CencAuthPolicyId;
    string cencAssetDeliveryPolicyId = data.CencAssetDeliveryPolicyId;
    string keyId = null;
    try
    {
        // Load AMS account context
        log.Info($"Using Azure Media Service Rest API Endpoint : {_amsRestApiEndpoint}");
        AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(_amsAADTenantDomain,
            new AzureAdClientSymmetricKey(_amsClientId, _amsClientSecret),
            AzureEnvironments.AzureCloudEnvironment);
        AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);
        
        // there is a bug of the DataContext in SDK,
        // so we need to have different DataContext
        // for removing dynamic encryption policies if the asset already has
        if (!DeleteMultiDrmAuthorizationPolicyToAsset(tokenProvider, assetid, log))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });
        }
        
        // using new CloudMediaContext for applying dynamic encryption policies
        _context = new CloudMediaContext(new Uri(_amsRestApiEndpoint), tokenProvider);
        var asset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();
        if (asset == null)
        {
            log.Info("Asset not found - " + assetid);
            return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });
        }
        log.Info("Asset found, AssetId : " + asset.Id);

        // ContentKeyType.CommonEncryption
        IContentKey cencKey = CreateContentKeyCommonType();
        log.Info("Created CENC key " + cencKey.Id + " for the asset " + asset.Id);
        log.Info("PlayReady License Key delivery URL: " + cencKey.GetKeyDeliveryUrl(ContentKeyDeliveryType.PlayReadyLicense));
        Uri widevineUrl = cencKey.GetKeyDeliveryUrl(ContentKeyDeliveryType.Widevine);
        UriBuilder uriBuilder = new UriBuilder(widevineUrl);
        uriBuilder.Query = String.Empty;
        widevineUrl = uriBuilder.Uri;
        log.Info("Widevine License Key delivery URL: " + widevineUrl.ToString());
                
        asset.ContentKeys.Add(cencKey);
        IContentKeyAuthorizationPolicy cencAuthPol =
            _context.ContentKeyAuthorizationPolicies.Where(p => p.Id == cencAuthPolicyId).FirstOrDefault();
        if (cencAuthPol == null)
        {
            log.Info("Authorization Policy for CommonType not found - " + cencAuthPolicyId);
            return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "ContentKey Authorization Policy (CENC) not found" });
        }
        log.Info("ContentKey Authorization Policy (CENC) found, Policy Id : " + cencAuthPol.Id);
        cencKey.AuthorizationPolicyId = cencAuthPol.Id;
        cencKey = cencKey.UpdateAsync().Result;
        IAssetDeliveryPolicy cencAssetPol =
            _context.AssetDeliveryPolicies.Where(p => p.Id == cencAssetDeliveryPolicyId).FirstOrDefault();
        if (cencAssetPol == null)
        {
            log.Info("Asset Delivery Policy for CommonType not found - " + cencAssetDeliveryPolicyId);
            return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset Delivery Policy (CENC) not found" });
        }
        log.Info("Asset Delivery Policy (CENC) found, Policy Id : " + cencAssetPol.Id);
        asset.DeliveryPolicies.Add(cencAssetPol);
        
        keyId = cencKey.Id;
    }
    catch (Exception ex)
    {
        log.Info("Exception " + ex);
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }
    
    return req.CreateResponse(HttpStatusCode.OK, new
    {
        CencKeyId = keyId
    });
}

//private static readonly object listLock = new object();
static public bool DeleteMultiDrmAuthorizationPolicyToAsset(AzureAdTokenProvider tokenProvider, string assetId, TraceWriter log)
{
    // using new CloudMediaContext for removing dynamic encryption policies
    _context = new CloudMediaContext(new Uri(_amsRestApiEndpoint), tokenProvider);
    
    var asset = _context.Assets.Where(a => a.Id == assetId).FirstOrDefault();
    if (asset == null)
    {
        log.Info("Removing policies - Asset not found - " + assetId);
        return false;
    }
    log.Info("Removing policies - Asset found, AssetId : " + asset.Id);

    // Delete Locators    
    List<ILocator> locators = new List<ILocator>(asset.Locators);
    foreach (var loc in locators)
    {
        string locId = loc.Id;
        string locName = loc.Name;
        loc.Delete();
        log.Info("Removed Delivery Policy (" + locId + " = " + locName + ") from Asset " + asset.Id);
    }
    // Delete AssetDeliveryPolicies
    List<IAssetDeliveryPolicy> assetDeliveryPolicies = new List<IAssetDeliveryPolicy>(asset.DeliveryPolicies);
    foreach (var assetDeliveryPolicy in assetDeliveryPolicies)
    {
        asset.DeliveryPolicies.Remove(assetDeliveryPolicy);
        log.Info("Removed Delivery Policy (" + assetDeliveryPolicy.Id + " = " + assetDeliveryPolicy.Name + ") from Asset " + asset.Id);
    }
    // Delete ContentKey
    List<IContentKey> keys = new List<IContentKey>(asset.ContentKeys);
    foreach (var key in keys)
    {
        if (key.ContentKeyType != ContentKeyType.StorageEncryption)
        {
            asset.ContentKeys.Remove(key);
            log.Info("Removed Content Key (" + key.Id + " = " + key.Name + ") from Asset " + asset.Id);
        }
    }
    return true;
}

static public IContentKey CreateContentKeyCommonType()
{
    Guid keyId = Guid.NewGuid();
    byte[] contentKey = GetRandomBuffer(16);
    
    IContentKey key = _context.ContentKeys.Create(keyId, contentKey, "ContentKey CENC", ContentKeyType.CommonEncryption);
    // Associate the key with the asset.
    return key;
}

static public byte[] GetRandomBuffer(int length)
{
    var returnValue = new byte[length];
    
    using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
    {
        rng.GetBytes(returnValue);
    }
    return returnValue;
}