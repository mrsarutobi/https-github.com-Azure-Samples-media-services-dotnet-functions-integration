//
// Azure Media Services REST API v2 Functions
//
// CreateAccessPolicy - This function creates AccessPolicy object.
//
//  Input:
//      {
//          "accessPolicyName": "1-day StreamingPolicy",    // Name of the access policy
//          "accessDuration": "1:00:00:00.0000000"          // Duration of time span
//              // format = d:hh:mm:ss.fffffff (long general format = "G")
//              // https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-timespan-format-strings#the-general-long-g-format-specifier
//      }
//  Output:
//      {
//          "accessPolicyId": "nb:pid:UUID:a8bc8819-c2cf-4b7a-bc54-e3667f92dc80"    // Id of the access policy
//      }
//

using System;
using System.Globalization;
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
    public static class CreateAccessPolicy
    {
        private static CloudMediaContext _context = null;

        [FunctionName("CreateAccessPolicy")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"AMS v2 Function - CreateAccessPolicy was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            // parameter handling
            if (data.accessPolicyName == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass accessPolicyName in the input object" });
            if (data.accessDuration == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass accessDuration in the input object" });
            string accessPolicyName = data.accessPolicyName;
            string accessDuration = data.accessDuration;

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
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

                TimeSpan duration = TimeSpan.ParseExact(accessDuration, "G", CultureInfo.CurrentCulture);
                accessPolicy = _context.AccessPolicies.Create(accessPolicyName, duration, AccessPermissions.Read);
            }
            catch (Exception e)
            {
                log.Info($"Exception {e}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                accessPolicyId = accessPolicy.Id
            });
        }
    }
}
