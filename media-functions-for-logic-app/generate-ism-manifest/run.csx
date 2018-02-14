/*
This function generates a manifest (.ism) from the MP4/M4A files in the asset. It makes this file primary.
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

#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "System.Xml.Linq"
#load "../Shared/mediaServicesHelpers.csx"
#load "../Shared/copyBlobHelpers.csx"

using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Web;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.Azure.WebJobs;
using System.Xml.Linq;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

// Read values from the App.config file.
static string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
static string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");

static readonly string _AADTenantDomain = Environment.GetEnvironmentVariable("AMSAADTenantDomain");
static readonly string _RESTAPIEndpoint = Environment.GetEnvironmentVariable("AMSRESTAPIEndpoint");

static readonly string _mediaservicesClientId = Environment.GetEnvironmentVariable("AMSClientId");
static readonly string _mediaservicesClientSecret = Environment.GetEnvironmentVariable("AMSClientSecret");

// Field for service context.
private static CloudMediaContext _context = null;
private static CloudStorageAccount _destinationStorageAccount = null;


public static async Task<object> Run(HttpRequestMessage req, TraceWriter log, Microsoft.Azure.WebJobs.ExecutionContext execContext)
{
    log.Info($"Webhook was triggered!");

    // Init variables

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);

    string fileName = null;
    var manifestInfo = new ManifestGenerated();

    log.Info(jsonContent);

    if (data.assetId == null)
    {
        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass asset ID in the input object (assetId)"
        });
    }

    log.Info($"Using Azure Media Service Rest API Endpoint : {_RESTAPIEndpoint}");

    bool checkStreamingEndpointResponse = false;
    bool checkStreamingEndpointResponseSuccess = true;

    try
    {
        fileName = (string)data.fileName;

        AzureAdTokenCredentials tokenCredentials = new AzureAdTokenCredentials(_AADTenantDomain,
                              new AzureAdClientSymmetricKey(_mediaservicesClientId, _mediaservicesClientSecret),
                              AzureEnvironments.AzureCloudEnvironment);

        AzureAdTokenProvider tokenProvider = new AzureAdTokenProvider(tokenCredentials);

        _context = new CloudMediaContext(new Uri(_RESTAPIEndpoint), tokenProvider);

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
        manifestInfo = LoadAndUpdateManifestTemplate(destAsset, execContext);

        // if not file name passed, then we use the one generated based on mp4 files names
        if (fileName == null)
        {
            fileName = manifestInfo.FileName;
        }

        var filetocreate = destAsset.AssetFiles.Create(fileName);

        using (Stream s = GenerateStreamFromString(manifestInfo.Content))
        {
            filetocreate.Upload(s);
        }

        log.Info("Manifest file created.");

        // let's make the manifest the primary file of the asset
        SetFileAsPrimary(destAsset, fileName);
        log.Info("Manifest file set as primary.");



        if (data.checkStreamingEndpointResponse != null && (bool)data.checkStreamingEndpointResponse)
        {
            checkStreamingEndpointResponse = true;
            // testing streaming
            // publish with a streaming locator (1 hour)
            IAccessPolicy readPolicy = _context.AccessPolicies.Create("readPolicy", TimeSpan.FromHours(1), AccessPermissions.Read);
            ILocator outputLocator = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, destAsset, readPolicy);
            var publishurlsmooth = GetValidOnDemandURI(destAsset);

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
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.InternalServerError, new
        {
            Error = ex.ToString()
        });
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

public static Stream GenerateStreamFromString(string s)
{
    MemoryStream stream = new MemoryStream();
    StreamWriter writer = new StreamWriter(stream);
    writer.Write(s);
    writer.Flush();
    stream.Position = 0;
    return stream;
}

public class ManifestGenerated
{
    public string FileName;
    public string Content;
}

public static ManifestGenerated LoadAndUpdateManifestTemplate(IAsset asset, Microsoft.Azure.WebJobs.ExecutionContext execContext)
{
    var mp4AssetFiles = asset.AssetFiles.ToList().Where(f => f.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)).ToArray();
    var m4aAssetFiles = asset.AssetFiles.ToList().Where(f => f.Name.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)).ToArray();
    var mediaAssetFiles = asset.AssetFiles.ToList().Where(f => f.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)).ToArray();

    if (mediaAssetFiles.Count() != 0)
    {
        // Prepare the manifest
        string mp4fileuniqueaudio = null;

        // let's load the manifest template
        string manifestPath = Path.Combine(System.IO.Directory.GetParent(execContext.FunctionDirectory).FullName, "presets", "Manifest.ism");

        XDocument doc = XDocument.Load(manifestPath);

        XNamespace ns = "http://www.w3.org/2001/SMIL20/Language";

        var bodyxml = doc.Element(ns + "smil");
        var body2 = bodyxml.Element(ns + "body");

        var switchxml = body2.Element(ns + "switch");

        // audio tracks (m4a)
        foreach (var file in m4aAssetFiles)
        {
            switchxml.Add(new XElement(ns + "audio", new XAttribute("src", file.Name), new XAttribute("title", Path.GetFileNameWithoutExtension(file.Name))));
        }

        if (m4aAssetFiles.Count() == 0)
        {
            // audio track(s)
            var mp4AudioAssetFilesName = mp4AssetFiles.Where(f =>
                                                       (f.Name.ToLower().Contains("audio") && !f.Name.ToLower().Contains("video"))
                                                       ||
                                                       (f.Name.ToLower().Contains("aac") && !f.Name.ToLower().Contains("h264"))
                                                       );

            var mp4AudioAssetFilesSize = mp4AssetFiles.OrderBy(f => f.ContentFileSize);

            string mp4fileaudio = (mp4AudioAssetFilesName.Count() == 1) ? mp4AudioAssetFilesName.FirstOrDefault().Name : mp4AudioAssetFilesSize.FirstOrDefault().Name; // if there is one file with audio or AAC in the name then let's use it for the audio track
            switchxml.Add(new XElement(ns + "audio", new XAttribute("src", mp4fileaudio), new XAttribute("title", "audioname")));

            if (mp4AudioAssetFilesName.Count() == 1 && mediaAssetFiles.Count() > 1) //looks like there is one audio file and dome other video files
            {
                mp4fileuniqueaudio = mp4fileaudio;
            }
        }

        // video tracks
        foreach (var file in mp4AssetFiles)
        {
            if (file.Name != mp4fileuniqueaudio) // we don't put the unique audio file as a video track
            {
                switchxml.Add(new XElement(ns + "video", new XAttribute("src", file.Name)));
            }
        }

        // manifest filename
        string name = CommonPrefix(mediaAssetFiles.Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToArray());
        if (string.IsNullOrEmpty(name))
        {
            name = "manifest";
        }
        else if (name.EndsWith("_") && name.Length > 1) // i string ends with "_", let's remove it
        {
            name = name.Substring(0, name.Length - 1);
        }
        name = name + ".ism";

        return new ManifestGenerated() { Content = doc.Declaration.ToString() + Environment.NewLine + doc.ToString(), FileName = name };
    }
    else
    {
        return new ManifestGenerated() { Content = null, FileName = string.Empty }; // no mp4 in asset
    }
}

static public void SetFileAsPrimary(IAsset asset, string assetfilename)
{
    var ismAssetFiles = asset.AssetFiles.ToList().
        Where(f => f.Name.Equals(assetfilename, StringComparison.OrdinalIgnoreCase)).ToArray();

    if (ismAssetFiles.Count() == 1)
    {
        try
        {
            // let's remove primary attribute to another file if any
            asset.AssetFiles.Where(af => af.IsPrimary).ToList().ForEach(af => { af.IsPrimary = false; af.Update(); });
            ismAssetFiles.First().IsPrimary = true;
            ismAssetFiles.First().Update();
        }
        catch
        {
            throw;
        }
    }
}

public static string CommonPrefix(string[] ss)
{
    if (ss.Length == 0)
    {
        return "";
    }

    if (ss.Length == 1)
    {
        return ss[0];
    }

    int prefixLength = 0;

    foreach (char c in ss[0])
    {
        foreach (string s in ss)
        {
            if (s.Length <= prefixLength || s[prefixLength] != c)
            {
                return ss[0].Substring(0, prefixLength);
            }
        }
        prefixLength++;
    }

    return ss[0]; // all strings identical
}