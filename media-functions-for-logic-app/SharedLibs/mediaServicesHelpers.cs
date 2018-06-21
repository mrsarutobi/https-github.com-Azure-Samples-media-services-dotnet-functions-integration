//
// Azure Media Services REST API v2 - Functions
//
// Shared Library
//

using System;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.IO;
using System.Text;
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace media_functions_for_logic_app
{
    public class MediaServicesCredentials
    {

        public string AmsAadTenantDomain
        {
            get { return Environment.GetEnvironmentVariable("AMSAADTenantDomain"); }
        }

        public string AmsClientId
        {
            get { return Environment.GetEnvironmentVariable("AMSClientId"); }
        }

        public string AmsClientSecret
        {
            get { return Environment.GetEnvironmentVariable("AMSClientSecret"); }
        }

        public Uri AmsRestApiEndpoint
        {
            get { return new Uri(Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint")); }
        }

        public string StorageAccountName
        {
            get { return Environment.GetEnvironmentVariable("MediaServicesStorageAccountName"); }
        }

        public string StorageAccountKey
        {
            get { return Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey"); }
        }

        public string AttachedStorageCredentials
        {
            get { return Environment.GetEnvironmentVariable("MediaServicesAttachedStorageCredentials"); }
        }
    }

    public class MediaServicesHelper
    {
        public static IMediaProcessor GetLatestMediaProcessorByName(CloudMediaContext context, string mediaProcessorName)
        {
            var processor = context.MediaProcessors.Where(p => p.Name == mediaProcessorName).
            ToList().OrderBy(p => new Version(p.Version)).LastOrDefault();

            if (processor == null)
                throw new ArgumentException(string.Format("Unknown media processor", mediaProcessorName));

            return processor;
        }

        public static Uri GetValidOnDemandURI(CloudMediaContext context, IAsset asset, string preferredSE = null)
        {
            var aivalidurls = GetValidURIs(context, asset, preferredSE);
            if (aivalidurls != null)
            {
                return aivalidurls.FirstOrDefault();
            }
            else
            {
                return null;
            }
        }

        public static IEnumerable<Uri> GetValidURIs(CloudMediaContext context, IAsset asset, string preferredSE = null)
        {
            IEnumerable<Uri> ValidURIs;
            var ismFile = asset.AssetFiles.AsEnumerable().Where(f => f.Name.EndsWith(".ism")).OrderByDescending(f => f.IsPrimary).FirstOrDefault();

            if (ismFile != null)
            {
                var locators = asset.Locators.Where(l => l.Type == LocatorType.OnDemandOrigin && l.ExpirationDateTime > DateTime.UtcNow).OrderByDescending(l => l.ExpirationDateTime);

                var se = context.StreamingEndpoints.AsEnumerable().Where(o =>

                    (string.IsNullOrEmpty(preferredSE) || (o.Name == preferredSE))
                    &&
                    (!string.IsNullOrEmpty(preferredSE) || ((o.State == StreamingEndpointState.Running) && (CanDoDynPackaging(o)))
                                                                            ))
                .OrderByDescending(o => o.CdnEnabled);


                if (se.Count() == 0) // No running which can do dynpackaging SE and if not preferredSE. Let's use the default one to get URL
                {
                    se = context.StreamingEndpoints.AsEnumerable().Where(o => o.Name == "default").OrderByDescending(o => o.CdnEnabled);
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

        public static Uri GetValidOnDemandPath(CloudMediaContext context, IAsset asset, string preferredSE = null)
        {
            var aivalidurls = GetValidPaths(context, asset, preferredSE);
            if (aivalidurls != null)
            {
                return aivalidurls.FirstOrDefault();
            }
            else
            {
                return null;
            }
        }

        public static IEnumerable<Uri> GetValidPaths(CloudMediaContext context, IAsset asset, string preferredSE = null)
        {
            IEnumerable<Uri> ValidURIs;

            var locators = asset.Locators.Where(l => l.Type == LocatorType.OnDemandOrigin && l.ExpirationDateTime > DateTime.UtcNow).OrderByDescending(l => l.ExpirationDateTime);

            //var se = _context.StreamingEndpoints.AsEnumerable().Where(o => (o.State == StreamingEndpointState.Running) && (CanDoDynPackaging(o))).OrderByDescending(o => o.CdnEnabled);

            var se = context.StreamingEndpoints.AsEnumerable().Where(o =>

                   (string.IsNullOrEmpty(preferredSE) || (o.Name == preferredSE))
                   &&
                   (!string.IsNullOrEmpty(preferredSE) || ((o.State == StreamingEndpointState.Running) && (CanDoDynPackaging(o)))
                                                                           ))
               .OrderByDescending(o => o.CdnEnabled);

            if (se.Count() == 0) // No running which can do dynpackaging SE and if not preferredSE. Let's use the default one to get URL
            {
                se = context.StreamingEndpoints.AsEnumerable().Where(o => o.Name == "default").OrderByDescending(o => o.CdnEnabled);
            }
          
            var template = new UriTemplate("{contentAccessComponent}/");
            ValidURIs = locators.SelectMany(l => se.Select(
                        o =>
                            template.BindByPosition(new Uri("https://" + o.HostName), l.ContentAccessComponent)))
                .ToArray();
            /*   
            var templates = "/{0}";

            ValidURIs = locators.SelectMany(l =>
                se.Select(
                        o =>
                          
                           new Uri("https://" + o.HostName + string.Format(templates, l.ContentAccessComponent))))
                .ToArray();
            */

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


        // Return the new name of Media Reserved Unit
        public static string ReturnMediaReservedUnitName(ReservedUnitType unitType)
        {
            switch (unitType)
            {
                case ReservedUnitType.Basic:
                default:
                    return "S1";

                case ReservedUnitType.Standard:
                    return "S2";

                case ReservedUnitType.Premium:
                    return "S3";
            }
        }

        static public void SetFileAsPrimary(IAsset asset, string assetfilename)
        {
            var ismAssetFiles = asset.AssetFiles.ToList().
                Where(f => f.Name.Equals(assetfilename, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (ismAssetFiles.Count() == 1)
            {
                try
                {
                    // let's remove primary attribute to another file if any
                    asset.AssetFiles.Where(af => af.IsPrimary).ToList().ForEach(af => { af.IsPrimary = false; af.Update(); });
                    ismAssetFiles.First().IsPrimary = true;
                    ismAssetFiles.First().Update();
                }
                catch
                {
                    throw;
                }
            }
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



        public static ILocator CreatedTemporaryOnDemandLocator(IAsset asset)
        {
            ILocator tempLocator = null;

            try
            {
                var locatorTask = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        tempLocator = asset.GetMediaContext().Locators.Create(LocatorType.OnDemandOrigin, asset, AccessPermissions.Read, TimeSpan.FromHours(1));
                    }
                    catch
                    {
                        throw;
                    }
                });
                locatorTask.Wait();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return tempLocator;
        }

        public static string ReturnNewRUName(ReservedUnitType reservedUnitType)
        {
            return "S" + ((int)reservedUnitType + 1);
        }


        public static string GetErrorMessage(Exception e)
        {
            string s = "";

            while (e != null)
            {
                s = e.Message;
                e = e.InnerException;
            }
            return ParseXml(s);
        }

        public static string ParseXml(string strXml)
        {
            try
            {
                var message = XDocument
                    .Parse(strXml)
                    .Descendants()
                    .Where(d => d.Name.LocalName == "message")
                    .Select(d => d.Value)
                    .SingleOrDefault();

                return message;
            }
            catch
            {
                return strXml;
            }
        }
    }

}

