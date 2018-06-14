//
// Azure Media Services REST API v2 Functions
//
// SubmitMediaJob - This function submits media job.
//
//  Input:
//      {
//          "assetId": "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",  // Id of the asset for copy destination
//          "mediaTasks" [
//              {
//                  "mediaTaskName": "MediaEncoding",                       // Name of the media task
//                  "mediaProcessor": "Media Encoder Standard",             // Name of Media Processor for the media task
//                  "configuration": "configuration string",                // Configuration parameter of Media Processor
//                                                                          // You can set Base64 encoded text data which starts with "base64,".
//                  "additionalInputAssetIds": [                            // (Optional) Id list of additional input assets
//                      "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c811",
//                      "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c812"
//                  ],
//                  "outputStorageAccount": "amsstorage01"                  // (Optional) Name of the Storage account where to put the output asset (attached to AMS account)
//              },
//              {
//                  ...
//              }
//          ],
//          "jobPriority" : 10,                                             // (Optional) Priority of the media job
//          "jobName" : "Azure Function Media Job"                          // (Optional) Name of the media job
//      }
//  Output:
//      {
//          "jobId": "nb:jid:UUID:1904c0ff-0300-80c0-9cb2-f1e868091e81",                        // Id of the media job
//          "mediaTaskOutputs" [
//              {
//                  "mediaTaskIndex": 0,                                                        // Index of the media task
//                  "mediaTaskId": "nb:tid:UUID:1904c0ff-0300-80c0-9cb3-f1e868091e81",          // Id of the media task
//                  "mediaTaskName": "Azure Functions: Task for Media Encoder Standard",        // Name of the nedia task
//                  "mediaProcessorId": "nb:mpid:UUID:ff4df607-d419-42f0-bc17-a481b1331e56",    // Id of Media Processor for the media task
//                  "mediaTaskOutputAssetId": "nb:cid:UUID:8739680c-2708-4cb9-b8f1-300c41a92423"
//                                                                                              // Id of the output asset for the media task
//              },
//              {
//                  ...
//              }
//          ]
//      }
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using advanced_vod_functions.SharedLibs;


namespace advanced_vod_functions
{
    public static class SubmitMediaJob
    {
        private static CloudMediaContext _context = null;
        private static string base64encodedstringprefix = "base64,";

        [FunctionName("SubmitMediaJob")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"AMS v2 Function - SubmitMediaJob was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            // Validate input objects
            if (data.assetId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetId in the input object" });
            if (data.mediaTasks == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass mediaTasks in the input object" });
            string assetId = data.assetId;
            List<AMSMediaTask> mediaTasks = ((JArray)data.mediaTasks).ToObject<List<AMSMediaTask>>();
            int jobPriority = 10;
            if (data.jobPriority != null)
                jobPriority = data.jobPriority;
            string jobName = "Azure Functions - Media Processing Job";
            if (data.jobName != null)
                jobName = data.jobName;

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            IAsset asset = null;
            IJob job = null;
            uint taskId = 0;

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

                // Declare a new Media Processing Job
                job = _context.Jobs.Create(jobName + " - " + asset.Name + " [" + assetId + "]");
                job.Priority = jobPriority;

                foreach (AMSMediaTask mediaTask in mediaTasks)
                {
                    ITask task = null;
                    IMediaProcessor processor = MediaServicesHelper.GetLatestMediaProcessorByName(_context, mediaTask.mediaProcessor);
                    if (mediaTask.configuration.StartsWith(base64encodedstringprefix))
                    {
                        byte[] b64decoded = Convert.FromBase64String(mediaTask.configuration.Substring(base64encodedstringprefix.Length));
                        mediaTask.configuration = System.Text.ASCIIEncoding.ASCII.GetString(b64decoded);
                    }
                    task = job.Tasks.AddNew(mediaTask.mediaTaskName, processor, mediaTask.configuration, TaskOptions.None);
                    if (mediaTask.additionalInputAssetIds != null)
                    {
                        foreach (string inputAssetId in mediaTask.additionalInputAssetIds)
                        {
                            IAsset aAsset = _context.Assets.Where(a => a.Id == inputAssetId).FirstOrDefault();
                            task.InputAssets.Add(aAsset);
                        }
                    }
                    // Add primary input asset at last
                    task.InputAssets.Add(asset);

                    string outputAssetName = asset.Name + " - " + mediaTask.mediaProcessor;
                    IAsset outputAsset = task.OutputAssets.AddNew(outputAssetName, mediaTask.outputStorageAccount, asset.Options);

                    taskId++;
                }

                // media job submission
                job.Submit();
            }
            catch (Exception e)
            {
                log.Info($"Exception {e}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            // Prepare output JSON
            int taskIndex = 0;
            JArray mediaTaskOutputs = new JArray();
            foreach (var task in job.Tasks)
            {
                JObject o = new JObject();
                o["mediaTaskIndex"] = taskIndex;
                o["mediaTaskId"] = task.Id;
                o["mediaTaskName"] = task.Name;
                o["mediaProcessorId"] = task.MediaProcessorId;
                o["mediaTaskOutputAssetId"] = task.OutputAssets[0].Id;
                mediaTaskOutputs.Add(o);
                taskIndex++;
            }

            JObject result = new JObject();
            result["jobId"] = job.Id;
            result["mediaTaskOutputs"] = mediaTaskOutputs;
            return req.CreateResponse(HttpStatusCode.OK, result);
        }
    }
}
