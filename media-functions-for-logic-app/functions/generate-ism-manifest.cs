/*

Azure Media Services REST API v2 Function
 
This function generates a server-side manifest (.ism) from the MP4/M4A files in the asset. It makes this file primary.
This manifest is needed to stream MP4 file(s) with Azure Media Services.

Caution : such assets are not guaranteed to work with Dynamic Packaging.

Note : this function makes  guesses to determine the files for the video tracks and audio tracks.
These guesses can be wrong. Please check the SMIL generated data for your scenario and your source assets.

As an option, this function can check that the streaming endpoint returns a successful client manifest.

Input:
{
    "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Mandatory, Id of the asset
    "fileName" : "manifest.ism", // Optional. file name of the manifest to create
    "checkStreamingEndpointResponse" : true // Optional. If true, then the asset is published temporarly and the function checks that the streaming endpoint returns a valid client manifest. It's a good way to know if the asset looks streamable (GOP aligned, etc)
}

Output:
{
    "fileName" : "manifest.ism" // The name of the manifest file created
    "manifestContent" : "" // the SMIL data created as an asset file 
    "checkStreamingEndpointResponseSuccess" : true //if check is successful 
}
*/


using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace media_functions_for_logic_app
{
    public static class generate_manifest
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        [FunctionName("generate-ism-manifest")]
        public static async Task<object> Run([HttpTrigger(WebHookType = "genericJson")]HttpRequestMessage req, TraceWriter log, Microsoft.Azure.WebJobs.ExecutionContext execContext)
        {
            log.Info($"Webhook was triggered!");

            // Init variables

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            string fileName = null;
            var manifestInfo = new ManifestHelpers.ManifestGenerated();

            log.Info(jsonContent);

            if (data.assetId == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass asset ID in the input object (assetId)"
                });
            }

            MediaServicesCredentials amsCredentials = new MediaServicesCredentials();
            log.Info($"Using Azure Media Service Rest API Endpoint : {amsCredentials.AmsRestApiEndpoint}");

            bool checkStreamingEndpointResponse = false;
            bool checkStreamingEndpointResponseSuccess = true;

            try
            {
                fileName = (string)data.fileName;

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

                log.Info($"creation of file {fileName}");

                // Manifest generate
                manifestInfo = ManifestHelpers.LoadAndUpdateManifestTemplate(destAsset, execContext);

                // if not file name passed, then we use the one generated based on mp4 files names
                if (fileName == null)
                {
                    fileName = manifestInfo.FileName;
                }

                var filetocreate = destAsset.AssetFiles.Create(fileName);

                using (Stream s = ManifestHelpers.GenerateStreamFromString(manifestInfo.Content))
                {
                    filetocreate.Upload(s);
                }

                log.Info("Manifest file created.");

                // let's make the manifest the primary file of the asset
                MediaServicesHelper.SetFileAsPrimary(destAsset, fileName);
                log.Info("Manifest file set as primary.");



                if (data.checkStreamingEndpointResponse != null && (bool)data.checkStreamingEndpointResponse)
                {
                    checkStreamingEndpointResponse = true;
                    // testing streaming
                    // publish with a streaming locator (1 hour)
                    IAccessPolicy readPolicy = _context.AccessPolicies.Create("readPolicy", TimeSpan.FromHours(1), AccessPermissions.Read);
                    ILocator outputLocator = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, destAsset, readPolicy);
                    var publishurlsmooth = MediaServicesHelper.GetValidOnDemandURI(_context, destAsset);

                    try
                    {
                        WebRequest request = WebRequest.Create(publishurlsmooth.ToString());
                        WebResponse response = request.GetResponse();
                        response.Close();
                    }

                    catch (Exception ex)
                    {
                        checkStreamingEndpointResponseSuccess = false;
                    }
                    outputLocator.Delete();
                    readPolicy.Delete();
                }

            }
            catch (Exception ex)
            {
                string message = ex.Message + ((ex.InnerException != null) ? Environment.NewLine + MediaServicesHelper.GetErrorMessage(ex) : "");
                log.Info($"ERROR: Exception {message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new { error = message });
            }

            log.Info($"");

            if (checkStreamingEndpointResponse)
            {
                return req.CreateResponse(HttpStatusCode.OK, new
                {
                    fileName = fileName,
                    manifestContent = manifestInfo.Content,
                    checkStreamingEndpointResponseSuccess = checkStreamingEndpointResponseSuccess
                });
            }
            else
            {
                return req.CreateResponse(HttpStatusCode.OK, new
                {
                    fileName = fileName,
                    manifestContent = manifestInfo.Content
                });
            }
        }

     
    }
}
