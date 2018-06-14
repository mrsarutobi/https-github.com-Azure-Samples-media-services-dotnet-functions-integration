//
// Azure Media Services REST API v2 Functions
//
// CreateContentKeyAuthorizationPolicy - This function creates new ContentKeyAuthorizationPolicy object in the AMS account.
//
//  Input:
//      {
//          "contentKeyAuthorizationPolicyName": "Open CENC Key Authorization Policy",  // Name of the ContentKeyAuthorizationPolicy object
//          "contentKeyAuthorizationPolicyOptionIds": [                                 // List of the ContentKeyAuthorizationPolicyOption Identifiers
//              "nb:ckpoid:UUID:68adb036-43b7-45e6-81bd-8cf32013c821",
//              "nb:ckpoid:UUID:68adb036-43b7-45e6-81bd-8cf32013c822"
//          ]
//      }
//  Output:
//      {
//          "contentKeyAuthorizationPolicyId": "nb:ckpid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",
//                                                                                      // Id of the AssetDeliveryPolicy object
//      }
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.MediaServices.Client.ContentKeyAuthorization;
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using advanced_vod_functions.SharedLibs;


namespace advanced_vod_functions
{
    public static class CreateContentKeyAuthorizationPolicy
    {
        private static CloudMediaContext _context = null;

        [FunctionName("CreateContentKeyAuthorizationPolicy")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"AMS v2 Function - CreateContentKeyAuthorizationPolicy was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            // Validate input objects
            if (data.contentKeyAuthorizationPolicyName == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass contentKeyAuthorizationPolicyName in the input object" });
            if (data.contentKeyAuthorizationPolicyOptionIds == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass contentKeyAuthorizationPolicyOptionIds in the input object" });
            string contentKeyAuthorizationPolicyName = data.contentKeyAuthorizationPolicyName;
            List<string> contentKeyAuthorizationPolicyOptionIds = ((JArray)data.contentKeyAuthorizationPolicyOptionIds).ToObject<List<string>>();

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            IContentKeyAuthorizationPolicy policy = null;

            try
            {
                // Load AMS account context
                log.Info($"Using AMS v2 REST API Endpoint : {amsCredentials.AmsRestApiEndpoint.ToString()}");

                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                    new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                    AzureEnvironments.AzureCloudEnvironment);
                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);
                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);

                // validate contentKeyAuthorizationPolicyOptionIds list
                List<IContentKeyAuthorizationPolicyOption> options = new List<IContentKeyAuthorizationPolicyOption>();
                foreach (var policyOptionId in contentKeyAuthorizationPolicyOptionIds)
                {
                    var policyOpiton = _context.ContentKeyAuthorizationPolicyOptions.Where(p => p.Id == policyOptionId).Single();
                    if (policyOpiton == null)
                    {
                        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "ContentKeyAuthorizationPolicyOption not found" });
                    }
                    options.Add(policyOpiton);
                }

                policy = _context.ContentKeyAuthorizationPolicies.CreateAsync(contentKeyAuthorizationPolicyName).Result;
                foreach (var op in options) policy.Options.Add(op);
            }
            catch (Exception e)
            {
                log.Info($"ERROR: Exception {e}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                contentKeyAuthorizationPolicyId = policy.Id
            });
        }
    }
}
