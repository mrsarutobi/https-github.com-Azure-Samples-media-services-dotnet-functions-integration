/*

Azure Media Services REST API v2 Function
 
This function returns subtitles from an asset.

Input:
{
    "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Mandatory, Id of the source asset
    "timeOffset" :"00:01:00", // optional, offset to add to subtitles (used for live analytics)
    "deleteAsset" : true // Optional, delete the asset once data has been read from it
 }

Output:
{
    "vttUrl" : "",      // the full path to vtt file if asset is published
    "ttmlUrl" : "",     // the full path to vtt file if asset is published
    "pathUrl" : "",     // the path to the asset if asset is published
    "vttDocument" : "", // the full vtt document,
    "vttDocumentOffset" : "", // the full vtt document with offset
    "ttmlDocument : ""  // the full ttml document
    "ttmlDocumentOffset : ""  // the full ttml document with offset
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


namespace media_functions_for_logic_app
{
    public static class return_subtitles
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("return-subtitles")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            {
                log.Info($"Webhook was triggered!");

                // Init variables
                string vttUrl = "";
                string pathUrl = "";
                string ttmlUrl = "";
                string vttContent = "";
                string ttmlContent = "";
                string ttmlContentTimeCorrected = "";
                string vttContentTimeCorrected = "";

                string jsonContent = await req.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(jsonContent);

                log.Info(jsonContent);

                if (data.assetId == null)
                {
                    // for test
                    // data.assetId = "nb:cid:UUID:d9496372-32f5-430d-a4c6-d21ec3e01525";

                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = "Please pass asset ID in the input object (AssetId)"
                    });
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


                    // Get the asset
                    string assetid = data.assetId;
                    var outputAsset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

                    if (outputAsset == null)
                    {
                        log.Info($"Asset not found {assetid}");
                        return req.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            error = "Asset not found"
                        });
                    }

                    var vttSubtitle = outputAsset.AssetFiles.Where(a => a.Name.ToUpper().EndsWith(".VTT")).FirstOrDefault();
                    var ttmlSubtitle = outputAsset.AssetFiles.Where(a => a.Name.ToUpper().EndsWith(".TTML")).FirstOrDefault();

                    Uri publishurl = MediaServicesHelper.GetValidOnDemandPath(_context, outputAsset);
                    if (publishurl != null)
                    {
                        pathUrl = publishurl.ToString();
                    }
                    else
                    {
                        log.Info($"Asset not published");
                    }

                    if (vttSubtitle != null)
                    {
                        if (publishurl != null)
                        {
                            vttUrl = pathUrl + vttSubtitle.Name;
                            log.Info($"vtt url : {vttUrl}");
                        }
                        vttContent = MediaServicesHelper.ReturnContent(vttSubtitle);

                        if (data.timeOffset != null) // let's update the ttml with new timecode
                        {
                            var tsoffset = TimeSpan.Parse((string)data.timeOffset);
                            string arrow = " --> ";
                            StringBuilder sb = new StringBuilder();
                            string[] delim = { Environment.NewLine, "\n" }; // "\n" added in case you manually appended a newline
                            string[] vttlines = vttContent.Split(delim, StringSplitOptions.None);

                            foreach (string vttline in vttlines)
                            {
                                string line = vttline;
                                if (vttline.Contains(arrow))
                                {
                                    TimeSpan begin = TimeSpan.Parse(vttline.Substring(0, vttline.IndexOf(arrow)));
                                    TimeSpan end = TimeSpan.Parse(vttline.Substring(vttline.IndexOf(arrow) + 5));
                                    line = (begin + tsoffset).ToString(@"d\.hh\:mm\:ss\.fff") + arrow + (end + tsoffset).ToString(@"d\.hh\:mm\:ss\.fff");
                                }
                                sb.AppendLine(line);
                            }
                            vttContentTimeCorrected = sb.ToString();
                        }
                    }

                    if (ttmlSubtitle != null)
                    {
                        if (publishurl != null)
                        {
                            ttmlUrl = pathUrl + vttSubtitle.Name;
                            log.Info($"ttml url : {ttmlUrl}");
                        }
                        ttmlContent = MediaServicesHelper.ReturnContent(ttmlSubtitle);
                        if (data.timeOffset != null) // let's update the vtt with new timecode
                        {
                            var tsoffset = TimeSpan.Parse((string)data.timeOffset);
                            log.Info("tsoffset : " + tsoffset.ToString(@"d\.hh\:mm\:ss\.fff"));
                            XNamespace xmlns = "http://www.w3.org/ns/ttml";
                            XDocument docXML = XDocument.Parse(ttmlContent);
                            var tt = docXML.Element(xmlns + "tt");
                            var subtitles = docXML.Element(xmlns + "tt").Element(xmlns + "body").Element(xmlns + "div").Elements(xmlns + "p");
                            foreach (var sub in subtitles)
                            {
                                var begin = TimeSpan.Parse((string)sub.Attribute("begin"));
                                var end = TimeSpan.Parse((string)sub.Attribute("end"));
                                sub.SetAttributeValue("end", (end + tsoffset).ToString(@"d\.hh\:mm\:ss\.fff"));
                                sub.SetAttributeValue("begin", (begin + tsoffset).ToString(@"d\.hh\:mm\:ss\.fff"));
                            }
                            ttmlContentTimeCorrected = docXML.Declaration.ToString() + Environment.NewLine + docXML.ToString();
                        }
                    }

                    if (ttmlContent != "" && vttContent != "" && data.deleteAsset != null && ((bool)data.deleteAsset))
                    // If asset deletion was asked
                    {
                        outputAsset.Delete();
                    }
                }
                catch (Exception ex)
                {
                    string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                    log.Info($"ERROR: Exception {message}");
                    return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
                }

                log.Info($"");
                return req.CreateResponse(HttpStatusCode.OK, new
                {
                    vttUrl = vttUrl,
                    ttmlUrl = ttmlUrl,
                    pathUrl = pathUrl,
                    ttmlDocument = ttmlContent,
                    ttmlDocumentWithOffset = ttmlContentTimeCorrected,
                    vttDocument = vttContent,
                    vttDocumentWithOffset = vttContentTimeCorrected
                });
            }
        }
    }
}
