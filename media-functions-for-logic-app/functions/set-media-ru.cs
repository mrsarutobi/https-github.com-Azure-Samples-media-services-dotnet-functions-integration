/*

Azure Media Services REST API v2 Function

This function sets the number and speed of media reserved units in the account.

Input:
{
    "ruCount" : "+1", // can be a number like "1", or a number with + or - to increase or decrease the number. Example :  "+2" or "-3"
    "ruSpeed" : "S1"  // can be "S1", "S2" or "S3"
    "extendedInfo" : true
}

Output:
{
    "success" : "True", // return if operation is a success or not
    "maxRu" : 10,       // number of max units
    "newRuCount" : 3,   // new count of units
    "newRuSpeed" : "S2", // new speed of units
    "jobsProcessing" = 2, // if "extendedInfo" : true in input
    "jobsScheduled" = 1, // if "extendedInfo" : true in input
    "jobsQueue" = 1 // if "extendedInfo" : true in input
}

*/


using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace media_functions_for_logic_app
{
    public static class set_media_ru
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("set-media-ru")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            log.Info(jsonContent);

            int targetNbRU = -1;
            int? nbunits = null;
            bool relative = false;
            string RUspeed = "";
            ReservedUnitType? type = null;

            if (data.ruSpeed != null)
            {
                RUspeed = ((string)data.ruSpeed).ToUpper();
                if (RUspeed == "S1")
                {
                    type = ReservedUnitType.Basic;
                }
                else if (RUspeed == "S2")
                {
                    type = ReservedUnitType.Standard;
                }
                else if (RUspeed == "S3")
                {
                    type = ReservedUnitType.Premium;
                }
                else
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = "Error parsing ruSpeed"
                    });
                }
            }

            if (data.ruCount != null)
            {
                string RUcount = (string)data.ruCount;
                if (RUcount[0] == '+' || RUcount[0] == '-')
                {
                    relative = true;
                    try
                    {
                        nbunits = int.Parse(RUcount);
                    }
                    catch
                    {
                        return req.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            error = "Error (1) parsing ruCount"
                        });
                    }
                }
                else
                {
                    try
                    {
                        nbunits = int.Parse(RUcount);
                    }
                    catch
                    {
                        return req.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            error = "Error (2) parsing ruCount"
                        });
                    }
                }
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
            }
            catch (Exception ex)
            {
                string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                log.Info($"ERROR: Exception {message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
            }

            IEncodingReservedUnit EncResUnit = _context.EncodingReservedUnits.FirstOrDefault();
            targetNbRU = EncResUnit.CurrentReservedUnits;
            ReservedUnitType targetType = EncResUnit.ReservedUnitType;

            log.Info("Current type of media RU: " + MediaServicesHelper.ReturnNewRUName(EncResUnit.ReservedUnitType));
            log.Info("Current count of media RU: " + EncResUnit.CurrentReservedUnits);
            log.Info("Maximum reservable media RUs: " + EncResUnit.MaxReservableUnits);

            if (nbunits != null)
            {
                if (relative)
                {
                    if (((int)nbunits) > 0)
                    {
                        log.Info($"Adding {nbunits} unit(s)");
                    }
                    else
                    {
                        log.Info($"Removing {nbunits} unit(s)");
                    }
                    targetNbRU = Math.Max(targetNbRU + (int)nbunits, 0);
                }
                else
                {
                    log.Info($"Changing to {nbunits} unit(s)");
                    targetNbRU = (int)nbunits;
                }
            }

            if (type != null)
            {
                string sru = MediaServicesHelper.ReturnNewRUName((ReservedUnitType)type);
                log.Info($"Changing to {sru} speed");
                targetType = (ReservedUnitType)type;
            }

            if (targetNbRU == 0 && targetType != ReservedUnitType.Basic)
            {
                targetType = ReservedUnitType.Basic; // 0 units so we switch to S1
            }

            bool Error = false;
            try
            {
                EncResUnit.CurrentReservedUnits = targetNbRU;
                EncResUnit.ReservedUnitType = targetType;
                EncResUnit.Update();
                EncResUnit = _context.EncodingReservedUnits.FirstOrDefault(); // Refresh
            }
            catch (Exception ex)
            {
                Error = true;
            }

            log.Info("Media RU unit(s) updated successfully.");
            log.Info("New current speed of media RU  : " + MediaServicesHelper.ReturnNewRUName(EncResUnit.ReservedUnitType));
            log.Info("New current count of media RU : " + EncResUnit.CurrentReservedUnits);

            if (data.extentedInfo != null && (bool)data.extentedInfo)
            {
                return req.CreateResponse(HttpStatusCode.OK, new
                {
                    success = (!Error).ToString(),
                    maxRu = EncResUnit.MaxReservableUnits,
                    newRuCount = EncResUnit.CurrentReservedUnits,
                    newRuSpeed = MediaServicesHelper.ReturnNewRUName(EncResUnit.ReservedUnitType),
                    jobsProcessing = _context.Jobs.Where(j => j.State == JobState.Processing).Count(),
                    jobsScheduled = _context.Jobs.Where(j => j.State == JobState.Scheduled).Count(),
                    jobsQueue = _context.Jobs.Where(j => j.State == JobState.Queued).Count()
                });
            }
            else

                return req.CreateResponse(HttpStatusCode.OK, new
                {
                    success = (!Error).ToString(),
                    maxRu = EncResUnit.MaxReservableUnits,
                    newRuCount = EncResUnit.CurrentReservedUnits,
                    newRuSpeed = MediaServicesHelper.ReturnNewRUName(EncResUnit.ReservedUnitType)
                });
        }
    }
}