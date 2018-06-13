//
// Azure Media Services REST API v2 Functions
//
// MonitorMediaJob - This function monitors media job.
//
//  Input:
//      {
//          "jobId": "nb:jid:UUID:1904c0ff-0300-80c0-9cb2-f1e868091e81"    // Id of the media job
//      }
//  Output:
//      {
//          "jobState": 0           // Status code of the media job
//          // https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.mediaservices.client.jobstate?view=azure-dotnet
//          //      Queued      0
//          //      Scheduled   1
//          //      Processing  2
//          //      Finished    3
//          //      Error       4
//          //      Canceled    5
//          //      Canceling   6
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
    public static class MonitorMediaJob
    {
        private static CloudMediaContext _context = null;

        [FunctionName("MonitorMediaJob")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"AMS v2 Function - MonitorMediaJob was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            // parameter handling
            if (data.jobId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass jobId in the input object" });
            string jobId = data.jobId;

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            IJob job = null;

            try
            {
                // Load AMS account context
                log.Info($"Using AMS v2 REST API Endpoint : {amsCredentials.AmsRestApiEndpoint.ToString()}");

                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                    new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                    AzureEnvironments.AzureCloudEnvironment);
                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);
                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);

                job = _context.Jobs.Where(j => j.Id == jobId).FirstOrDefault();
                if (job == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Job not found" });
                }

                //if (job.State == JobState.Error || job.State == JobState.Canceled)
                //{
                //    return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Job was in error or cancelled" });
                //}
            }
            catch (Exception e)
            {
                log.Info($"Exception {e}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                jobState = job.State
            });
        }
    }
}
