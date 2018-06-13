//
// Azure Media Services REST API v2 Functions
//
// GetCaptionBlobSasUri - This function gets caption data URI.
//
//  Input:
//      {
//          "assetId": "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",  // Id of the asset for copy destination
//          "timecodeOffset": "00:01:00"                                    // (Optional) Offset to add to captions
//      }
//  Output:
//      {
//          "vttBlobSasUri": "https://mediademostorage.blob.core.windows.net/asset-67f7e54a-4141-4e30-becf-3f508fbdd85f/HoloLensDemo_aud_SpReco.vtt?sv=2017-04-17&sr=b&sig=EFk1BMbk4QveTXuXS8HS065fB76%2FjX90aeIrSzh8d5I%3D&st=2018-06-12T14%3A06%3A40Z&se=2018-06-12T14%3A21%3A40Z&sp=r",
//          "ttmlBlobSasUri": "https://mediademostorage.blob.core.windows.net/asset-67f7e54a-4141-4e30-becf-3f508fbdd85f/HoloLensDemo_aud_SpReco.ttml?sv=2017-04-17&sr=b&sig=EFk1BMbk4QveTXuXS8HS065fB76%2FjX90aeIrSzh8d5I%3D&st=2018-06-12T14%3A06%3A40Z&se=2018-06-12T14%3A21%3A40Z&sp=r"
//      }
//

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Azure.WebJobs;
//using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
//using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
//using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.MediaServices.Client;

using Newtonsoft.Json;

using advanced_vod_functions.SharedLibs;


namespace advanced_vod_functions
{
    public static class GetCaptionBlobSasUri
    {
        private static CloudMediaContext _context = null;

        [FunctionName("GetCaptionBlobSasUri")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"AMS v2 Function - GetCaptionBlobSasUri was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            // parameter handling
            if (data.assetId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Please pass assetId in the input object" });
            string assetId = data.assetId;
            string timecodeOffset = null;
            if (data.timecodeOffset != null)
                timecodeOffset = data.timecodeOffset;

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            IAsset asset = null;
            string vttContent = "";
            string vttContentTimeOffset = "";
            string vttBlobSasUri = null;
            string ttmlContent = "";
            string ttmlContentTimeOffset = "";
            string ttmlBlobSasUri = null;

            try
            {
                // Load AMS account context
                log.Info($"Using AMS v2 REST API Endpoint : {amsCredentials.AmsRestApiEndpoint.ToString()}");

                AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(amsCredentials.AmsAadTenantDomain,
                    new AzureAdClientSymmetricKey(amsCredentials.AmsClientId, amsCredentials.AmsClientSecret),
                    AzureEnvironments.AzureCloudEnvironment);
                AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);
                _context = new CloudMediaContext(amsCredentials.AmsRestApiEndpoint, tokenProvider);

                // Get the Asset
                asset = _context.Assets.Where(a => a.Id == assetId).FirstOrDefault();
                if (asset == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, new { error = "Asset not found" });
                }

                string destinationContainerName = asset.Uri.Segments[1];
                var vttAssetFile = asset.AssetFiles.Where(a => a.Name.ToUpper().EndsWith(".VTT")).FirstOrDefault();
                var ttmlAssetFile = asset.AssetFiles.Where(a => a.Name.ToUpper().EndsWith(".TTML")).FirstOrDefault();

                if (vttAssetFile != null)
                {
                    vttContent = MediaServicesHelper.GetContentFromAssetFile(vttAssetFile);
                    CloudBlobContainer destinationBlobContainer = BlobStorageHelper.GetCloudBlobContainer(BlobStorageHelper.AmsStorageAccountName, BlobStorageHelper.AmsStorageAccountKey, destinationContainerName);
                    CloudBlockBlob blobVTT = destinationBlobContainer.GetBlockBlobReference(vttAssetFile.Name);

                    if (timecodeOffset != null) // let's update the ttml with new timecode
                    {
                        var tcOffset = TimeSpan.Parse(timecodeOffset);
                        string arrow = " --> ";
                        System.Text.StringBuilder sb = new System.Text.StringBuilder();
                        string[] delim = { Environment.NewLine, "\n" }; // "\n" added in case you manually appended a newline
                        string[] vttlines = vttContent.Split(delim, StringSplitOptions.None);

                        foreach (string vttline in vttlines)
                        {
                            string line = vttline;
                            if (vttline.Contains(arrow))
                            {
                                TimeSpan begin = TimeSpan.Parse(vttline.Substring(0, vttline.IndexOf(arrow)));
                                TimeSpan end = TimeSpan.Parse(vttline.Substring(vttline.IndexOf(arrow) + 5));
                                line = (begin + tcOffset).ToString(@"d\.hh\:mm\:ss\.fff") + arrow + (end + tcOffset).ToString(@"d\.hh\:mm\:ss\.fff");
                            }
                            sb.AppendLine(line);
                        }
                        vttContentTimeOffset = sb.ToString();

                        if (vttContentTimeOffset != null)
                        {
                            string blobName = "Converted-" + vttAssetFile.Name;
                            blobVTT = MediaServicesHelper.WriteContentToBlob(asset, destinationBlobContainer, blobName, vttContentTimeOffset);
                        }
                    }

                    // Get Blob URI with SAS Token
                    var sasBlobPolicy = new SharedAccessBlobPolicy();
                    sasBlobPolicy.SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-5);
                    sasBlobPolicy.SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(10);
                    sasBlobPolicy.Permissions = SharedAccessBlobPermissions.Read;
                    string sasBlobToken = blobVTT.GetSharedAccessSignature(sasBlobPolicy);
                    vttBlobSasUri = blobVTT.Uri + sasBlobToken;
                }

                if (ttmlAssetFile != null)
                {
                    ttmlContent = MediaServicesHelper.GetContentFromAssetFile(ttmlAssetFile);
                    CloudBlobContainer destinationBlobContainer = BlobStorageHelper.GetCloudBlobContainer(BlobStorageHelper.AmsStorageAccountName, BlobStorageHelper.AmsStorageAccountKey, destinationContainerName);
                    CloudBlockBlob blobTTML = destinationBlobContainer.GetBlockBlobReference(ttmlAssetFile.Name);

                    if (timecodeOffset != null) // let's update the vtt with new timecode
                    {
                        var tcOffset = TimeSpan.Parse(timecodeOffset);
                        //log.Info("tsoffset : " + tcOffset.ToString(@"d\.hh\:mm\:ss\.fff"));
                        XNamespace xmlns = "http://www.w3.org/ns/ttml";
                        XDocument docXML = XDocument.Parse(ttmlContent);
                        var tt = docXML.Element(xmlns + "tt");
                        var ttmlElements = docXML.Element(xmlns + "tt").Element(xmlns + "body").Element(xmlns + "div").Elements(xmlns + "p");
                        foreach (var ttmlElement in ttmlElements)
                        {
                            var begin = TimeSpan.Parse((string)ttmlElement.Attribute("begin"));
                            var end = TimeSpan.Parse((string)ttmlElement.Attribute("end"));
                            ttmlElement.SetAttributeValue("end", (end + tcOffset).ToString(@"d\.hh\:mm\:ss\.fff"));
                            ttmlElement.SetAttributeValue("begin", (begin + tcOffset).ToString(@"d\.hh\:mm\:ss\.fff"));
                        }
                        ttmlContentTimeOffset = docXML.Declaration.ToString() + Environment.NewLine + docXML.ToString();

                        if (ttmlContentTimeOffset != null)
                        {
                            string blobName = "Converted-" + ttmlAssetFile.Name;
                            blobTTML = MediaServicesHelper.WriteContentToBlob(asset, destinationBlobContainer, blobName, ttmlContentTimeOffset);
                        }
                    }

                    // Get Blob URI with SAS Token
                    var sasBlobPolicy = new SharedAccessBlobPolicy();
                    sasBlobPolicy.SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-5);
                    sasBlobPolicy.SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(10);
                    sasBlobPolicy.Permissions = SharedAccessBlobPermissions.Read;
                    string sasBlobToken = blobTTML.GetSharedAccessSignature(sasBlobPolicy);
                    ttmlBlobSasUri = blobTTML.Uri + sasBlobToken;
                }
            }
            catch (Exception e)
            {
                log.Info($"Exception {e}");
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                vttBlobSasUri = vttBlobSasUri,
                ttmlBlobSasUri = ttmlBlobSasUri
            });
        }
    }
}