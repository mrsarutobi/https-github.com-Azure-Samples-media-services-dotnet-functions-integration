//
// Azure Media Services REST API v2 - Functions
//
// Shared Library
//

using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.WindowsAzure.MediaServices.Client;

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

        public static IMediaProcessor GetLatestMediaProcessorByName(CloudMediaContext context, string mediaProcessorName)
        {
            var processor = context.MediaProcessors.Where(p => p.Name == mediaProcessorName).
            ToList().OrderBy(p => new Version(p.Version)).LastOrDefault();

            if (processor == null)
                throw new ArgumentException(string.Format("Unknown media processor", mediaProcessorName));

            return processor;
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
