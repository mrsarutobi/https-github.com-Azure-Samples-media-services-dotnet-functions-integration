/*
This function publishes an asset.

Input:
{
    "assetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Mandatory, Id of the source asset
    "preferredSE" : "default" // Optional, name of Streaming Endpoint if a specific Streaming Endpoint should be used for the URL outputs
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
    public static class publish_asset
    {
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
                    // for test
                    // data.assetId = "nb:cid:UUID:c0d770b4-1a69-43c4-a4e6-bc60d20ab0b2";
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

                    // publish with a streaming locator (100 years)
                    IAccessPolicy readPolicy2 = _context.AccessPolicies.Create("readPolicy", TimeSpan.FromDays(365 * 100), AccessPermissions.Read);
                    ILocator outputLocator2 = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, outputAsset, readPolicy2);

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
                    log.Info($"Exception {ex}");
                    return req.CreateResponse(HttpStatusCode.InternalServerError, new
                    {
                        Error = ex.ToString()
                    });
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
