/*

Azure Media Services REST API v2 Function
 
This function creates an empty asset.

Input:
{
    "assetName" : "the name of the asset",
    "assetStorage" :"amsstore01" // optional. Name of attached storage where to create the asset
    "alternateId" : "data" //optional. Set data in alternate id
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
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
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

                if (data.alternateId != null)
                {
                    newAsset.AlternateId = (string)data.alternateId;
                    newAsset.Update();
                }

            }
            catch (Exception ex)
            {
                string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                log.Info($"ERROR: Exception {message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
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




