/*
 
This function adds time offset to video indexer insights.

Input:
{
    "insights" : [], // Mandatory, video indexer json
    "timeOffset" :"00:01:00", // offset to add (used for live analytics)
 }

Output:
{
    "jsonOffset : ""  // the full json document with offset
 }
*/


using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using System.Xml.Linq;
using Microsoft.Azure.WebJobs.Host;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace media_functions_for_logic_app
{
    public static class add_offset_to_insights
    {
        [FunctionName("add-offset-to-insights")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            {
                log.Info($"Webhook was triggered!");

                string jsonContent = await req.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(jsonContent);

                // Init variables
                dynamic dataJson;// = data.insights;
                string timeOffset = data.timeOffset;

                string[] stringTimes = new string[] { "\"adjustedStart\": ", "\"adjustedEnd\": ", "\"startTime\": ", "\"endTime\": ", "\"start\": ", "\"end\": " };

                if (data.insights == null || data.timeOffset == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = "Please pass the json insights and time offset in the input object"
                    });
                }

                try
                {
                    dataJson = data.insights;
                    var tsoffset = TimeSpan.Parse((string)timeOffset);

                    StringBuilder sb = new StringBuilder();

                    //dynamic dataJson = JsonConvert.DeserializeObject(jsonInsights);
                    var dindent = JsonConvert.SerializeObject(dataJson, Formatting.Indented);
                    var lines = Regex.Split(dindent, "\r\n|\r|\n");

                    foreach (var line in lines)
                    {
                        string lineCopy = line;
                        foreach (var s in stringTimes) // for each time properties
                        {
                            if (line.IndexOf(s) >= 0)
                            {
                                int pos = line.IndexOf(s) + s.Length + 1;
                                int pos2 = line.IndexOf('"', pos);
                                TimeSpan timeToProcess = TimeSpan.Parse(line.Substring(pos, pos2 - pos));
                                lineCopy = line.Substring(0, pos) + (timeToProcess + tsoffset).ToString(@"d\.hh\:mm\:ss\.fff") + line.Substring(pos2);
                                break;
                            }
                        }
                        sb.AppendLine(lineCopy);
                    }

                    dataJson = JsonConvert.DeserializeObject(sb.ToString());
                }
                catch (Exception ex)
                {
                    log.Info($"Exception {ex}");
                    return req.CreateResponse(HttpStatusCode.InternalServerError, new
                    {
                        Error = ex.ToString()
                    });
                }

                log.Info($"");
                return req.CreateResponse(HttpStatusCode.OK, new
                {
                    jsonOffset = dataJson
                });
            }
        }
    }
}
