#r "System.Web"
#r "System.ServiceModel"

using System;
using System.ServiceModel;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.IO;
using System.Text;

private static IMediaProcessor GetLatestMediaProcessorByName(string mediaProcessorName)
{
    var processor = _context.MediaProcessors.Where(p => p.Name == mediaProcessorName).
    ToList().OrderBy(p => new Version(p.Version)).LastOrDefault();

    if (processor == null)
        throw new ArgumentException(string.Format("Unknown media processor", mediaProcessorName));

    return processor;
}

public static Uri GetValidOnDemandURI(IAsset asset, string preferredSE = null)
{
    var aivalidurls = GetValidURIs(asset, preferredSE);
    if (aivalidurls != null)
    {
        return aivalidurls.FirstOrDefault();
    }
    else
    {
        return null;
    }
}

public static IEnumerable<Uri> GetValidURIs(IAsset asset, string preferredSE = null)
{
    IEnumerable<Uri> ValidURIs;
    var ismFile = asset.AssetFiles.AsEnumerable().Where(f => f.Name.EndsWith(".ism")).OrderByDescending(f => f.IsPrimary).FirstOrDefault();

    if (ismFile != null)
    {
        var locators = asset.Locators.Where(l => l.Type == LocatorType.OnDemandOrigin && l.ExpirationDateTime > DateTime.UtcNow).OrderByDescending(l => l.ExpirationDateTime);

        var se = _context.StreamingEndpoints.AsEnumerable().Where(o =>

            (string.IsNullOrEmpty(preferredSE) || (o.Name == preferredSE))
            &&
            (!string.IsNullOrEmpty(preferredSE) || ((o.State == StreamingEndpointState.Running) && (CanDoDynPackaging(o)))
                                                                    ))
        .OrderByDescending(o => o.CdnEnabled);


        if (se.Count() == 0) // No running which can do dynpackaging SE and if not preferredSE. Let's use the default one to get URL
        {
            se = _context.StreamingEndpoints.AsEnumerable().Where(o => o.Name == "default").OrderByDescending(o => o.CdnEnabled);
        }

        var template = new UriTemplate("{contentAccessComponent}/{ismFileName}/manifest");

        ValidURIs = locators.SelectMany(l =>
            se.Select(
                    o =>
                        template.BindByPosition(new Uri("https://" + o.HostName), l.ContentAccessComponent,
                            ismFile.Name)))
            .ToArray();

        return ValidURIs;
    }
    else
    {
        return null;
    }
}

public static Uri GetValidOnDemandPath(IAsset asset, string preferredSE = null)
{
    var aivalidurls = GetValidPaths(asset, preferredSE);
    if (aivalidurls != null)
    {
        return aivalidurls.FirstOrDefault();
    }
    else
    {
        return null;
    }
}

public static IEnumerable<Uri> GetValidPaths(IAsset asset, string preferredSE = null)
{
    IEnumerable<Uri> ValidURIs;

    var locators = asset.Locators.Where(l => l.Type == LocatorType.OnDemandOrigin && l.ExpirationDateTime > DateTime.UtcNow).OrderByDescending(l => l.ExpirationDateTime);

    //var se = _context.StreamingEndpoints.AsEnumerable().Where(o => (o.State == StreamingEndpointState.Running) && (CanDoDynPackaging(o))).OrderByDescending(o => o.CdnEnabled);

    var se = _context.StreamingEndpoints.AsEnumerable().Where(o =>

           (string.IsNullOrEmpty(preferredSE) || (o.Name == preferredSE))
           &&
           (!string.IsNullOrEmpty(preferredSE) || ((o.State == StreamingEndpointState.Running) && (CanDoDynPackaging(o)))
                                                                   ))
       .OrderByDescending(o => o.CdnEnabled);

    if (se.Count() == 0) // No running which can do dynpackaging SE and if not preferredSE. Let's use the default one to get URL
    {
        se = _context.StreamingEndpoints.AsEnumerable().Where(o => o.Name == "default").OrderByDescending(o => o.CdnEnabled);
    }

    var template = new UriTemplate("{contentAccessComponent}/");
    ValidURIs = locators.SelectMany(l => se.Select(
                o =>
                    template.BindByPosition(new Uri("https://" + o.HostName), l.ContentAccessComponent)))
        .ToArray();

    return ValidURIs;
}

static public bool CanDoDynPackaging(IStreamingEndpoint mySE)
{
    return ReturnTypeSE(mySE) != StreamEndpointType.Classic;
}

static public StreamEndpointType ReturnTypeSE(IStreamingEndpoint mySE)
{
    if (mySE.ScaleUnits != null && mySE.ScaleUnits > 0)
    {
        return StreamEndpointType.Premium;
    }
    else
    {
        if (new Version(mySE.StreamingEndpointVersion) == new Version("1.0"))
        {
            return StreamEndpointType.Classic;
        }
        else
        {
            return StreamEndpointType.Standard;
        }
    }
}

public enum StreamEndpointType
{
    Classic = 0,
    Standard,
    Premium
}

public static string ReturnContent(IAssetFile assetFile)
{
    string datastring = null;

    try
    {
        string tempPath = System.IO.Path.GetTempPath();
        string filePath = Path.Combine(tempPath, assetFile.Name);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        assetFile.Download(filePath);

        StreamReader streamReader = new StreamReader(filePath);
        Encoding fileEncoding = streamReader.CurrentEncoding;
        datastring = streamReader.ReadToEnd();
        streamReader.Close();

        File.Delete(filePath);
    }
    catch
    {

    }

    return datastring;
}


public static Dictionary<string, AssetCreationOptions> AMSAssetCreationOptions = new Dictionary<string, AssetCreationOptions>()
        {
            { "CommonEncryptionProtected",      AssetCreationOptions.CommonEncryptionProtected },
            { "EnvelopeEncryptionProtected",    AssetCreationOptions.EnvelopeEncryptionProtected },
            { "None",                           AssetCreationOptions.None },
            { "StorageEncrypted",               AssetCreationOptions.StorageEncrypted },
        };

public static Dictionary<string, AssetDeliveryPolicyType> AMSAssetDeliveryPolicyType = new Dictionary<string, AssetDeliveryPolicyType>()
        {
            { "NoDynamicEncryption",            AssetDeliveryPolicyType.NoDynamicEncryption },
            { "DynamicEnvelopeEncryption",      AssetDeliveryPolicyType.DynamicEnvelopeEncryption },
            { "DynamicCommonEncryption",        AssetDeliveryPolicyType.DynamicCommonEncryption },
            { "DynamicCommonEncryptionCbcs",    AssetDeliveryPolicyType.DynamicCommonEncryptionCbcs },
        };

public static Dictionary<string, AssetDeliveryProtocol> AMSAssetDeliveryProtocol = new Dictionary<string, AssetDeliveryProtocol>()
        {
            { "SmoothStreaming",                AssetDeliveryProtocol.SmoothStreaming },
            { "Dash",                           AssetDeliveryProtocol.Dash },
            { "HLS",                            AssetDeliveryProtocol.HLS },
            { "Hds",                            AssetDeliveryProtocol.Hds },
            { "ProgressiveDownload",            AssetDeliveryProtocol.ProgressiveDownload },
            { "All",                            AssetDeliveryProtocol.All },
        };

public enum AssetDeliveryContentProtection { AESClearKey = 1, PlayReady, Widevine, FairPlay };
public static Dictionary<string, AssetDeliveryContentProtection> AMSAssetDeliveryContentProtection = new Dictionary<string, AssetDeliveryContentProtection>()
        {
            { "AESClearKey",        AssetDeliveryContentProtection.AESClearKey },
            { "PlayReady",          AssetDeliveryContentProtection.PlayReady },
            { "Widevine",           AssetDeliveryContentProtection.Widevine },
            { "FairPlay",           AssetDeliveryContentProtection.FairPlay },
        };

public static Dictionary<string, ContentKeyType> AMSContentKeyType = new Dictionary<string, ContentKeyType>()
        {
            { "CommonEncryption",           ContentKeyType.CommonEncryption },
            { "StorageEncryption",          ContentKeyType.StorageEncryption },
            { "ConfigurationEncryption",    ContentKeyType.ConfigurationEncryption },
            { "UrlEncryption",              ContentKeyType.UrlEncryption },
            { "EnvelopeEncryption",         ContentKeyType.EnvelopeEncryption },
            { "CommonEncryptionCbcs",       ContentKeyType.CommonEncryptionCbcs },
            { "FairPlayASk",                ContentKeyType.FairPlayASk },
            { "FairPlayPfxPassword",        ContentKeyType.FairPlayPfxPassword },
        };

public static IContentKey CreateContentKey(CloudMediaContext context, string contentKeyName, ContentKeyType keyType, string keyId = null, string contentKeyb64 = null)
{
    byte[] contentKey;

    var keyidguid = (keyId == null) ? Guid.NewGuid() : Guid.Parse(keyId);

    if (contentKeyb64 == null)
    {
        contentKey = GetRandomBuffer(16);
    }
    else
    {
        contentKey = Convert.FromBase64String(contentKeyb64);
    }

    IContentKey key = context.ContentKeys.Create(keyidguid, contentKey, contentKeyName, keyType);
    return key;
}

public static byte[] GetRandomBuffer(int length)
{
    var returnValue = new byte[length];

    using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
    {
        rng.GetBytes(returnValue);
    }

    return returnValue;
}
