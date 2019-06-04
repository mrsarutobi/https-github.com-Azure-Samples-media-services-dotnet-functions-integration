/*

Azure Media Services REST API v2 Function
 
This function deletes a program.

Input:
{
    "channelName" : "the name of the existing channel",
    "programName" : "the name of the program to create",
    "deleteAsset" : true // optional, default is false
}

Output:
{
    "success" : true
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
using System.Linq;

namespace media_functions_for_logic_app
{
    public static class delete_program
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("delete-program")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)

        {
            log.Info($"Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            log.Info(jsonContent);

            if (data.channelName == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass channelName in the input object"
                });
            }
            if (data.programName == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass programName in the input object"
                });
            }
            string channelName = data.channelName;
            string programName = data.programName;
            IProgram program = null;

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            log.Info($"Using Azure Media Service Rest API Endpoint : {amsCredentials.AmsRestApiEndpoint}");

            try
            {
                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                                             new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                                             AzureEnvironments.AzureCloudEnvironment);

                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);

                log.Info("Context object created.");

                var channel = _context.Channels.Where(c => c.Name == channelName).FirstOrDefault();
                if (channel == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = string.Format("Channel {0} not found", channelName)
                    });

                }
              
                program = channel.Programs.Where(p => p.Name.Equals(programName)).FirstOrDefault();
                if (program == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = string.Format("Program {0} not found", programName)
                    });

                }
                var asset = program.Asset;

                log.Info("Deleting program...");
                program.Delete();
                log.Info("Program deleted.");

                if (data.deleteAsset != null && (bool)data.deleteAsset)
                {
                    log.Info("Deleting asset...");
                    asset.Delete();
                    log.Info("Asset deleted.");
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
                success = true
            });
        }
    }
}




