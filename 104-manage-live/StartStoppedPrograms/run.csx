using System;
using Microsoft.WindowsAzure.MediaServices.Client;

private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");

private static readonly string _channelNames = Environment.GetEnvironmentVariable("ChannelNames");
private static readonly string _programNamePrefix = Environment.GetEnvironmentVariable("ProgramNamePrefix");
private static readonly string _assetNamePrefix = Environment.GetEnvironmentVariable("AssetNamePrefix");

private static CloudMediaContext _context = null;

public static void Run(TimerInfo myTimer, TraceWriter log)
{
    log.Info($"Function started at: {DateTime.Now}");    

    _context = new CloudMediaContext(new MediaServicesCredentials(_mediaServicesAccountName, _mediaServicesAccountKey));

    foreach (string channelName in _channelNames.Split(';'))
    {
        var channel = _context.Channels.Where(c => c.Name.Equals(channelName)).FirstOrDefault();

        if (channel != null)
        {
            var programs = channel.Programs.ToList();

            foreach (var program in programs)
            {
                if (program.State.Equals(ProgramState.Stopped))
                {
                    try
                    {
                        program.Start();
                    }
                    catch (Exception e)
                    {
                        log.Error(e.Message);
                    }
                }
            }
        }
    }

    log.Info($"Function ended at: {DateTime.Now}");
}