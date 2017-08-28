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
private static readonly string _assetNamePrefix = Environment.GetEnvironmentVariable("AssetNamePrefix");


public static void Run(TimerInfo myTimer, TraceWriter log)
{
    log.Info($"Function started at: {DateTime.Now}");

    string dateTomorrow = DateTime.UtcNow.AddDays(1).ToString("yyyyMMdd");

    string programName = _programNamePrefix + dateTomorrow;

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
            string assetName = _assetNamePrefix + channelName + "-" + dateTomorrow;

            try
            {
                CreateProgram(channel, assetName, programName);
            }
            catch { }
        }
    }

    log.Info($"Function ended at: {DateTime.Now}");
}

private static void CreateProgram(IChannel channel, string assetName, string programName)
{
    IAsset asset = _context.Assets.Create(assetName, AssetCreationOptions.None);

    try
    {
        IProgram program = channel.Programs.Create(programName, TimeSpan.FromHours(25), asset.Id);

        program.Start();
    }
    catch { }

    CreateLocatorForAsset(asset, TimeSpan.FromDays(3650));
}

private static ILocator CreateLocatorForAsset(IAsset asset, TimeSpan ArchiveWindowLength)
{
    var locator = _context.Locators.CreateLocator
        (
            LocatorType.OnDemandOrigin,
            asset,
            _context.AccessPolicies.Create
            (
                "Live Stream Policy",
                ArchiveWindowLength,
                AccessPermissions.Read
            )
        );

    return locator;
}