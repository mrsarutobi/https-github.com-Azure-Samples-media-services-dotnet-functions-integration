/*

Azure Media Services REST API v2 Function
 
This function publishes an asset.

Input:
{
    "assetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Mandatory, Id of the source asset
    "preferredSE" : "default" // Optional, name of Streaming Endpoint if a specific Streaming Endpoint should be used for the URL outputs
    "locatorId" : "bde256b9-67a6-4876-acc6-5505c7a2a3c6" // Optional, the locator Id to use
}

Output:
{
    playerUrl : "", // Url of demo AMP with content
    smoothUrl : "", // Url for the published asset (contains name.ism/manifest at the end) for dynamic packaging
    pathUrl : ""    // Url of the asset (path)
}
*/


using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;


namespace media_functions_for_logic_app
{
    public static class publish_asset
    {
        public const string LocatorIdPrefix = "nb:lid:UUID:";

        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("publish-asset")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            {
                log.Info($"Webhook was triggered!");

                string jsonContent = await req.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(jsonContent);

                log.Info(jsonContent);

                if (data.assetId == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = "Please pass asset ID in the input object (assetId)"
                    });
                }

                string playerUrl = "";
                string smoothUrl = "";
                string pathUrl = "";
                string preferredSE = data.preferredSE;

                MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
                log.Info($"Using Azure Media Service Rest API Endpoint : {amsCredentials.AmsRestApiEndpoint}");

                try
                {
                    AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                                                    new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                                                    AzureEnvironments.AzureCloudEnvironment);

                    AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

                    _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);

                    // Get the asset
                    string assetid = data.assetId;
                    var outputAsset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

                    if (outputAsset == null)
                    {
                        log.Info($"Asset not found {assetid}");

                        return req.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            error = "Asset not found"
                        });
                    }

                    string locatorId = data.locatorId;
                    if (locatorId != null && !locatorId.StartsWith(LocatorIdPrefix))
                    {
                        locatorId = LocatorIdPrefix + locatorId;
                    }

                    // publish with a streaming locator (100 years)
                    IAccessPolicy readPolicy2 = _context.AccessPolicies.Create("readPolicy", TimeSpan.FromDays(365 * 100), AccessPermissions.Read);
                    ILocator outputLocator2 = _context.Locators.CreateLocator(locatorId, LocatorType.OnDemandOrigin, outputAsset, readPolicy2, null);

                    var publishurlsmooth = MediaServicesHelper.GetValidOnDemandURI(_context, outputAsset, preferredSE);
                    var publishurlpath = MediaServicesHelper.GetValidOnDemandPath(_context, outputAsset, preferredSE);

                    if (outputLocator2 != null && publishurlsmooth != null)
                    {
                        smoothUrl = publishurlsmooth.ToString();
                        playerUrl = "https://ampdemo.azureedge.net/?url=" + HttpUtility.UrlEncode(smoothUrl);
                        log.Info($"smooth url : {smoothUrl}");
                    }

                    if (outputLocator2 != null && publishurlpath != null)
                    {
                        pathUrl = publishurlpath.ToString();
                        log.Info($"path url : {pathUrl}");
                    }
                }

                catch (Exception ex)
                {
                    string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                    log.Info($"ERROR: Exception {message}");
                    return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
                }

                log.Info($"");
                return req.CreateResponse(HttpStatusCode.OK, new
                {
                    playerUrl = playerUrl,
                    smoothUrl = smoothUrl,
                    pathUrl = pathUrl
                });
            }
        }
    }
}
