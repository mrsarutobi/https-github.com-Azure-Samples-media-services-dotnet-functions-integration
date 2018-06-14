//
// Azure Media Services REST API v2 Functions
//
// PublishAsset - This function publishes asset.
//
//  Input:
//      {
//          "assetId": "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",          // Id of the asset created
//          "accessPolicyId": "nb:pid:UUID:a8bc8819-c2cf-4b7a-bc54-e3667f92dc80",   // Id of the access policy
//          "startDateTime": "2018-12-31 00:00:00Z"                                 // Start date of publishing
//              // https://msdn.microsoft.com/en-us/library/system.globalization.datetimeformatinfo(v=vs.110).aspx
//              // format = "yyyy'-'MM'-'dd HH':'mm':'ss'Z'"
//      }
//  Output:
//      {
//      }
//

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;

using Newtonsoft.Json;

using advanced_vod_functions.SharedLibs;


namespace advanced_vod_functions
{
    public static class PublishAsset
    {
        private static CloudMediaContext _context = null;

        [FunctionName("PublishAsset")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"AMS v2 Function - PublishAsset was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            // parameter handling
            if (data.assetId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetId in the input object" });
            string assetId = data.assetId;
            if (data.accessPolicyId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass accessPolicyId in the input object" });
            string accessPolicyId = data.accessPolicyId;
            string startDateTime = null;
            if (data.startDateTime != null)
            {
                startDateTime = data.startDateTime;
            }


            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            IAsset asset = null;
            IAccessPolicy accessPolicy = null;

            try
            {
                // Load AMS account context
                log.Info($"Using AMS v2 REST API Endpoint : {amsCredentials.AmsRestApiEndpoint.ToString()}");

                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                    new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                    AzureEnvironments.AzureCloudEnvironment);
                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);
                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);

                // Get the Asset
                asset = _context.Assets.Where(a => a.Id == assetId).FirstOrDefault();
                if (asset == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });
                }

                accessPolicy = _context.AccessPolicies.Where(a => a.Id == accessPolicyId).FirstOrDefault();
                if (accessPolicyId == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "AccessPolicy object not found" });
                }
                DateTime start = DateTime.Now;
                if (startDateTime != null) start = Convert.ToDateTime(startDateTime);
                ILocator outputLocator = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, asset, accessPolicy, start);
            }
            catch (Exception e)
            {
                log.Info($"Exception {e}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
            });
        }
    }
}
