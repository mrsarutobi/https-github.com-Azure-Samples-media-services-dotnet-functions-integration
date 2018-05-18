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

private static readonly bool _isTokenRestricted = false;
private static readonly bool _isTokenTypeJWT = true;
private static readonly Uri _sampleIssuer = new Uri("urn:test");
private static readonly Uri _sampleAudience = new Uri("urn:test");
private static readonly string _symmetricVerificationKey = "YmY0MjA1MDkxZGE5NTU0MDNkYWEyMDdlMDc2YzdhZTZjMWEzN2FlNGFiNjI3MDM3ODIyODU3N2IyODQ3NmI2NGEzNGRkNWY5OTU0ZTkyNzNhNjk5NjhlZGI0MGI0N2FlYTAyOWFjMjU5ODBkMjlkYWY5YmQzMWU2M2U4ODNhOGY=";

private static readonly bool _enableKidClaim = false;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);
    log.Info("Request : " + jsonContent);
    
    // Validate input objects
    if (data.ContentKeyAuthorizationPolicyName == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass ContentKeyAuthorizationPolicyName in the input object" });
    log.Info("Input - ContentKeyAuthorizationPolicyName : " + data.ContentKeyAuthorizationPolicyName);
    if (data.AssetDeliveryPolicyname == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass AssetDeliveryPolicyname in the input object" });
    log.Info("Input - AssetDeliveryPolicyname : " + data.AssetDeliveryPolicyname);
    
    string contentKeyAuthorizationPolicyName = data.ContentKeyAuthorizationPolicyName;
    string assetDeliveryPolicyName = data.AssetDeliveryPolicyname;
    string contentKeyAuthorizationPolicyId = null;
    string assetDeliveryPolicyId = null;
    try
    {
        // Load AMS account context
        log.Info($"Using Azure Media Service Rest API Endpoint : {_amsRestApiEndpoint}");
        AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(_amsAADTenantDomain,
            new AzureAdClientSymmetricKey(_amsClientId, _amsClientSecret),
            AzureEnvironments.AzureCloudEnvironment);
        AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);
        
        // using new CloudMediaContext for applying dynamic encryption policies
        _context = new CloudMediaContext(new Uri(_amsRestApiEndpoint), tokenProvider);
        
        // Search ContentKeyAuthorizationPolicy with ContentKeyType.CommonEncryption
        IContentKeyAuthorizationPolicy apol = 
            _context.ContentKeyAuthorizationPolicies.Where(p => p.Name == contentKeyAuthorizationPolicyName).FirstOrDefault();
        if (apol != null)
        {
            log.Info("Already exist CENC Type Policy: Id = " + apol.Id + ", Name = " + apol.Name);
            contentKeyAuthorizationPolicyId = apol.Id;
        }
        else
        {
            apol = CreateAuthorizationPolicyCommonType(contentKeyAuthorizationPolicyName);
            log.Info("Created CENC Type Policy: Id = " + apol.Id + ", Name = " + apol.Name);
            contentKeyAuthorizationPolicyId = apol.Id;
        }
        
        // Search AssetDeliveryPolicy with ContentKeyType.CommonEncryption
        IAssetDeliveryPolicy dpol = 
            _context.AssetDeliveryPolicies.Where(p => p.Name == assetDeliveryPolicyName).FirstOrDefault();
        if (dpol != null)
        {
            log.Info("Already exist Asset Delivery CENC Type Policy: Id = " + dpol.Id + ", Name = " + dpol.Name);
            assetDeliveryPolicyId = dpol.Id;
        }
        else
        {
            dpol = CreateAssetDeliveryPolicyCenc(assetDeliveryPolicyName);
            log.Info("Created Asset Delivery CENC Type Policy: Id = " + dpol.Id + ", Name = " + dpol.Name);
            assetDeliveryPolicyId = dpol.Id;
        }
    }
    catch (Exception ex)
    {
        log.Info("Exception " + ex);
        return new HttpResponseMessage(HttpStatusCode.BadRequest);
    }
    
    return req.CreateResponse(HttpStatusCode.OK, new
    {
        ContentKeyAuthorizationPolicyId = contentKeyAuthorizationPolicyId,
        AssetDeliveryPolicyId = assetDeliveryPolicyId
    });
}

static public IContentKeyAuthorizationPolicy CreateAuthorizationPolicyCommonType(string policyName)
{
    List<ContentKeyAuthorizationPolicyRestriction> restrictions;
    string PlayReadyOptionName;
    string WidevineOptionName;
    if (_isTokenRestricted)
    {
        string tokenTemplateString = GenerateTokenRequirements();
        restrictions = new List<ContentKeyAuthorizationPolicyRestriction>
        {
            new ContentKeyAuthorizationPolicyRestriction
            {
                Name = "Token Authorization Policy",
                KeyRestrictionType = (int)ContentKeyRestrictionType.TokenRestricted,
                Requirements = tokenTemplateString,
            }
        };
        PlayReadyOptionName = "TokenRestricted PlayReady Option 1";
        WidevineOptionName = "TokenRestricted Widevine Option 1";
    }
    else
    {
        restrictions = new List<ContentKeyAuthorizationPolicyRestriction>
        {
            new ContentKeyAuthorizationPolicyRestriction
            {
                Name = "Open",
                KeyRestrictionType = (int)ContentKeyRestrictionType.Open,
                Requirements = null
            }
        };
        PlayReadyOptionName = "Open PlayReady Option 1";
        WidevineOptionName = "Open Widevine Option 1";
    }
    
    // Configure PlayReady and Widevine license templates.
    string PlayReadyLicenseTemplate = ConfigurePlayReadyPolicyOptions();
    string WidevineLicenseTemplate = ConfigureWidevinePolicyOptions();
    
    IContentKeyAuthorizationPolicyOption PlayReadyPolicy =
        _context.ContentKeyAuthorizationPolicyOptions.Create(PlayReadyOptionName, ContentKeyDeliveryType.PlayReadyLicense, restrictions, PlayReadyLicenseTemplate);
    IContentKeyAuthorizationPolicyOption WidevinePolicy =
        _context.ContentKeyAuthorizationPolicyOptions.Create(WidevineOptionName, ContentKeyDeliveryType.Widevine, restrictions, WidevineLicenseTemplate);
    IContentKeyAuthorizationPolicy contentKeyAuthorizationPolicy = _context.ContentKeyAuthorizationPolicies.CreateAsync(policyName).Result;
    
    contentKeyAuthorizationPolicy.Options.Add(PlayReadyPolicy);
    contentKeyAuthorizationPolicy.Options.Add(WidevinePolicy);
    
    return contentKeyAuthorizationPolicy;
}

static public IAssetDeliveryPolicy CreateAssetDeliveryPolicyCenc(string assetDeliveryPolicyName)
{
    Guid keyId = Guid.NewGuid();
    byte[] contentKey = GetRandomBuffer(16);
    IContentKey key = _context.ContentKeys.Create(keyId, contentKey, "ContentKey CENC", ContentKeyType.CommonEncryption);
    
    // Get the PlayReady license service URL.
    Uri acquisitionUrl = key.GetKeyDeliveryUrl(ContentKeyDeliveryType.PlayReadyLicense);
    
    // GetKeyDeliveryUrl for Widevine attaches the KID to the URL.
    // For example: https://amsaccount1.keydelivery.mediaservices.windows.net/Widevine/?KID=268a6dcb-18c8-4648-8c95-f46429e4927c.  
    // The WidevineBaseLicenseAcquisitionUrl (used below) also tells Dynamaic Encryption
    // to append /? KID =< keyId > to the end of the url when creating the manifest.
    // As a result Widevine license acquisition URL will have KID appended twice,
    // so we need to remove the KID that in the URL when we call GetKeyDeliveryUrl.
    Uri widevineUrl = key.GetKeyDeliveryUrl(ContentKeyDeliveryType.Widevine);
    UriBuilder uriBuilder = new UriBuilder(widevineUrl);
    uriBuilder.Query = String.Empty;
    widevineUrl = uriBuilder.Uri;
    
    Dictionary<AssetDeliveryPolicyConfigurationKey, string> assetDeliveryPolicyConfiguration =
        new Dictionary<AssetDeliveryPolicyConfigurationKey, string>
        {
            {AssetDeliveryPolicyConfigurationKey.PlayReadyLicenseAcquisitionUrl, acquisitionUrl.ToString()},
            {AssetDeliveryPolicyConfigurationKey.WidevineBaseLicenseAcquisitionUrl, widevineUrl.ToString()}
        };
    
    // In this case we only specify Dash streaming protocol in the delivery policy,
    // All other protocols will be blocked from streaming.
    var assetDeliveryPolicy = _context.AssetDeliveryPolicies.Create(
        assetDeliveryPolicyName,
        //"AssetDeliveryPolicy CommonEncryption (SmoothStreaming, Dash)",
        AssetDeliveryPolicyType.DynamicCommonEncryption,
        AssetDeliveryProtocol.Dash | AssetDeliveryProtocol.SmoothStreaming,
        assetDeliveryPolicyConfiguration);
    key.Delete();
    //("Create AssetDeliveryPolicy: Id = {0}, Name = {1}", assetDeliveryPolicy.Id, assetDeliveryPolicy.Name);
    return assetDeliveryPolicy;
}

private static byte[] GetRandomBuffer(int length)
{
    var returnValue = new byte[length];
    
    using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
    {
        rng.GetBytes(returnValue);
    }
    
    return returnValue;
}

private static string GenerateTokenRequirements()
{
    TokenType tType = TokenType.SWT;
    if (_isTokenTypeJWT) tType = TokenType.JWT;
    TokenRestrictionTemplate template = new TokenRestrictionTemplate(tType);
    template.PrimaryVerificationKey = new SymmetricVerificationKey(Convert.FromBase64String(_symmetricVerificationKey));
    //template.AlternateVerificationKeys.Add(new SymmetricVerificationKey());
    template.Audience = _sampleAudience.ToString();
    template.Issuer = _sampleIssuer.ToString();
    if (_enableKidClaim)
    {
        template.RequiredClaims.Add(TokenClaim.ContentKeyIdentifierClaim);
    }
    return TokenRestrictionTemplateSerializer.Serialize(template);
}

private static string ConfigurePlayReadyPolicyOptions()
{
    // The following code configures PlayReady License Template using .NET classes
    // and returns the XML string.
    
    //The PlayReadyLicenseResponseTemplate class represents the template for the response sent back to the end user.
    //It contains a field for a custom data string between the license server and the application
    //(may be useful for custom app logic) as well as a list of one or more license templates.
    PlayReadyLicenseResponseTemplate responseTemplate = new PlayReadyLicenseResponseTemplate();
    
    // The PlayReadyLicenseTemplate class represents a license template for creating PlayReady licenses
    // to be returned to the end users.
    //It contains the data on the content key in the license and any rights or restrictions to be
    //enforced by the PlayReady DRM runtime when using the content key.
    PlayReadyLicenseTemplate licenseTemplate = new PlayReadyLicenseTemplate();
    //Configure whether the license is persistent (saved in persistent storage on the client)
    //or non-persistent (only held in memory while the player is using the license).  
    licenseTemplate.LicenseType = PlayReadyLicenseType.Nonpersistent;
    
    // AllowTestDevices controls whether test devices can use the license or not.  
    // If true, the MinimumSecurityLevel property of the license
    // is set to 150.  If false (the default), the MinimumSecurityLevel property of the license is set to 2000.
    licenseTemplate.AllowTestDevices = false;
    
    // You can also configure the Play Right in the PlayReady license by using the PlayReadyPlayRight class.
    // It grants the user the ability to playback the content subject to the zero or more restrictions
    // configured in the license and on the PlayRight itself (for playback specific policy).
    // Much of the policy on the PlayRight has to do with output restrictions
    // which control the types of outputs that the content can be played over and
    // any restrictions that must be put in place when using a given output.
    // For example, if the DigitalVideoOnlyContentRestriction is enabled,
    //then the DRM runtime will only allow the video to be displayed over digital outputs
    //(analog video outputs won’t be allowed to pass the content).
    
    //IMPORTANT: These types of restrictions can be very powerful but can also affect the consumer experience.
    // If the output protections are configured too restrictive,
    // the content might be unplayable on some clients. For more information, see the PlayReady Compliance Rules document.
    
    // For example:
    //licenseTemplate.PlayRight.AgcAndColorStripeRestriction = new AgcAndColorStripeRestriction(1);
    
    responseTemplate.LicenseTemplates.Add(licenseTemplate);
    return MediaServicesLicenseTemplateSerializer.Serialize(responseTemplate);
}

private static string ConfigureWidevinePolicyOptions()
{
    var template = new WidevineMessage
    {
        allowed_track_types = AllowedTrackTypes.SD_HD,
        content_key_specs = new[]
        {
            new ContentKeySpecs
            {
                required_output_protection = new RequiredOutputProtection { hdcp = Hdcp.HDCP_NONE},
                security_level = 1,
                track_type = "SD"
            }
        },
        policy_overrides = new
        {
            can_play = true,
            can_persist = true,
            can_renew = false
            //renewal_server_url = keyDeliveryUrl.ToString(),
        }
    };
    
    string configuration = JsonConvert.SerializeObject(template);
    return configuration;
}