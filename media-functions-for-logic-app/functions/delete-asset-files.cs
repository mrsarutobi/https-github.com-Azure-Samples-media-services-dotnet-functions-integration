/*
This function deletes the files from a specific asset.

Input:
{
    "assetId" : "the Id of the asset",
    "startsWith" : "video", //optional, copy only files that start with name video
    "endsWith" : ".mp4", //optional, copy only files that end with .mp4
}
Output:
{
}

*/


using System;
using System.Net;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Azure.WebJobs;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Linq;

namespace media_functions_for_logic_app
{
    public static class delete_asset_file
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("delete-asset-file")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)


        {
            log.Info($"Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);
            log.Info("Request : " + jsonContent);

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            log.Info($"Using Azure Media Service Rest API Endpoint : {amsCredentials.AmsRestApiEndpoint}");

            // Validate input objects
            if (data.assetId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetId in the input object" });

            string startsWith = data.startsWith;
            string endsWith = data.endsWith;


            string assetId = data.assetId;

            IAsset asset = null;
            try
            {

                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                                             new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                                             AzureEnvironments.AzureCloudEnvironment);

                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);

                // Find the Asset
                asset = _context.Assets.Where(a => a.Id == assetId).FirstOrDefault();
                if (asset == null)
                    return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });

                var files = asset.AssetFiles.ToList().Where(f => ((string.IsNullOrEmpty(endsWith) || f.Name.EndsWith(endsWith)) && (string.IsNullOrEmpty(startsWith) || f.Name.StartsWith(startsWith))));

                foreach (var file in files)
                {
                    file.Delete();
                    log.Info($"Deleted file: {file.Name}");
                }
            }
            catch (Exception ex)
            {
                log.Info("Exception " + ex);
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
