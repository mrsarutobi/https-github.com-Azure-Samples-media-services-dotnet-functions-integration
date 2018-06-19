/*

Azure Media Services REST API v2 Function
 
This function creates an empty asset.

Input:
{
    "assetName" : "the name of the asset",
    "assetStorage" :"amsstore01" // optional. Name of attached storage where to create the asset 
}

Output:
{
    "assetId" : "the Id of the asset created",
    "containerPath" : "the url to the storage container of the asset"
}

*/


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
using Microsoft.Azure.WebJobs.Host;


namespace media_functions_for_logic_app
{
    public static class create_empty_asset
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("create-empty-asset")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)

        {
            log.Info($"Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            log.Info(jsonContent);

            if (data.assetName == null)
            {
                // for test
                // data.Path = "/input/WP_20121015_081924Z.mp4";

                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass assetName in the input object"
                });
            }

            string assetName = data.assetName;

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            log.Info($"Using Azure Media Service Rest API Endpoint : {amsCredentials.AmsRestApiEndpoint}");

            IAsset newAsset = null;

            try
            {
                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                                             new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                                             AzureEnvironments.AzureCloudEnvironment);

                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);

                log.Info("Context object created.");

                newAsset = _context.Assets.Create(assetName, (string)data.assetStorage, AssetCreationOptions.None);

                log.Info("new asset created.");

            }
            catch (Exception ex)
            {
                log.Info($"Exception {ex}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Error = ex.ToString()
                });
            }


            log.Info("asset Id: " + newAsset.Id);
            log.Info("container Path: " + newAsset.Uri.Segments[1]);

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                containerPath = newAsset.Uri.Segments[1],
                assetId = newAsset.Id
            });
        }
    }
}




