/*

Azure Media Services REST API v2 Function
 
This function returns ffprobe results from a url to a media file.

Input:
{
    "mediaUrl" : "http://fileserver.net/folder/subfolder/file.mpeg" // Mandatory, url to media file
 }

Output:
{
    "isSuccessful" : "", // boolean indicating if the ffprobe operation succeeded or not
    "probeResult" : "",  // json document with the ffprobe result
    "errorText" : ""     // error text from either the Function or ffprobe execution
 }
*/


using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace media_functions_for_logic_app.functions
{
    public static class return_ffprobe_info
    {
        [FunctionName("return-ffprobe-info")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log, ExecutionContext context)
        {
            {
                log.Info($"Webhook was triggered!");

                // Init variables
                string mediaUrl = string.Empty;
                string output = string.Empty;
                bool isSuccessful = true;
                dynamic probeResult = new JObject();
                string errorText = string.Empty;
                int exitCode = 0;

                string jsonContent = await req.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(jsonContent);

                log.Info(jsonContent);

                // Offset value ?
                if (data.mediaUrl != null) // let's store the offset
                {
                    mediaUrl = (string)data.mediaUrl;
                }

                try
                {
                    var folder = context.FunctionDirectory;

                    var file = System.IO.Path.Combine(folder, "..\\SharedLibs\\ffprobe.exe");

                    System.Diagnostics.Process process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = file;

                    process.StartInfo.Arguments = " -v quiet -of json -show_format -show_streams -i \"" + mediaUrl + "\"";

                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.Start();
                    output = process.StandardOutput.ReadToEnd();
                    errorText = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    exitCode = process.ExitCode;
                    probeResult = JObject.Parse(output);
                }
                catch (Exception e)
                {
                    isSuccessful = false;
                    errorText += e.Message;
                }

                if (exitCode != 0)
                {
                    isSuccessful = false;
                }

                return req.CreateResponse(HttpStatusCode.OK, new
                {
                    isSuccessful,
                    probeResult,
                    errorText
                });
            }
        }
    }
}
