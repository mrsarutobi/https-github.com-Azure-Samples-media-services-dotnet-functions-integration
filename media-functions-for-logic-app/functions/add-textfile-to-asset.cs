/*

Azure Media Services REST API v2 Function
 
This function adds a text file to an existing asset. Oct 17 version.
As a option, the text can be converted from ttml to vtt (useful when the ttml has been translated with MS Translator and the user wants a VTT file for Azure Media Player

Input:
{
    "document" : "", // content of the text file to create
    "fileName" : "subtitle-en.ttml", // file name to create
    "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Mandatory, Id of the asset
    "convertTtml" :true // optional, convert the document from ttml to vtt, and create another file in the asset : subtitle-en.vtt
 }

Output:
{
    
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
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System.Xml.Linq;

namespace media_functions_for_logic_app
{
    public static class add_textfile_to_asset
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("add-textfile-to-asset")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"Webhook was triggered!");

            // Init variables

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            log.Info(jsonContent);

            if (data.assetId == null)
            {
                // for test
                // data.assetId = "nb:cid:UUID:d9496372-32f5-430d-a4c6-d21ec3e01525";

                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass asset ID in the input object (assetId)"
                });
            }

            if (data.document == null)
            {
                // for test
                // data.assetId = "nb:cid:UUID:d9496372-32f5-430d-a4c6-d21ec3e01525";

                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass document data in the input object (document)"
                });
            }


            if (data.fileName == null)
            {
                // for test
                // data.assetId = "nb:cid:UUID:d9496372-32f5-430d-a4c6-d21ec3e01525";

                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass the file name data in the input object (fileName)"
                });
            }

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();

            log.Info($"Using Azure Media Service Rest API Endpoint : {amsCredentials.AmsRestApiEndpoint}");


            try
            {
                string document = (string)data.document;
                string fileName = (string)data.fileName;

                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                                      new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                                      AzureEnvironments.AzureCloudEnvironment);

                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);

                // Get the asset
                string assetid = data.assetId;
                var destAsset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

                if (destAsset == null)
                {
                    log.Info($"Asset not found {assetid}");
                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = "Asset not found"
                    });
                }

                log.Info(@"creation of file {fileName}");

                var filetocreate = destAsset.AssetFiles.Create(fileName);

                using (Stream s = GenerateStreamFromString(document))
                {
                    filetocreate.Upload(s);
                }

                if (data.convertTtml != null && ((bool)data.convertTtml == true))
                { // let's convert the ttml to vtt
                    log.Info("ttml to vtt convert process...");
                    XNamespace xmlns = "http://www.w3.org/ns/ttml";
                    XDocument docXML = XDocument.Parse(document);
                    var tt = docXML.Element(xmlns + "tt");
                    var subtitles = docXML.Element(xmlns + "tt").Element(xmlns + "body").Element(xmlns + "div").Elements(xmlns + "p");
                    StringBuilder sbuild = new StringBuilder();
                    string vttarrow = " --> ";
                    sbuild.AppendLine("WEBVTT");
                    sbuild.AppendLine();
                    foreach (var sub in subtitles)
                    {
                        var begin = (string)sub.Attribute("begin");
                        var end = (string)sub.Attribute("end");
                        var text = (string)sub.Value;
                        sbuild.AppendLine(begin + vttarrow + end);
                        sbuild.AppendLine(text);
                        sbuild.AppendLine();
                    }

                    string vttfilename = Path.GetFileNameWithoutExtension(fileName) + ".vtt";
                    log.Info(@"creation of file {vttfilename}");
                    var filetocreate2 = destAsset.AssetFiles.Create(vttfilename);
                    using (Stream s2 = GenerateStreamFromString(sbuild.ToString()))
                    {
                        filetocreate2.Upload(s2);
                    }
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                log.Info($"ERROR: Exception {message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
            }

            log.Info($"");
            return req.CreateResponse(HttpStatusCode.OK);
        }

        public static Stream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}