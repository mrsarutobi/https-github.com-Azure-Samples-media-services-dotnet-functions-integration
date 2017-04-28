#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"
#r "System.Web"

using System;
using System.Text;
using System.Net;
using System.Threading;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;


public static HttpResponseMessage Run(CloudBlockBlob myBlob, string fileName, TraceWriter log)
{
    // NOTE that the variables {fileName} here come from the path setting in function.json
    // and are passed into the  Run method signature above. We can use this to make decisions on what type of file
    // was dropped into the input container for the function. 

    // No need to do any Retry strategy in this function, By default, the SDK calls a function up to 5 times for a 
    // given blob. If the fifth try fails, the SDK adds a message to a queue named webjobs-blobtrigger-poison.

    log.Info($"C# Blob trigger function processed: {fileName}.json");
    string fName = "inputs\\" + fileName + ".json";
    string fContent;

    Stream stream = new MemoryStream();
    try
    {
        myBlob.DownloadToStream(stream);
        stream.Seek(0, SeekOrigin.Begin);
        StreamReader reader = new StreamReader(stream);
        fContent = reader.ReadToEnd();
    }
    catch (Exception ex)
    {
        log.Error("ERROR: failed.");
        log.Info($"StackTrace : {ex.StackTrace}");
        throw ex;
    }

    log.Info("FileName : " + fName);
    log.Info("FileContent : " + fContent);

    var param = Newtonsoft.Json.JsonConvert.SerializeObject(new { FileName = fName, FileContent = fContent });
    HttpContent jsonContent = new StringContent(param, Encoding.UTF8, "application/json");
    return new HttpResponseMessage
    {
        Content = jsonContent,
        StatusCode = HttpStatusCode.OK
    };
}
