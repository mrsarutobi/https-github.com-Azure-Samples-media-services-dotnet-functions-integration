//
// Azure Media Services REST API v2 - Functions
//
// Shared Library
//

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption;
using Microsoft.WindowsAzure.Storage.Blob;

using Newtonsoft.Json;


namespace advanced_vod_functions.SharedLibs
{
    public class MediaServicesHelper
    {
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


        public static IMediaProcessor GetLatestMediaProcessorByName(CloudMediaContext context, string mediaProcessorName)
        {
            var processor = context.MediaProcessors.Where(p => p.Name == mediaProcessorName).
            ToList().OrderBy(p => new Version(p.Version)).LastOrDefault();

            if (processor == null)
                throw new ArgumentException(string.Format("Unknown media processor", mediaProcessorName));

            return processor;
        }
        public static IContentKey CreateContentKey(CloudMediaContext context, string contentKeyName, ContentKeyType keyType)
        {
            Guid keyId = Guid.NewGuid();
            byte[] contentKey = GenericHelper.GetRandomBuffer(16);
            IContentKey key = context.ContentKeys.Create(keyId, contentKey, contentKeyName, keyType);
            return key;
        }

        public static string GetContentFromAssetFile(IAssetFile assetFile)
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
                System.Text.Encoding fileEncoding = streamReader.CurrentEncoding;
                datastring = streamReader.ReadToEnd();
                streamReader.Close();

                File.Delete(filePath);
            }
            catch (Exception e)
            {
                throw e;
            }

            return datastring;
        }

        public static CloudBlockBlob WriteContentToBlob(IAsset asset, CloudBlobContainer dstContainer, string dstBlobName, string blobContent)
        {
            //string uri = null;
            CloudBlockBlob blob = null;

            try
            {
                //dstContainer.CreateIfNotExists();
                blob = dstContainer.GetBlockBlobReference(dstBlobName);
                blob.DeleteIfExists();

                var options = new BlobRequestOptions()
                {
                    ServerTimeout = TimeSpan.FromMinutes(10)
                };
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(blobContent), false))
                {
                    blob.UploadFromStream(stream, null, options);
                }

                IAssetFile assetFile = asset.AssetFiles.Create(dstBlobName);
                blob.FetchAttributes();
                assetFile.ContentFileSize = blob.Properties.Length;
                //assetFile.IsPrimary = false;
                assetFile.Update();
            }
            catch (Exception e)
            {
                throw e;
            }

            return blob;
        }

    }

    public class AMSMediaTask
    {
        public string mediaTaskName { get; set; }

        public string mediaProcessor { get; set; }

        public string configuration { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> additionalInputAssetIds { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string outputStorageAccount { get; set; }
    }
}
