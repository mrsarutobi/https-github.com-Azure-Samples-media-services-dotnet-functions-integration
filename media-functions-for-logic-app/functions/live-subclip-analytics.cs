/*

Azure Media Services REST API v2 Function
 
This function submits a job to process a live stream with media analytics.
The first task is a subclipping task that createq a MP4 file, then media analytics are processed on this asset.

Input:
{
    "channelName": "channel1",      // Mandatory
    "programName" : "program1",     // Mandatory
    "intervalSec" : 60              // Optional. Default is 60 seconds. The duration of subclip (and interval between two calls)

    "mesSubclip" :      // Optional as subclip will always be done but it is required to specify an output storage
    {
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "mesThumbnails" :      // Optional but required to generate thumbnails with Media Encoder Standard (MES)
    {
        "start" : "{Best}",  // Optional. Start time/mode. Default is "{Best}"
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
     "indexV1" :             // Optional but required to index audio with Media Indexer v1
    {
        "language" : "English", // Optional. Default is "English"
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    }


    // General job properties
    "priority" : 10,                            // Optional, priority of the job
 
    // For compatibility only with old workflows. Do not use anymore!
    "indexV1Language" : "English",  // Optional
    "mesThumbnailsStart" : "{Best}",            // Optional. Add a task to generate thumbnails
}

Output:
{
        "triggerStart" : "" // date and time when the function was called
        "jobId" :  // job id
        "subclip" :
        {
            assetId : "",
            taskId : "",
            start : "",
            duration : ""
        },
        "indexV1" :
        {
            assetId : "",
            taskId : "",
            language : ""
        },
        "programId" = programid,
        "channelName" : "",
        "programName" : "",
        "programUrl":"",
        "programState" : "Running",
        "programStateChanged" : "True", // if state changed since last call
        "otherJobsQueue" = 3 // number of jobs in the queue
}
*/


using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace media_functions_for_logic_app
{
    public static class live_subclip_analytics
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("live-subclip-analytics")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log, Microsoft.Azure.WebJobs.ExecutionContext execContext)
        {
            // Variables
            int taskindex = 0;
            int OutputMES = -1;
            int OutputPremium = -1;
            int OutputIndex1 = -1;
            int OutputMesThumbnails = -1;

            int id = 0;
            string programid = "";
            string programName = "";
            string channelName = "";
            string programUrl = "";
            string programState = "";
            string lastProgramState = "";

            IJob job = null;
            ITask taskEncoding = null;
            int NumberJobsQueue = 0;

            int intervalsec = 60; // Interval for each subclip job (sec). Default is 60

            TimeSpan starttime = TimeSpan.FromSeconds(0);
            TimeSpan duration = TimeSpan.FromSeconds(intervalsec);

            log.Info($"Webhook was triggered!");
            string triggerStart = DateTime.UtcNow.ToString("o");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            log.Info(jsonContent);

            if (data.channelName == null || data.programName == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass channel name and program name in the input object (channelName, programName)"
                });
            }

            if (data.intervalSec != null)
            {
                intervalsec = (int)data.intervalSec;
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

                // find the Channel, Program and Asset
                channelName = (string)data.channelName;
                var channel = _context.Channels.Where(c => c.Name == channelName).FirstOrDefault();
                if (channel == null)
                {
                    log.Info("Channel not found");
                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = "Channel not found"
                    });
                }

                programName = (string)data.programName;
                var program = channel.Programs.Where(p => p.Name == programName).FirstOrDefault();
                if (program == null)
                {
                    log.Info("Program not found");
                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = "Program not found"
                    });
                }

                programState = program.State.ToString();
                programid = program.Id;
                var asset = ManifestHelpers.GetAssetFromProgram(_context, programid);

                if (asset == null)
                {
                    log.Info($"Asset not found for program {programid}");
                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = "Asset not found"
                    });
                }

                log.Info($"Using asset Id : {asset.Id}");

                // Table storage to store and real the last timestamp processed
                // Retrieve the storage account from the connection string.
                CloudStorageAccount storageAccount = new CloudStorageAccount(new StorageCredentials(amsCredentials.StorageAccountName, amsCredentials.StorageAccountKey), true);

                // Create the table client.
                CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

                // Retrieve a reference to the table.
                CloudTable table = tableClient.GetTableReference("liveanalytics");

                // Create the table if it doesn't exist.

                if (!table.CreateIfNotExists())
                {
                    log.Info($"Table {table.Name} already exists");
                }
                else
                {
                    log.Info($"Table {table.Name} created");
                }

                var lastendtimeInTable = ManifestHelpers.RetrieveLastEndTime(table, programid);

                // Get the manifest data (timestamps)
                var assetmanifestdata = ManifestHelpers.GetManifestTimingData(_context, asset, log);
                if (assetmanifestdata.Error)
                {
                    return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = "Data cannot be read from program manifest." });
                }

                log.Info("Timestamps: " + string.Join(",", assetmanifestdata.TimestampList.Select(n => n.ToString()).ToArray()));

                var livetime = TimeSpan.FromSeconds((double)assetmanifestdata.TimestampEndLastChunk / (double)assetmanifestdata.TimeScale);

                log.Info($"Livetime: {livetime}");

                starttime = ManifestHelpers.ReturnTimeSpanOnGOP(assetmanifestdata, livetime.Subtract(TimeSpan.FromSeconds(intervalsec)));
                log.Info($"Value starttime : {starttime}");

                if (lastendtimeInTable != null)
                {
                    lastProgramState = lastendtimeInTable.ProgramState;
                    log.Info($"Value ProgramState retrieved : {lastProgramState}");

                    var lastendtimeInTableValue = TimeSpan.Parse(lastendtimeInTable.LastEndTime);
                    log.Info($"Value lastendtimeInTable retrieved : {lastendtimeInTableValue}");

                    id = int.Parse(lastendtimeInTable.Id);
                    log.Info($"Value id retrieved : {id}");

                    if (lastendtimeInTableValue != null)
                    {
                        var delta = (livetime - lastendtimeInTableValue - TimeSpan.FromSeconds(intervalsec)).Duration();
                        log.Info($"Delta: {delta}");

                        //if (delta < (new TimeSpan(0, 0, 3*intervalsec))) // less than 3 times the normal duration (3*60s)
                        if (delta < (TimeSpan.FromSeconds(3 * intervalsec))) // less than 3 times the normal duration (3*60s)
                        {
                            starttime = lastendtimeInTableValue;
                            log.Info($"Value new starttime : {starttime}");
                        }
                    }
                }

                duration = livetime - starttime;
                log.Info($"Value duration: {duration}");
                if (duration == new TimeSpan(0)) // Duration is zero, this may happen sometimes !
                {
                    return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = "Stopping. Duration of subclip is zero." });
                }

                // D:\home\site\wwwroot\Presets\LiveSubclip.json
                string ConfigurationSubclip = File.ReadAllText(Path.Combine(System.IO.Directory.GetParent(execContext.FunctionDirectory).FullName, "presets", "LiveSubclip.json")).Replace("0:00:00.000000", starttime.Subtract(TimeSpan.FromMilliseconds(100)).ToString()).Replace("0:00:30.000000", duration.Add(TimeSpan.FromMilliseconds(200)).ToString());

                int priority = 10;
                if (data.priority != null)
                {
                    priority = (int)data.priority;
                }

                // MES Subclipping TASK
                // Declare a new encoding job with the Standard encoder
                job = _context.Jobs.Create("Azure Function - Job for Live Analytics - " + programName, priority);
                // Get a media processor reference, and pass to it the name of the 
                // processor to use for the specific task.
                IMediaProcessor processor = MediaServicesHelper.GetLatestMediaProcessorByName(_context, "Media Encoder Standard");

                // Change or modify the custom preset JSON used here.
                // string preset = File.ReadAllText("D:\home\site\wwwroot\Presets\H264 Multiple Bitrate 720p.json");

                // Create a task with the encoding details, using a string preset.
                // In this case "H264 Multiple Bitrate 720p" system defined preset is used.
                taskEncoding = job.Tasks.AddNew("Subclipping task",
                   processor,
                   ConfigurationSubclip,
                   TaskOptions.None);

                // Specify the input asset to be encoded.
                taskEncoding.InputAssets.Add(asset);
                OutputMES = taskindex++;

                // Add an output asset to contain the results of the job. 
                // This output is specified as AssetCreationOptions.None, which 
                // means the output asset is not encrypted. 
                var subclipasset = taskEncoding.OutputAssets.AddNew(asset.Name + " subclipped " + triggerStart, JobHelpers.OutputStorageFromParam(data.mesSubclip), AssetCreationOptions.None);

                log.Info($"Adding media analytics tasks");

                //new
                OutputIndex1 = JobHelpers.AddTask(execContext, _context, job, subclipasset, (data.indexV1 == null) ? (string)data.indexV1Language : ((string)data.indexV1.language ?? "English"), "Azure Media Indexer", "IndexerV1.xml", "English", ref taskindex, specifiedStorageAccountName: JobHelpers.OutputStorageFromParam(data.indexV1));

                // MES Thumbnails
                OutputMesThumbnails = JobHelpers.AddTask(execContext, _context, job, subclipasset, (data.mesThumbnails != null) ? ((string)data.mesThumbnails.Start ?? "{Best}") : null, "Media Encoder Standard", "MesThumbnails.json", "{Best}", ref taskindex, specifiedStorageAccountName: JobHelpers.OutputStorageFromParam(data.mesThumbnails));

                job.Submit();
                log.Info("Job Submitted");

                id++;
                ManifestHelpers.UpdateLastEndTime(table, starttime + duration, programid, id, program.State);

                log.Info($"Output MES index {OutputMES}");

                // Let store some data in altid of subclipped asset
                var sid = JobHelpers.ReturnId(job, OutputMES);
                log.Info($"SID {sid}");
                var subclipassetrefreshed = _context.Assets.Where(a => a.Id == sid).FirstOrDefault();
                log.Info($"subclipassetrefreshed ID {subclipassetrefreshed.Id}");
                subclipassetrefreshed.AlternateId = JsonConvert.SerializeObject(new ManifestHelpers.SubclipInfo() { programId = programid, subclipStart = starttime, subclipDuration = duration });
                subclipassetrefreshed.Update();

                // Let store some data in altid of index assets
                var index1sid = JobHelpers.ReturnId(job, OutputIndex1);
                if (index1sid != null)
                {
                    var index1assetrefreshed = _context.Assets.Where(a => a.Id == index1sid).FirstOrDefault();
                    log.Info($"index1assetrefreshed ID {index1assetrefreshed.Id}");
                    index1assetrefreshed.AlternateId = JsonConvert.SerializeObject(new ManifestHelpers.SubclipInfo() { programId = programid, subclipStart = starttime, subclipDuration = duration });
                    index1assetrefreshed.Update();
                }

                // Get program URL
                var publishurlsmooth = MediaServicesHelper.GetValidOnDemandURI(_context, asset);

                if (publishurlsmooth != null)
                {
                    programUrl = publishurlsmooth.ToString();
                }

                NumberJobsQueue = _context.Jobs.Where(j => j.State == JobState.Queued).Count();

            }
            catch (Exception ex)
            {
                string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                log.Info($"ERROR: Exception {message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
            }

            log.Info("Job Id: " + job.Id);
            log.Info("Output asset Id: " + ((OutputMES > -1) ? JobHelpers.ReturnId(job, OutputMES) : JobHelpers.ReturnId(job, OutputPremium)));

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                triggerStart = triggerStart,
                jobId = job.Id,
                subclip = new
                {
                    assetId = JobHelpers.ReturnId(job, OutputMES),
                    taskId = JobHelpers.ReturnTaskId(job, OutputMES),
                    start = starttime,
                    duration = duration,
                },
                mesThumbnails = new
                {
                    assetId = JobHelpers.ReturnId(job, OutputMesThumbnails),
                    taskId = JobHelpers.ReturnTaskId(job, OutputMesThumbnails)
                },
                indexV1 = new
                {
                    assetId = JobHelpers.ReturnId(job, OutputIndex1),
                    taskId = JobHelpers.ReturnTaskId(job, OutputIndex1),
                    language = (string)data.indexV1Language
                },
                channelName = channelName,
                programName = programName,
                programId = programid,
                programUrl = programUrl,
                programState = programState,
                programStateChanged = (lastProgramState != programState).ToString(),
                otherJobsQueue = NumberJobsQueue
            });
        }
    }
}