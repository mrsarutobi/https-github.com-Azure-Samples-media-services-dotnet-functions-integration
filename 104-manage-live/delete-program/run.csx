using System;
using Microsoft.WindowsAzure.MediaServices.Client;

private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");

private static readonly string _channelNames = Environment.GetEnvironmentVariable("ChannelNames");
private static readonly string _programNamePrefix = Environment.GetEnvironmentVariable("ProgramNamePrefix");

private static CloudMediaContext _context = null;

public static void Run(TimerInfo myTimer, TraceWriter log)
{
    log.Info($"Function started at: {DateTime.Now}");    

    string dateToday = DateTime.UtcNow.AddDays(-1).ToString("yyyyMMdd");

    string programName = _programNamePrefix + dateToday;

    _context = new CloudMediaContext(new MediaServicesCredentials(_mediaServicesAccountName, _mediaServicesAccountKey));

    foreach (string channelName in _channelNames.Split(';'))
    {
        var channel = _context.Channels.Where(c => c.Name.Equals(channelName)).FirstOrDefault();

        if (channel != null)
        {
            try
            {
                DeleteProgram(channel, programName);
            }
            catch {}
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
    catch {}

    try
    {
        program.Delete();
    }
    catch {}
}