/*

Azure Media Services REST API v2 Function
 
This function creates a program.

Input:
{
    "channelName" : "the name of the existing channel",
    "programName" : "the name of the program to create",
    "assetName" : "the name of the asset",
    "assetStorage" :"amsstore01" // optional. Name of attached storage where to create the asset
    "alternateId" : "data" //optional. Set data in alternate id,
    "description" : "my description" // optional,
    "manifestName" : "myname" // optional
    "archiveWindowLength" : 5  // optional (min)
    "startProgram" : true // optional, default is true
}

Output:
{
    "assetId" : "the Id of the asset created",
    "containerPath" : "the url to the storage container of the asset",
    "manifestName" : "the manifest name",
    "state" : "Running"
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
    public static class create_program
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("create-program")]
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
            string assetName = data.assetName;
            IProgram program = null;
            TimeSpan archiveLength = new TimeSpan(0, data.archiveWindowLength != null ? (int)data.archiveWindowLength : 5, 0); // default 5 min

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

                var channel = _context.Channels.Where(c => c.Name == channelName).FirstOrDefault();
                if (channel == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = string.Format("Channel {0} not found", channelName)
                    });

                }

                newAsset = _context.Assets.Create(assetName, (string)data.assetStorage, AssetCreationOptions.None);

                log.Info("new asset created.");

                if (data.alternateId != null)
                {
                    newAsset.AlternateId = (string)data.alternateId;
                    newAsset.Update();
                }


                ProgramCreationOptions options = new ProgramCreationOptions()
                {
                    Name = programName,
                    ManifestName = data.manifestName,
                    Description = data.description,
                    AssetId = newAsset.Id,
                    ArchiveWindowLength = archiveLength
                };
                program = channel.Programs.Create(options);

                log.Info("new program created.");


                if (data.startProgram == null || (data.startProgram != null && (bool)data.startProgram))
                {
                    log.Info("starting program...");
                    program.Start();
                    log.Info("Program started.");
                }

                program = _context.Programs.Where(p => p.Id == program.Id).FirstOrDefault(); // refresh
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
                assetId = newAsset.Id,
                manifestName = program.ManifestName,
                id = program.Id,
                state = program.State
            });
        }
    }
}




