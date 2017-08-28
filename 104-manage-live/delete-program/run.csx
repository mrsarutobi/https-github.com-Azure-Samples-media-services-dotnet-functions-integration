using System;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

static readonly string _AADTenantDomain = Environment.GetEnvironmentVariable("AMSAADTenantDomain");
static readonly string _RESTAPIEndpoint = Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint");

static readonly string _mediaservicesClientId = Environment.GetEnvironmentVariable("AMSClientId");
static readonly string _mediaservicesClientSecret = Environment.GetEnvironmentVariable("AMSClientSecret");

// Field for service context.
private static CloudMediaContext _context = null;

private static readonly string _channelNames = Environment.GetEnvironmentVariable("ChannelNames");
private static readonly string _programNamePrefix = Environment.GetEnvironmentVariable("ProgramNamePrefix");


public static void Run(TimerInfo myTimer, TraceWriter log)
{
    log.Info($"Function started at: {DateTime.Now}");

    string dateToday = DateTime.UtcNow.AddDays(-1).ToString("yyyyMMdd");

    string programName = _programNamePrefix + dateToday;

    AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(_AADTenantDomain,
                              new AzureAdClientSymmetricKey(_mediaservicesClientId, _mediaservicesClientSecret),
                              AzureEnvironments.AzureCloudEnvironment);

    AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

    _context = new CloudMediaContext(new Uri(_RESTAPIEndpoint), tokenProvider);

    foreach (string channelName in _channelNames.Split(';'))
    {
        var channel = _context.Channels.Where(c => c.Name.Equals(channelName)).FirstOrDefault();

        if (channel != null)
        {
            try
            {
                DeleteProgram(channel, programName);
            }
            catch { }
        }
    }

    log.Info($"Function ended at: {DateTime.Now}");
}

private static void DeleteProgram(IChannel channel, string programName)
{
    var program = channel.Programs.Where(p => p.Name.Equals(programName)).FirstOrDefault();

    try
    {
        program.Stop();
    }
    catch { }

    try
    {
        program.Delete();
    }
    catch { }
}