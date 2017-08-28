#r "Newtonsoft.Json"
#r "System.Web"

#load "../Shared/mediaServicesHelpers.csx"

using System;
using System.Net;
using System.Web;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

// Read values from the App.config file.
static string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
static string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");

static readonly string _AADTenantDomain = Environment.GetEnvironmentVariable("AMSAADTenantDomain");
static readonly string _RESTAPIEndpoint = Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint");

static readonly string _mediaservicesClientId = Environment.GetEnvironmentVariable("AMSClientId");
static readonly string _mediaservicesClientSecret = Environment.GetEnvironmentVariable("AMSClientSecret");

// Field for service context.
private static CloudMediaContext _context = null;
private static CloudStorageAccount _destinationStorageAccount = null;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");
    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);
    log.Info("Request : " + jsonContent);

    // Validate input objects
    int delay = 15000;
    if (data.JobId == null)
        return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass JobId in the input object" });
    if (data.Delay != null)
        delay = data.Delay;
    log.Info("Input - Job Id : " + data.JobId);
    //log.Info("delay : " + delay);

    log.Info($"Wait " + delay + "(ms)");
    System.Threading.Thread.Sleep(delay);

    IJob job = null;
    try
    {
        // Load AMS account context
        log.Info($"Using Azure Media Service Rest API Endpoint : {_RESTAPIEndpoint}");

        AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(_AADTenantDomain,
                                  new AzureAdClientSymmetricKey(_mediaservicesClientId, _mediaservicesClientSecret),
                                  AzureEnvironments.AzureCloudEnvironment);

        AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

        _context = new CloudMediaContext(new Uri(_RESTAPIEndpoint), tokenProvider);

        // Get the job
        string jobid = (string)data.JobId;
        job = _context.Jobs.Where(j => j.Id == jobid).FirstOrDefault();
        if (job == null)
        {
            log.Info("Job not found : " + jobid);
            return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Job not found" });
        }
    }
    catch (Exception ex)
    {
        log.Info("Exception " + ex);
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    // IJob.State
    // - Queued = 0
    // - Scheduled = 1
    // - Processing = 2
    // - Finished = 3
    // - Error = 4
    // - Canceled = 5
    // - Canceling = 6
    log.Info($"Job {job.Id} status is {job.State}");

    return req.CreateResponse(HttpStatusCode.OK, new
    {
        JobId = job.Id,
        JobState = job.State
    });
}
