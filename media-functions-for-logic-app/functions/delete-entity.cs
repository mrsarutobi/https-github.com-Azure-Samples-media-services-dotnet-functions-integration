/*

Azure Media Services REST API v2 Function
 
This function delete AMS entities (job(s) and/or asset(s)).

Input:
{
    "jobID": "nb:jid:UUID:7f566f5e-be9c-434f-bb7b-101b2e24f27e,nb:jid:UUID:58f9e85a-a889-4205-baa1-ecf729f9c753",     // job(s) id. Coma delimited if several job ids 
    "assetId" : "nb:cid:UUID:61926f1d-69ba-4386-a90e-e27803104853,nb:cid:UUID:b4668bc4-2899-4247-b339-429025153ab9"   // asset(s) id.
}

Output:
{
        
}
*/


using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace media_functions_for_logic_app
{
    public static class delete_entity
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("delete-entity")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            log.Info(jsonContent);

            if (data.jobId == null && data.assetId == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass job Id and/or asset Id of the objects to delete"
                });
            }

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            log.Info($"Using Azure Media Service Rest API Endpoint : {amsCredentials.AmsRestApiEndpoint}");

            try
            {
                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                                                new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                                                AzureEnvironments.AzureCloudEnvironment);

                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);
            }
            catch (Exception ex)
            {
                log.Info($"Exception {ex}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Error = ex.ToString()
                });
            }

            try
            {
                if (data.jobId != null)
                {
                    var jobids = (string)data.jobId;
                    foreach (string jobi in jobids.Split(','))
                    {
                        log.Info($"Using job Id : {jobi}");
                        var job = _context.Jobs.Where(j => j.Id == jobi).FirstOrDefault();
                        if (job != null)
                        {
                            job.Delete();
                            log.Info("Job deleted.");
                        }
                        else
                        {
                            log.Info("Job not found!");
                        }
                    }
                }

                if (data.assetId != null)
                {
                    var assetids = (string)data.assetId;
                    foreach (string asseti in assetids.Split(','))
                    {
                        log.Info($"Using asset Id : {asseti}");
                        var asset = _context.Assets.Where(a => a.Id == asseti).FirstOrDefault();
                        if (asset != null)
                        {
                            asset.Delete();
                            log.Info("Asset deleted.");
                        }
                        else
                        {
                            log.Info("Asset not found!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                log.Info($"ERROR: Exception {message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {

            });
        }
    }
}

