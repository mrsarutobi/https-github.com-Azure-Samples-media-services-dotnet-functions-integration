//
// Azure Media Services REST API v2 - Functions
//
// Shared Library
//

using System;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.Azure.WebJobs.Host;
using System.Xml.Linq;

namespace media_functions_for_logic_app
{
    public class ManifestHelpers
    {
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


        static public ManifestTimingData GetManifestTimingData(CloudMediaContext context, IAsset asset, TraceWriter log)
        // Parse the manifest and get data from it
        {
            ManifestTimingData response = new ManifestTimingData() { IsLive = false, Error = false, TimestampOffset = 0, TimestampList = new List<ulong>() };

            try
            {
                ILocator mytemplocator = null;
                Uri myuri = MediaServicesHelper.GetValidOnDemandURI(context, asset);
                if (myuri == null)
                {
                    mytemplocator = MediaServicesHelper.CreatedTemporaryOnDemandLocator(asset);
                    myuri = MediaServicesHelper.GetValidOnDemandURI(context, asset);
                }
                if (myuri != null)
                {
                    log.Info($"Asset URI {myuri.ToString()}");

                    XDocument manifest = XDocument.Load(myuri.ToString());

                    //log.Info($"manifest {manifest}");
                    var smoothmedia = manifest.Element("SmoothStreamingMedia");
                    var videotrack = smoothmedia.Elements("StreamIndex").Where(a => a.Attribute("Type").Value == "video");

                    // TIMESCALE
                    string timescalefrommanifest = smoothmedia.Attribute("TimeScale").Value;
                    if (videotrack.FirstOrDefault().Attribute("TimeScale") != null) // there is timescale value in the video track. Let's take this one.
                    {
                        timescalefrommanifest = videotrack.FirstOrDefault().Attribute("TimeScale").Value;
                    }
                    ulong timescale = ulong.Parse(timescalefrommanifest);
                    response.TimeScale = (ulong?)timescale;

                    // Timestamp offset
                    if (videotrack.FirstOrDefault().Element("c").Attribute("t") != null)
                    {
                        response.TimestampOffset = ulong.Parse(videotrack.FirstOrDefault().Element("c").Attribute("t").Value);
                    }
                    else
                    {
                        response.TimestampOffset = 0; // no timestamp, so it should be 0
                    }

                    ulong totalduration = 0;
                    ulong durationpreviouschunk = 0;
                    ulong durationchunk;
                    int repeatchunk;
                    foreach (var chunk in videotrack.Elements("c"))
                    {
                        durationchunk = chunk.Attribute("d") != null ? ulong.Parse(chunk.Attribute("d").Value) : 0;
                        log.Info($"duration d {durationchunk}");

                        repeatchunk = chunk.Attribute("r") != null ? int.Parse(chunk.Attribute("r").Value) : 1;
                        log.Info($"repeat r {repeatchunk}");
                        totalduration += durationchunk * (ulong)repeatchunk;

                        if (chunk.Attribute("t") != null)
                        {
                            //totalduration = ulong.Parse(chunk.Attribute("t").Value) - response.TimestampOffset; // new timestamp, perhaps gap in live stream....
                            response.TimestampList.Add(ulong.Parse(chunk.Attribute("t").Value));
                            log.Info($"t value {ulong.Parse(chunk.Attribute("t").Value)}");
                        }
                        else
                        {
                            response.TimestampList.Add(response.TimestampList[response.TimestampList.Count() - 1] + durationpreviouschunk);
                        }

                        for (int i = 1; i < repeatchunk; i++)
                        {
                            response.TimestampList.Add(response.TimestampList[response.TimestampList.Count() - 1] + durationchunk);
                        }

                        durationpreviouschunk = durationchunk;

                    }
                    response.TimestampEndLastChunk = response.TimestampList[response.TimestampList.Count() - 1] + durationpreviouschunk;

                    if (smoothmedia.Attribute("IsLive") != null && smoothmedia.Attribute("IsLive").Value == "TRUE")
                    { // Live asset.... No duration to read (but we can read scaling and compute duration if no gap)
                        response.IsLive = true;
                        response.AssetDuration = TimeSpan.FromSeconds((double)totalduration / ((double)timescale));
                    }
                    else
                    {
                        totalduration = ulong.Parse(smoothmedia.Attribute("Duration").Value);
                        response.AssetDuration = TimeSpan.FromSeconds((double)totalduration / ((double)timescale));
                    }
                }
                else
                {
                    response.Error = true;
                }
                if (mytemplocator != null) mytemplocator.Delete();
            }
            catch (Exception ex)
            {
                response.Error = true;
            }
            return response;
        }



        public static EndTimeInTable RetrieveLastEndTime(CloudTable table, string programID)
        {
            TableOperation tableOperation = TableOperation.Retrieve<EndTimeInTable>(programID, "lastEndTime");
            TableResult tableResult = table.Execute(tableOperation);
            return tableResult.Result as EndTimeInTable;
        }

        public static void UpdateLastEndTime(CloudTable table, TimeSpan endtime, string programId, int id, ProgramState state)
        {
            var EndTimeInTableEntity = new EndTimeInTable();
            EndTimeInTableEntity.ProgramId = programId;
            EndTimeInTableEntity.Id = id.ToString();
            EndTimeInTableEntity.ProgramState = state.ToString();
            EndTimeInTableEntity.LastEndTime = endtime.ToString();
            EndTimeInTableEntity.AssignPartitionKey();
            EndTimeInTableEntity.AssignRowKey();
            TableOperation tableOperation = TableOperation.InsertOrReplace(EndTimeInTableEntity);
            table.Execute(tableOperation);
        }

        public static IAsset GetAssetFromProgram(CloudMediaContext context, string programId)
        {
            IAsset asset = null;

            try
            {
                IProgram program = context.Programs.Where(p => p.Id == programId).FirstOrDefault();
                if (program != null)
                {
                    asset = program.Asset;
                }
            }
            catch
            {
            }
            return asset;
        }


        // return the exact timespan on GOP
        static public TimeSpan ReturnTimeSpanOnGOP(ManifestTimingData data, TimeSpan ts)
        {
            var response = ts;
            ulong timestamp = (ulong)(ts.TotalSeconds * data.TimeScale);

            int i = 0;
            foreach (var t in data.TimestampList)
            {
                if (t < timestamp && i < (data.TimestampList.Count - 1) && timestamp < data.TimestampList[i + 1])
                {
                    response = TimeSpan.FromSeconds((double)t / (double)data.TimeScale);
                    break;
                }
                i++;
            }
            return response;
        }






        public class ManifestTimingData
        {
            public TimeSpan AssetDuration { get; set; }
            public ulong TimestampOffset { get; set; }
            public ulong? TimeScale { get; set; }
            public bool IsLive { get; set; }
            public bool Error { get; set; }
            public List<ulong> TimestampList { get; set; }
            public ulong TimestampEndLastChunk { get; set; }
        }

        public class SubclipInfo
        {
            public TimeSpan subclipStart { get; set; }
            public TimeSpan subclipDuration { get; set; }
            public string programId { get; set; }
        }


        public class EndTimeInTable : TableEntity
        {
            private string programId;
            private string lastendtime;
            private string id;
            private string programState;

            public void AssignRowKey()
            {
                this.RowKey = "lastEndTime";
            }
            public void AssignPartitionKey()
            {
                this.PartitionKey = programId;
            }
            public string ProgramId
            {
                get
                {
                    return programId;
                }

                set
                {
                    programId = value;
                }
            }
            public string LastEndTime
            {
                get
                {
                    return lastendtime;
                }

                set
                {
                    lastendtime = value;
                }
            }
            public string Id
            {
                get
                {
                    return id;
                }

                set
                {
                    id = value;
                }
            }
            public string ProgramState
            {
                get
                {
                    return programState;
                }

                set
                {
                    programState = value;
                }
            }
        }
    }
}

