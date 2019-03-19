/*

Azure Media Services REST API v2 Function
 
This function checks a job status.

Input:
{
    "jobId" : "nb:jid:UUID:1ceaa82f-2607-4df9-b034-cd730dad7097", // Mandatory, Id of the source asset
    "extendedInfo" : true, // optional. Returns ams account unit size, nb units, nb of jobs in queue, scheduled and running states. Only if job is complete or error
    "noWait" : true // optional. Set this parameter if you don't want the function to wait. Otherwise it waits for 15 seconds if the job is not completed before doing another check and terminate
 }

Output:
{
    "jobState" : 2,             // The state of the job (int)
    "isRunning" : "False",      // True if job is running
    "isSuccessful" : "True",    // True is job is a success. Only valid if IsRunning = False
    "errorText" : ""            // error(s) text if job state is error
    "startTime" :""
    "endTime" : "",
    "runningDuration" : "",
    "progress" : 20.3           // overall progress, between 0 and 100
    "extendedInfo" :            // if extendedInfo is true and job is finished or in error
    {
        "mediaUnitNumber" = 2,
        "mediaUnitSize" = "S2",
        "otherJobsProcessing" = 2,
        "otherJobsScheduled" = 1,
        "otherJobsQueue" = 1,
        "amsRESTAPIEndpoint" = "http:/...."
    }
 }
*/

using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace media_functions_for_logic_app
{
    public static class check_job_status
    {

        // Field for service context.
        private static CloudMediaContext _context = null;
        private static CloudStorageAccount _destinationStorageAccount = null;


        [FunctionName("check-job-status")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)



        //public static async Task<object> Run(HttpRequestMessage req, TraceWriter log, Microsoft.Azure.WebJobs.ExecutionContext execContext)
        {
            log.Info($"Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            log.Info(jsonContent);

            if (data.jobId == null)
            {
                // used to test the function
                //data.jobId = "nb:jid:UUID:acf38b8a-aef9-4789-9f0f-f69bf6ccb8e5";
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass the job ID in the input object (JobId)"
                });
            }
            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            log.Info($"Using Azure Media Service Rest API Endpoint : {amsCredentials.AmsRestApiEndpoint}");

            IJob job = null;
            string startTime = "";
            string endTime = "";
            StringBuilder sberror = new StringBuilder();
            string runningDuration = "";
            bool isRunning = true;
            bool isSuccessful = true;
            bool extendedInfo = false;

            if (data.extendedInfo != null && ((bool)data.extendedInfo) == true)
            {
                extendedInfo = true;
            }

            try
            {
                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                                      new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                                      AzureEnvironments.AzureCloudEnvironment);

                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);

                // Get the job
                string jobid = (string)data.jobId;
                job = _context.Jobs.Where(j => j.Id == jobid).FirstOrDefault();

                if (job == null)
                {
                    log.Info($"Job not found {jobid}");

                    return req.CreateResponse(HttpStatusCode.InternalServerError, new
                    {
                        error = "Job not found"
                    });
                }

                if (data.noWait != null && (bool)data.noWait)
                {
                    // No wait
                }
                else
                {
                    for (int i = 1; i <= 3; i++) // let's wait 3 times 5 seconds (15 seconds)
                    {
                        log.Info($"Job {job.Id} status is {job.State}");

                        if (job.State == JobState.Finished || job.State == JobState.Canceled || job.State == JobState.Error)
                        {
                            break;
                        }

                        log.Info("Waiting 5 s...");
                        System.Threading.Thread.Sleep(5 * 1000);
                        job = _context.Jobs.Where(j => j.Id == job.Id).FirstOrDefault();
                    }
                }


                if (job.State == JobState.Error || job.State == JobState.Canceled)
                {
                    foreach (var taskenum in job.Tasks)
                    {
                        foreach (var details in taskenum.ErrorDetails)
                        {
                            sberror.AppendLine(taskenum.Name + " : " + details.Message);
                        }
                    }
                }

                if (job.StartTime != null) startTime = ((DateTime)job.StartTime).ToString("o");

                if (job.EndTime != null) endTime = ((DateTime)job.EndTime).ToString("o");

                if (job.RunningDuration != null) runningDuration = job.RunningDuration.ToString();

            }
            catch (Exception ex)
            {
                string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                log.Info($"ERROR: Exception {message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
            }

            isRunning = !(job.State == JobState.Finished || job.State == JobState.Canceled || job.State == JobState.Error);
            isSuccessful = (job.State == JobState.Finished);

            log.Info("isSuccessful " + isSuccessful.ToString());

            if (extendedInfo && (job.State == JobState.Finished || job.State == JobState.Canceled || job.State == JobState.Error))
            {
                dynamic stats = new JObject();
                stats.mediaUnitNumber = _context.EncodingReservedUnits.FirstOrDefault().CurrentReservedUnits;
                stats.mediaUnitSize = JobHelpers.ReturnMediaReservedUnitName(_context.EncodingReservedUnits.FirstOrDefault().ReservedUnitType); ;
                stats.otherJobsProcessing = _context.Jobs.Where(j => j.State == JobState.Processing).Count();
                stats.otherJobsScheduled = _context.Jobs.Where(j => j.State == JobState.Scheduled).Count();
                stats.otherJobsQueue = _context.Jobs.Where(j => j.State == JobState.Queued).Count();
                stats.amsRESTAPIEndpoint = amsCredentials.AmsRestApiEndpoint;

                return req.CreateResponse(HttpStatusCode.OK, new
                {
                    jobState = job.State,
                    errorText = sberror.ToString(),
                    startTime = startTime,
                    endTime = endTime,
                    runningDuration = runningDuration,
                    extendedInfo = stats.ToString(),
                    isRunning = isRunning.ToString(),
                    isSuccessful = isSuccessful.ToString(),
                    progress = job.GetOverallProgress()
                });
            }
            else
            {
                return req.CreateResponse(HttpStatusCode.OK, new
                {
                    jobState = job.State,
                    errorText = sberror.ToString(),
                    startTime = startTime,
                    endTime = endTime,
                    runningDuration = runningDuration,
                    isRunning = isRunning.ToString(),
                    isSuccessful = isSuccessful.ToString(),
                    progress = job.GetOverallProgress()
                });
            }
        }

     
    }
}



