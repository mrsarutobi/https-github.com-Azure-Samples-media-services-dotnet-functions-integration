/*

Azure Media Services REST API v2 Function
 
This function submits a job wth encoding and/or analytics.

Input:
{
    "assetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Mandatory, Id of the source asset
    "mes" :                 // Optional but required to encode with Media Encoder Standard (MES)
    {
        "preset" : "Content Adaptive Multiple Bitrate MP4", // Optional but required to encode with Media Encoder Standard (MES). If MESPreset contains an extension "H264 Multiple Bitrate 720p with thumbnail.json" then it loads this file from ..\Presets
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    }
    "mesThumbnails" :      // Optional but required to generate thumbnails with Media Encoder Standard (MES)
    {
        "start" : "{Best}",  // Optional. Start time/mode. Default is "{Best}"
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    }
    "mepw" :                // Optional but required to encode with Premium Workflow Encoder
    {
        "workflowAssetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Required. Id for the workflow asset
        "workflowConfig"  : "",                                                  // Optional. Premium Workflow Config for the task
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "indexV1" :             // Optional but required to index audio with Media Indexer v1
    {
        "language" : "English", // Optional. Default is "English"
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "indexV2" :             // Optional but required to index audio with Media Indexer v2
    {
        "language" : "EnUs", // Optional. Default is EnUs
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "ocr" :             // Optional but required to do OCR
    {
        "language" : "AutoDetect", // Optional (Autodetect is the default)
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "faceDetection" :             // Optional but required to do Face Detection
    {
        "mode" : "PerFaceEmotion", // Optional (PerFaceEmotion is the default)
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "faceRedaction" :             // Optional but required to do Face Redaction
    {
        "mode" : "analyze"                  // Optional (analyze is the default)
        "outputStorage" : "amsstorage01"    // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
     "motionDetection" :             // Optional but required to do Motion Detection
    {
        "level" : "medium",                 // Optional (medium is the default)
        "outputStorage" : "amsstorage01"    // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
     "summarization" :                      // Optional but required to do Motion Detection
    {
        "duration" : "0.0",                 // Optional (0.0 is the default)
        "outputStorage" : "amsstorage01"    // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
     "videoAnnotation" :             // Optional but required to do Video Annotator
    {
        "outputStorage" : "amsstorage01"    // Optional. Storage account name where to put the output asset (attached to AMS account)
    },

    // General job properties
    "priority" : 10,                            // Optional, priority of the job
    "useEncoderOutputForAnalytics" : true,      // Optional, use generated asset by MES or Premium Workflow as a source for media analytics
    "jobName" : ""                              // Optional, job name  

    // For compatibility only with old workflows. Do not use anymore!
    "mesPreset" : "Adaptive Streaming",         // Optional but required to encode with Media Encoder Standard (MES). If MESPreset contains an extension "H264 Multiple Bitrate 720p with thumbnail.json" then it loads this file from ..\Presets
    "workflowAssetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Optional, but required to encode the asset with Premium Workflow Encoder. Id for the workflow asset
    "workflowConfig"  : ""                      // Optional. Premium Workflow Config for the task
    "indexV1Language" : "English",              // Optional but required to index the asset with Indexer v1
    "indexV2Language" : "EnUs",                 // Optional but required to index the asset with Indexer v2
    "ocrLanguage" : "AutoDetect" or "English",  // Optional but required to do OCR
    "faceDetectionMode" : "PerFaceEmotion,      // Optional but required to trigger face detection
    "faceRedactionMode" : "analyze",            // Optional, but required for face redaction
    "motionDetectionLevel" : "medium",          // Optional, required for motion detection
    "summarizationDuration" : "0.0"             // Optional. Required to create video summarization. "0.0" for automatic
}

Output:
{
    "jobId" :  // job id
    "otherJobsQueue" = 3 // number of jobs in the queue
    "mes" : // Output asset generated by MES (if mesPreset was specified)
        {
            assetId : "",
            taskId : ""
        },
    "mesThumbnails" :// Output asset generated by MES
        {
            assetId : "",
            taskId : ""
        },
    "mepw" : // Output asset generated by Premium Workflow Encoder
        {
            assetId : "",
            taskId : ""
        },
    "indexV1" :  // Output asset generated by Indexer v1
        {
            assetId : "",
            taskId : "",
            language : ""
        },
    "indexV2" : // Output asset generated by Indexer v2
        {
            assetId : "",
            taskId : "",
            language : ""
        },
    "ocr" : // Output asset generated by OCR
        {
            assetId : "",
            taskId : ""
        },
    "faceDetection" : // Output asset generated by Face detection
        {
            assetId : ""
            taskId : ""
        },
    "faceRedaction" : // Output asset generated by Face redaction
        {
            assetId : ""
            taskId : ""
        },
     "motionDetection" : // Output asset generated by motion detection
        {
            assetId : "",
            taskId : ""
        },
     "summarization" : // Output asset generated by video summarization
        {
            assetId : "",
            taskId : ""
        },
     "videoAnnotation" :// Output asset generated by Video Annotator
        {
            assetId : "",
            taskId : ""
        }
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
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace media_functions_for_logic_app
{
    public static class submit_job
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("submit-job")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log, Microsoft.Azure.WebJobs.ExecutionContext execContext)
        {
            int taskindex = 0;
            bool useEncoderOutputForAnalytics = false;
            IAsset outputEncoding = null;

            log.Info($"Webhook was triggered!");
            string triggerStart = DateTime.UtcNow.ToString("o");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            log.Info(jsonContent);

            log.Info($"asset id : {data.assetId}");

            if (data.assetId == null)
            {
                // for test
                // data.assetId = "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc";

                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass asset ID in the input object (assetId)"
                });
            }


            IJob job = null;
            ITask taskEncoding = null;

            int OutputMES = -1;
            int OutputMEPW = -1;
            int OutputIndex1 = -1;
            int OutputIndex2 = -1;
            int OutputOCR = -1;
            int OutputFaceDetection = -1;
            int OutputMotion = -1;
            int OutputSummarization = -1;
            int OutputFaceRedaction = -1;
            int OutputMesThumbnails = -1;
            int OutputVideoAnnotation = -1;
            int NumberJobsQueue = 0;

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            log.Info($"Using Azure Media Service Rest API Endpoint : {amsCredentials.AmsRestApiEndpoint}");

            try
            {
                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                                                new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                                                AzureEnvironments.AzureCloudEnvironment);

                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);


                // find the Asset
                string assetid = (string)data.assetId;
                IAsset asset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

                if (asset == null)
                {
                    log.Info($"Asset not found {assetid}");
                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = "Asset not found"
                    });
                }

                if (data.useEncoderOutputForAnalytics != null && ((bool)data.useEncoderOutputForAnalytics) && (data.mesPreset != null || data.mes != null))  // User wants to use encoder output for media analytics
                {
                    useEncoderOutputForAnalytics = (bool)data.useEncoderOutputForAnalytics;
                }


                // Declare a new encoding job with the Standard encoder
                int priority = 10;
                if (data.priority != null)
                {
                    priority = (int)data.priority;
                }
                job = _context.Jobs.Create(((string)data.jobName) ?? "Azure Functions Job", priority);

                if (data.mes != null || data.mesPreset != null)  // MES Task
                {
                    // Get a media processor reference, and pass to it the name of the 
                    // processor to use for the specific task.
                    IMediaProcessor processorMES = MediaServicesHelper.GetLatestMediaProcessorByName(_context, "Media Encoder Standard");

                    string preset = null;
                    if (data.mes != null)
                    {
                        preset = (string)data.mes.preset;
                    }
                    else
                    {
                        preset = (string)data.mesPreset; // Compatibility mode
                    }
                    if (preset == null)
                    {
                        preset = "Content Adaptive Multiple Bitrate MP4";  // the default preset
                    }

                    if (preset.ToUpper().EndsWith(".JSON"))
                    {
                        // Build the folder path to the preset
                        string presetPath = Path.Combine(System.IO.Directory.GetParent(execContext.FunctionDirectory).FullName, "presets", preset);
                        log.Info("presetPath= " + presetPath);
                        preset = File.ReadAllText(presetPath);
                    }

                    // Create a task with the encoding details, using a string preset.
                    // In this case "H264 Multiple Bitrate 720p" system defined preset is used.
                    taskEncoding = job.Tasks.AddNew("MES encoding task",
                       processorMES,
                       preset,
                       TaskOptions.None);

                    // Specify the input asset to be encoded.
                    taskEncoding.InputAssets.Add(asset);
                    OutputMES = taskindex++;

                    // Add an output asset to contain the results of the job. 
                    // This output is specified as AssetCreationOptions.None, which 
                    // means the output asset is not encrypted. 
                    outputEncoding = taskEncoding.OutputAssets.AddNew(asset.Name + " MES encoded", JobHelpers.OutputStorageFromParam(data.mes), AssetCreationOptions.None);
                }

                if (data.mepw != null || data.workflowAssetId != null) // Premium Encoder Task
                {

                    //find the workflow asset
                    string workflowassetid = null;
                    if (data.mepw != null)
                    {
                        workflowassetid = (string)data.mepw.workflowAssetId;
                    }
                    else
                    {
                        workflowassetid = (string)data.workflowAssetId; // compatibility mode
                    }

                    IAsset workflowAsset = _context.Assets.Where(a => a.Id == workflowassetid).FirstOrDefault();

                    if (workflowAsset == null)
                    {
                        log.Info($"Workflow not found {workflowassetid}");
                        return req.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            error = "Workflow not found"
                        });
                    }

                    // Get a media processor reference, and pass to it the name of the 
                    // processor to use for the specific task.
                    IMediaProcessor processorMEPW = MediaServicesHelper.GetLatestMediaProcessorByName(_context, "Media Encoder Premium Workflow");

                    string premiumConfiguration = "";
                    if (data.mepw != null && data.mepw.workflowConfig != null)
                    {
                        premiumConfiguration = (string)data.mepw.workflowConfig;
                    }
                    else if (data.workflowConfig != null)
                    {
                        premiumConfiguration = (string)data.workflowConfig; // compatibility mode
                    }

                    // In some cases, a configuration can be loaded and passed it to the task to tuned the workflow
                    // premiumConfiguration=File.ReadAllText(Path.Combine(System.IO.Directory.GetParent(execContext.FunctionDirectory).FullName, "presets", "SetRuntime.xml")).Replace("VideoFileName", VideoFile.Name).Replace("AudioFileName", AudioFile.Name);

                    // Create a task
                    taskEncoding = job.Tasks.AddNew("Premium Workflow encoding task",
                       processorMEPW,
                       premiumConfiguration,
                       TaskOptions.None);

                    log.Info("task created");

                    // Specify the input asset to be encoded.
                    taskEncoding.InputAssets.Add(workflowAsset); // first add the Workflow
                    taskEncoding.InputAssets.Add(asset); // Then add the video asset
                    OutputMEPW = taskindex++;

                    // Add an output asset to contain the results of the job. 
                    // This output is specified as AssetCreationOptions.None, which 
                    // means the output asset is not encrypted. 
                    outputEncoding = taskEncoding.OutputAssets.AddNew(asset.Name + " Premium encoded", JobHelpers.OutputStorageFromParam(data.mepw), AssetCreationOptions.None);
                }

                IAsset an_asset = useEncoderOutputForAnalytics ? outputEncoding : asset;

                // Media Analytics
                OutputIndex1 = JobHelpers.AddTask(execContext, _context, job, an_asset, (data.indexV1 == null) ? (string)data.indexV1Language : ((string)data.indexV1.language ?? "English"), "Azure Media Indexer", "IndexerV1.xml", "English", ref taskindex, specifiedStorageAccountName: JobHelpers.OutputStorageFromParam(data.indexV1));
                OutputIndex2 = JobHelpers.AddTask(execContext, _context, job, an_asset, (data.indexV2 == null) ? (string)data.indexV2Language : ((string)data.indexV2.language ?? "EnUs"), "Azure Media Indexer 2 Preview", "IndexerV2.json", "EnUs", ref taskindex, specifiedStorageAccountName: JobHelpers.OutputStorageFromParam(data.indexV2));
                OutputOCR = JobHelpers.AddTask(execContext, _context, job, an_asset, (data.ocr == null) ? (string)data.ocrLanguage : ((string)data.ocr.language ?? "AutoDetect"), "Azure Media OCR", "OCR.json", "AutoDetect", ref taskindex, specifiedStorageAccountName: JobHelpers.OutputStorageFromParam(data.ocr));
                OutputFaceDetection = JobHelpers.AddTask(execContext, _context, job, an_asset, (data.faceDetection == null) ? (string)data.faceDetectionMode : ((string)data.faceDetection.mode ?? "PerFaceEmotion"), "Azure Media Face Detector", "FaceDetection.json", "PerFaceEmotion", ref taskindex, specifiedStorageAccountName: JobHelpers.OutputStorageFromParam(data.faceDetection));
                OutputFaceRedaction = JobHelpers.AddTask(execContext, _context, job, an_asset, (data.faceRedaction == null) ? (string)data.faceRedactionMode : ((string)data.faceRedaction.mode ?? "comined"), "Azure Media Redactor", "FaceRedaction.json", "combined", ref taskindex, specifiedStorageAccountName: JobHelpers.OutputStorageFromParam(data.faceRedaction));
                OutputMotion = JobHelpers.AddTask(execContext, _context, job, an_asset, (data.motionDetection == null) ? (string)data.motionDetectionLevel : ((string)data.motionDetection.level ?? "medium"), "Azure Media Motion Detector", "MotionDetection.json", "medium", ref taskindex, specifiedStorageAccountName: JobHelpers.OutputStorageFromParam(data.motionDetection));
                OutputSummarization = JobHelpers.AddTask(execContext, _context, job, an_asset, (data.summarization == null) ? (string)data.summarizationDuration : ((string)data.summarization.duration ?? "0.0"), "Azure Media Video Thumbnails", "Summarization.json", "0.0", ref taskindex, specifiedStorageAccountName: JobHelpers.OutputStorageFromParam(data.summarization));
                OutputVideoAnnotation = JobHelpers.AddTask(execContext, _context, job, an_asset, (data.videoAnnotation != null) ? "1.0" : null, "Azure Media Video Annotator", "VideoAnnotation.json", "1.0", ref taskindex, specifiedStorageAccountName: JobHelpers.OutputStorageFromParam(data.videoAnnotation));

                // MES Thumbnails
                OutputMesThumbnails = JobHelpers.AddTask(execContext, _context, job, asset, (data.mesThumbnails != null) ? ((string)data.mesThumbnails.Start ?? "{Best}") : null, "Media Encoder Standard", "MesThumbnails.json", "{Best}", ref taskindex, specifiedStorageAccountName: JobHelpers.OutputStorageFromParam(data.mesThumbnails));

                job.Submit();
                log.Info("Job Submitted");
                NumberJobsQueue = _context.Jobs.Where(j => j.State == JobState.Queued).Count();
            }
            catch (Exception ex)
            {
                string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                log.Info($"ERROR: Exception {message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
            }

            job = _context.Jobs.Where(j => j.Id == job.Id).FirstOrDefault(); // Let's refresh the job

            log.Info("Job Id: " + job.Id);
            log.Info("OutputAssetMESId: " + JobHelpers.ReturnId(job, OutputMES));
            log.Info("OutputAssetMEPWId: " + JobHelpers.ReturnId(job, OutputMEPW));
            log.Info("OutputAssetIndexV1Id: " + JobHelpers.ReturnId(job, OutputIndex1));
            log.Info("OutputAssetIndexV2Id: " + JobHelpers.ReturnId(job, OutputIndex2));
            log.Info("OutputAssetOCRId: " + JobHelpers.ReturnId(job, OutputOCR));
            log.Info("OutputAssetFaceDetectionId: " + JobHelpers.ReturnId(job, OutputFaceDetection));
            log.Info("OutputAssetFaceRedactionId: " + JobHelpers.ReturnId(job, OutputFaceRedaction));
            log.Info("OutputAssetMotionDetectionId: " + JobHelpers.ReturnId(job, OutputMotion));
            log.Info("OutputAssetSummarizationId: " + JobHelpers.ReturnId(job, OutputSummarization));
            log.Info("OutputMesThumbnailsId: " + JobHelpers.ReturnId(job, OutputMesThumbnails));
            log.Info("OutputAssetVideoAnnotationId: " + JobHelpers.ReturnId(job, OutputVideoAnnotation));

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                jobId = job.Id,
                otherJobsQueue = NumberJobsQueue,
                mes = new
                {
                    assetId = JobHelpers.ReturnId(job, OutputMES),
                    taskId = JobHelpers.ReturnTaskId(job, OutputMES)
                },
                mepw = new
                {
                    assetId = JobHelpers.ReturnId(job, OutputMEPW),
                    taskId = JobHelpers.ReturnTaskId(job, OutputMEPW)
                },
                indexV1 = new
                {
                    assetId = JobHelpers.ReturnId(job, OutputIndex1),
                    taskId = JobHelpers.ReturnTaskId(job, OutputIndex1),
                    language = (string)data.indexV1Language
                },
                indexV2 = new
                {
                    assetId = JobHelpers.ReturnId(job, OutputIndex2),
                    taskId = JobHelpers.ReturnTaskId(job, OutputIndex2),
                    language = (string)data.indexV2Language
                },
                ocr = new
                {
                    assetId = JobHelpers.ReturnId(job, OutputOCR),
                    taskId = JobHelpers.ReturnTaskId(job, OutputOCR)
                },
                faceDetection = new
                {
                    assetId = JobHelpers.ReturnId(job, OutputFaceDetection),
                    taskId = JobHelpers.ReturnTaskId(job, OutputFaceDetection)
                },
                faceRedaction = new
                {
                    assetId = JobHelpers.ReturnId(job, OutputFaceRedaction),
                    taskId = JobHelpers.ReturnTaskId(job, OutputFaceRedaction)
                },
                motionDetection = new
                {
                    assetId = JobHelpers.ReturnId(job, OutputMotion),
                    taskId = JobHelpers.ReturnTaskId(job, OutputMotion)
                },
                summarization = new
                {
                    assetId = JobHelpers.ReturnId(job, OutputSummarization),
                    taskId = JobHelpers.ReturnTaskId(job, OutputSummarization)
                },
                mesThumbnails = new
                {
                    assetId = JobHelpers.ReturnId(job, OutputMesThumbnails),
                    taskId = JobHelpers.ReturnTaskId(job, OutputMesThumbnails)
                },
                videoAnnotation = new
                {
                    assetId = JobHelpers.ReturnId(job, OutputVideoAnnotation),
                    taskId = JobHelpers.ReturnTaskId(job, OutputVideoAnnotation)
                }
            });
        }
    }
}
