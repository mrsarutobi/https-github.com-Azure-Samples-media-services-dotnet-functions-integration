//
// Azure Media Services REST API v2 - Functions
//
// Shared Library
//

using System;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Net;
using System.Net.Http;
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
using Newtonsoft.Json;

namespace media_functions_for_logic_app
{
    public class JobHelpers
    {
        // Return the new name of Media Reserved Unit
        public static string ReturnMediaReservedUnitName(ReservedUnitType unitType)
        {
            switch (unitType)
            {
                case ReservedUnitType.Basic:
                default:
                    return "S1";

                case ReservedUnitType.Standard:
                    return "S2";

                case ReservedUnitType.Premium:
                    return "S3";

            }
        }

        public static int AddTask(Microsoft.Azure.WebJobs.ExecutionContext execContext, CloudMediaContext context, IJob job, IAsset sourceAsset, string value, string processor, string presetfilename, string stringtoreplace, ref int taskindex, int priority = 10, string specifiedStorageAccountName = null)
        {
            if (value != null)
            {
                // Get a media processor reference, and pass to it the name of the 
                // processor to use for the specific task.
                IMediaProcessor mediaProcessor = MediaServicesHelper.GetLatestMediaProcessorByName(context, processor);

                string presetPath = Path.Combine(System.IO.Directory.GetParent(execContext.FunctionDirectory).FullName, "presets", presetfilename);

                string Configuration = File.ReadAllText(presetPath).Replace(stringtoreplace, value);

                // Create a task with the encoding details, using a string preset.
                var task = job.Tasks.AddNew(processor + " task",
                   mediaProcessor,
                   Configuration,
                   TaskOptions.None);

                task.Priority = priority;

                // Specify the input asset to be indexed.
                task.InputAssets.Add(sourceAsset);

                // Add an output asset to contain the results of the job.
                // Use a non default storage account in case this was provided in 'AddTask'

                task.OutputAssets.AddNew(sourceAsset.Name + " " + processor + " Output", specifiedStorageAccountName, AssetCreationOptions.None);

                return taskindex++;
            }
            else
            {
                return -1;
            }
        }

        public static string ReturnId(IJob job, int index)
        {
            return index > -1 ? job.OutputMediaAssets[index].Id : null;
        }

        public static string ReturnTaskId(IJob job, int index)
        {
            return index > -1 ? job.Tasks[index].Id : null;
        }

        public static string OutputStorageFromParam(dynamic objParam)
        {
            string storage = (objParam != null) ? (string)objParam.outputStorage : null;
            return string.IsNullOrWhiteSpace(storage) ? null : storage;
        }
    }
}

