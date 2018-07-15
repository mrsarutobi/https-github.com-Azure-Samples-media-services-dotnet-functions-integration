# Functions documentation
This section lists the functions available and describes the input and output parameters.

## add-textfile-to-asset

This function adds a text file to an existing asset.
As a option, the text can be converted from ttml to vtt (useful when the ttml has been translated with MS Translator and the user wants a VTT file for Azure Media Player
```c#
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

```

## add-offset-to-insights

This function adds time offset to video indexer insights JSON data.

Input:
{
    "insights" : [], // Mandatory, video indexer json
    "timeOffset" :"00:01:00", // offset to add (used for live analytics)
 }

Output:
{
    "jsonOffset : []  // the full json document with offset
 }

```


## check-blob-copy-to-asset-status

This function monitor the copy of files (blobs) to a new asset previously created.
```c#
Input:
{
      "destinationContainer" : "mycontainer",
      "delay": 15000; // optional (default is 5000)
      "assetStorage" :"amsstore01" // optional. Name of attached storage where to create the asset. Please use the function setting variable MediaServicesAttachedStorageCredentials to pass the credentials
}
Output:
{
      "copyStatus": 2 // status
       "isRunning" : "False"
       "isSuccessful" : "False"
}
```

## check-blob-copy-to-container-status

This function monitor the copy of files (blobs) to a storage container.
```c#
Input:
{
    "targetStorageAccountName" : "",
    "targetStorageAccountKey": "",
    "targetContainer" : ""
    "delay": 15000 // optional (default is 5000)
    "fileNamePrefix" :"video.mp4" // optional. To monitor only for a specific file name copy. Can be the full name or a prefix
}
Output:
{
      "copyStatus": 2 // status
       "isRunning" : "False"
       "isSuccessful" : "False"
}
```

## check-job-status

This function checks a job status.
```c#
Input:
{
    "jobId" : "nb:jid:UUID:1ceaa82f-2607-4df9-b034-cd730dad7097", // Mandatory, Id of the source asset
    "extendedInfo" : true // optional. Returns ams account unit size, nb units, nb of jobs in queue, scheduled and running states. Only if job is complete or error
    "noWait" : true // optional. Set this parameter if you don't want the function to wait. Otherwise it waits for 15 seconds if the job is not completed before doing another check and terminate
 }

Output:
{
    "jobState" : 2,             // The state of the job (int)
    "isRunning" : "False",      // True if job is running
    "isSuccessful" : "True",    // True is job is a success. Only valid if IsRunning = False
    "errorText" : ""            // error(s) text if job state is error
    "startTime" :""
    "endTime" : "",
    "runningDuration" : "",
    "progress" : 20.3           // overall progress, between 0 and 100
    "extendedInfo" :            // if extendedInfo is true and job is finished or in error
    {
        "mediaUnitNumber" = 2,
        "mediaUnitSize" = "S2",
        "otherJobsProcessing" = 2,
        "otherJobsScheduled" = 1,
        "otherJobsQueue" = 1,
        "amsRESTAPIEndpoint" = "http:/...."
    }
 }
```

## check-task-status

This function checks a task status.
```c#
Input:
{
    "jobId" : "nb:jid:UUID:1ceaa82f-2607-4df9-b034-cd730dad7097", // Mandatory, Id of the job
    "taskId" : "nb:tid:UUID:cdc25b10-3ed7-4005-bcf9-6222b35b5be3", // Mandatory, Id of the task
    "extendedInfo" : true // optional. Returns ams account unit size, nb units, nb of jobs in queue, scheduled and running states. Only if job is complete or error
    "noWait" : true // optional. Set this parameter if you don't want the function to wait. Otherwise it waits for 15 seconds if the job is not completed before doing another check and terminate
 }

Output:
{
    "taskState" : 2,            // The state of the task (int)
    "isRunning" : "False",      // True if task is running
    "isSuccessful" : "True",    // True is task is a success. Value is only valid if isRunning = "False"
    "errorText" : ""            // error(s) text if task state is error
    "startTime" :""
    "endTime" : "",
    "runningDuration" : "",
    "progress"" : 20.1          // progress of the task, between 0 and 100
    "extendedInfo" :            // if extendedInfo is true and job is finished or in error
    {
        "mediaUnitNumber" = 2,
        "mediaUnitSize" = "S2",
        "otherJobsProcessing" = 2,
        "otherJobsScheduled" = 1,
        "otherJobsQueue" = 1,
        "amsRESTAPIEndpoint" = "http://....."
    }
 }
```

## create-empty-asset

This function creates an empty asset.
```c#
Input:
{
    "assetName" : "the name of the asset",
    "assetStorage" :"amsstore01" // optional. Name of attached storage where to create the asset 
}

Output:
{
    "assetId" : "the Id of the asset created",
    "containerPath" : "the url to the storage container of the asset"
}
```

## delete-asset-files

This function deletes the files from a specific asset.
```c#
Input:
{
    "assetId" : "the Id of the asset",
    "startsWith" : "video", //optional, copy only files that start with name video
    "endsWith" : ".mp4", //optional, copy only files that end with .mp4
}
Output:
{
}
```

## delete-entity

This function delete AMS entities (job(s) and/or asset(s)).
```c#
Input:
{
    "jobID": "nb:jid:UUID:7f566f5e-be9c-434f-bb7b-101b2e24f27e,nb:jid:UUID:58f9e85a-a889-4205-baa1-ecf729f9c753",     // job(s) id. Coma delimited if several job ids 
    "assetId" : "nb:cid:UUID:61926f1d-69ba-4386-a90e-e27803104853,nb:cid:UUID:b4668bc4-2899-4247-b339-429025153ab9"   // asset(s) id.
}

Output:
{
}
```

## generate-ism-manifest

This function generates a manifest (.ism) from the MP4/M4A files in the asset. It makes this file primary.
This manifest is needed to stream MP4 file(s) with Azure Media Services.

Caution : such assets are not guaranteed to work with Dynamic Packaging.

Note : this function makes guesses to determine the files for the video tracks and audio tracks.
These guesses can be wrong. Please check the SMIL generated data for your scenario and your source assets.

As an option, this function can check that the streaming endpoint returns a successful client manifest.

```c#
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
```

## list-asset-files

This function lists asset files.
```c#
Input:
{
    "assetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Mandatory, Id of the source asset
    "startsWith" : "video", //optional, list only files that start with name video (filter)
    "endsWith" : ".mp4", //optional, list only files that end with .mp4 (filter)
}

Output:
{
    assetFiles :  Array of asset files (filtered)
}
```

## live-subclip-analytics

This function submits a job to process a live stream with media analytics.
The first task is a subclipping task that createq a MP4 file, then media analytics are processed on this asset.

```c#
Input:
{
    "channelName": "channel1",      // Mandatory
    "programName" : "program1",     // Mandatory
    "intervalSec" : 60              // Optional. Default is 60 seconds. The duration of subclip (and interval between two calls)

    "mesSubclip" :      // Optional as subclip will always be done but it is required to specify an output storage
    {
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "mesThumbnails" :      // Optional but required to generate thumbnails with Media Encoder Standard (MES)
    {
        "start" : "{Best}",  // Optional. Start time/mode. Default is "{Best}"
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
     "indexV1" :             // Optional but required to index audio with Media Indexer v1
    {
        "language" : "English", // Optional. Default is "English"
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "indexV2" :             // Optional but required to index audio with Media Indexer v2
    {
        "language" : "EnUs", // Optional. Default is EnUs
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "ocr" :             // Optional but required to do OCR
    {
        "language" : "AutoDetect", // Optional (Autodetect is the default)
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "faceDetection" :             // Optional but required to do Face Detection
    {
        "mode" : "PerFaceEmotion", // Optional (PerFaceEmotion is the default)
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "faceRedaction" :             // Optional but required to do Face Redaction
    {
        "mode" : "analyze"                  // Optional (analyze is the default)
        "outputStorage" : "amsstorage01"    // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "motionDetection" :             // Optional but required to do Motion Detection
    {
        "level" : "medium",                 // Optional (medium is the default)
        "outputStorage" : "amsstorage01"    // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "summarization" :                      // Optional but required to do summarization
    {
        "duration" : "0.0",                 // Optional (0.0 is the default)
        "outputStorage" : "amsstorage01"    // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "hyperlapse" :             // Optional but required to do hyperlapse
    {
        "speed" : "8", // Optional (8 is the default)
        "outputStorage" : "amsstorage01"    // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "videoAnnotation" :             // Optional but required to do Video Annotation
    {
        "outputStorage" : "amsstorage01"    // Optional. Storage account name where to put the output asset (attached to AMS account)
    },


    // General job properties
    "priority" : 10,                            // Optional, priority of the job
 
    // For compatibility only with old workflows. Do not use anymore!
    "indexV1Language" : "English",  // Optional
    "indexV2Language" : "EnUs",     // Optional
    "ocrLanguage" : "AutoDetect" or "English",  // Optional
    "faceDetectionMode" : "PerFaceEmotion,      // Optional
    "faceRedactionMode" : "analyze",            // Optional, but required for face redaction
    "motionDetectionLevel" : "medium",          // Optional
    "summarizationDuration" : "0.0",            // Optional. 0.0 for automatic
    "hyperlapseSpeed" : "8"                     // Optional
    "mesThumbnailsStart" : "{Best}",            // Optional. Add a task to generate thumbnails
}

Output:
{
        "triggerStart" : "" // date and time when the function was called
        "jobId" :  // job id
        "subclip" :
        {
            assetId : "",
            taskId : "",
            start : "",
            duration : ""
        },
        "indexV1" :
        {
            assetId : "",
            taskId : "",
            language : ""
        },
        "indexV2" :
        {
            assetId : "",
            taskId : "",
            language : ""
        },
        "ocr" :
        {
            assetId : "",
            taskId : ""
        },
        "faceDetection" :
        {
            assetId : ""
            taskId : ""
        },
        "faceRedaction" :
        {
            assetId : ""
            taskId : ""
        },
        "motionDetection" :
        {
            assetId : "",
            taskId : ""
        },
        "summarization" :
        {
            assetId : "",
            taskId : ""
        },
        "hyperlapse" :
        {
            assetId : "",
            taskId : ""
        },
         "mesThumbnails" :
        {
            assetId : "",
            taskId : ""
        },
         "videoAnnotation" :
        {
            assetId : "",
            taskId : ""
        }
        "programId" = programid,
        "channelName" : "",
        "programName" : "",
        "programUrl":"",
        "programState" : "Running",
        "programStateChanged" : "True", // if state changed since last call
        "otherJobsQueue" = 3 // number of jobs in the queue
}
```

## sync-asset

This function create the asset files based on the blobs in the asset container.
```c#
Input:
{
    "assetId" : "the Id of the asset"
}
```

## submit-job

This function submits a job wth encoding and/or analytics.
```c#
Input:
{
    "assetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Mandatory, Id of the source asset
    "mesPreset" : "Adaptive Streaming",         // Optional but required to encode with Media Encoder Standard (MES). If mesPreset contains an extension "H264 Multiple Bitrate 720p with thumbnail.json" then it loads this file from ..\Presets
    "workflowAssetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Optional, but required to encode the asset with Premium Workflow Encoder. Id for the workflow asset
    "indexV1Language" : "English",              // Optional but required to index the asset with Indexer v1
    "indexV2Language" : "EnUs",                 // Optional but required to index the asset with Indexer v2
    "ocrLanguage" : "AutoDetect" or "English",  // Optional but required to do OCR
    "faceDetectionMode" : "PerFaceEmotion,      // Optional but required to trigger face detection
    "faceRedactionMode" : "analyze",            // Optional, but required for face redaction
    "motionDetectionLevel" : "medium",          // Optional, required for motion detection
    "summarizationDuration" : "0.0",            // Optional. Required to create video summarization. "0.0" for automatic
    "hyperlapseSpeed" : "8",                    // Optional, required to hyperlapse the video
    "priority" : 10,                            // Optional, priority of the job
    "useEncoderOutputForAnalytics" : true       // Optional, use generated asset by MES or Premium Workflow as a source for media analytics (except hyperlapse)
    "jobName" : ""                              // Optional, job name
}

Output:
{
    "jobId" :  // job id
    "mes" : // Output asset generated by MES (if mesPreset was specified)
        {
            assetId : "",
            taskId : ""
        },
    "mepw" : // Output asset generated by Premium Workflow Encoder
        {
            assetId : "",
            taskId : ""
        },
    "indexV1" :  // Output asset generated by Indexer v1
        {
            assetId : "",
            taskId : "",
            language : ""
        },
    "indexV2" : // Output asset generated by Indexer v2
        {
            assetId : "",
            taskId : "",
            language : ""
        },
    "ocr" : // Output asset generated by OCR
        {
            assetId : "",
            taskId : ""
        },
    "faceDetection" : // Output asset generated by Face detection
        {
            assetId : ""
            taskId : ""
        },
    "faceRedaction" : // Output asset generated by Face redaction
        {
            assetId : ""
            taskId : ""
        },
     "motionDetection" : // Output asset generated by motion detection
        {
            assetId : "",
            taskId : ""
        },
     "summarization" : // Output asset generated by video summarization
        {
            assetId : "",
            taskId : ""
        },
     "hyperlapse" : // Output asset generated by Hyperlapse
        {
            assetId : "",
            taskId : ""
        }
 }
```

## publish-asset

This function publishes an asset.
```c#
Input:
{
    "assetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Mandatory, Id of the source asset
    "preferredSE" : "default" // Optional, name of Streaming Endpoint if a specific Streaming Endpoint should be used for the URL outputs
}

Output:
{
    playerUrl : "", // Url of demo AMP with content
    smoothUrl : "", // Url for the published asset (contains name.ism/manifest at the end) for dynamic packaging
    pathUrl : ""    // Url of the asset (path)
}
```

## return-analytics

his function returns media analytics from an asset.
```c#
Input:
{
    "faceRedaction" : 
    {
        "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Optional, Id of the source asset that contains media analytics (face redaction)
        "deleteAsset" : true, // Optional, delete the asset(s) once data has been read from it
        "copyToContainer" : "jpgfaces" // Optional, to copy the faces (jpg files) to a specific container in the same storage account. Use lowercases as this is the container name and there are restrictions. Used as a prefix, as date is added at the end (yyyyMMdd)
        "copyToContainerAccountName" : "jhggjgghggkj" // storage account name. optional. if not provided, ams storage account is used
        "copyToContainerAccountKey" "" // storage account key
        },
   "motionDetection" : 
    {
        "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Optional, Id of the source asset that contains media analytics (motion detection)
        "deleteAsset" : true // Optional, delete the asset(s) once data has been read from it
    },
     "ocr" : 
    {
        "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Optional, Id of the source asset that contains media analytics (ocr)
        "deleteAsset" : true // Optional, delete the asset(s) once data has been read from it
    },
   "videoAnnotation" : 
    {
        "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Optional, Id of the source asset that contains the MES thumbnails
        "deleteAsset" : true // Optional, delete the asset(s) once data has been read from it
    },
   "mesThumbnails" : 
    {
        "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Optional, Id of the source asset that contains media analytics (face redaction)
        "deleteAsset" : true // Optional, delete the asset(s) once data has been read from it
        "copyToContainer" : "thumbnails" // Optional, to copy the thumbnails (png files) to a specific container in the same storage account. Use lowercases as this is the container name and there are restrictions. Used as a prefix, as date is added at the end (yyyyMMdd)
        "copyToContainerAccountName" : "jhggjgghggkj" // storage account name. optional. if not provided, ams storage account is used
        "copyToContainerAccountKey" "" // storage account key
     },

     "timeOffset" :"00:01:00", // optional, offset to add to data from face redaction, ocr, video annotation (used for live analytics)
 }

Output:
{
    "faceRedaction" :
        {
        "json" : "",      // the json of the face redaction
        "jsonOffset" : "",      // the json of the face redaction with offset
        "jpgFaces":[
                {
                    "id" :24,
                    "fileId": "nb:cid:UUID:a93464ae-cbd5-4e63-9459-a3e2cf869f0e",
                    "fileName": "ArchiveTopBitrate_video_800000_thumb000024.jpg",
                    "url" : "http://xpouyatdemo.streaming.mediaservices.windows.net/903f9261-d745-48aa-8dfe-ebcd6e6128d6/ArchiveTopBitrate_video_800000_thumb000024.jpg"
                }
                ]
        "pathUrl" : "",     // the path to the asset if asset is published
        },
        "pngThumbnails":[
                {
                    "id" :24,
                    "fileId": "nb:cid:UUID:a93464ae-cbd5-4e63-9459-a3e2cf869f0e",
                    "fileName": "ArchiveTopBitrate_video_800000_thumb000024.jpg",
                    "url" : "http://xpouyatdemo.streaming.mediaservices.windows.net/903f9261-d745-48aa-8dfe-ebcd6e6128d6/ArchiveTopBitrate_video_800000_thumb000024.jpg"
                }
                ]
        "pathUrl" : "",     // the path to the asset if asset is published
        },
    "motionDetection":
        {
        "json" : "",      // the json of the face redaction
        "jsonOffset" : ""      // the json of the face redaction with offset
        },
    "ocr":
        {
        "json" : "",      // the json of the Ocr
        "jsonOffset" : ""      // the json of Ocr with offset
        },
    "videoAnnotation":
        {
        "json" : "",      // the json of the Video Annotator
        "jsonOffset" : ""      // the json of Video Annotator with offset
        }
 }
```

## return-subtitles

This function returns subtitles from an asset.
```c#
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
```

## set-media-ru

This function sets the number and speed of media reserved units in the account.

```c#
Input:
{
    "ruCount" : "+1", // can be a number like "1", or a number with + or - to increase or decrease the number. Example :  "+2" or "-3"
    "ruSpeed" : "S1"  // can be "S1", "S2" or "S3"
}

Output:
{
    "success" : "True", // return if operation is a success or not
    "maxRu" : 10,       // number of max units
    "newRuCount" : 3,   // new count of units
    "newRuSpeed" : "S2" // new speed of units
}
```

## start-asset-copy-to-container

This function copy the asset files to a specific azure storage container.

```c#
Input:
{
    "assetId" : "the Id of the source asset",
    "targetStorageAccountName" : "",
    "targetStorageAccountKey": "",
    "targetContainer" : "",
    "startsWith" : "video", //optional, copy only files that start with name video
    "endsWith" : ".mp4", //optional, copy only files that end with .mp4
}
Output:
{
}
```

## start-blob-copy-to-asset

This function copy a file (blob) or several blobs to a new asset previously created.

```c#
Input:
{
    "assetId" : "the Id of the asset where the file must be copied",
    "fileName" : "filename.mp4", // use fileName if one file, or FileNames if several files
    "fileNames" : [ "filename.mp4" , "filename2.mp4"],
    "sourceStorageAccountName" : "",
    "sourceStorageAccountKey": "",
    "sourceContainer" : "",
    "wait" : true // optional. Set this parameter if you want the function to wait up to 15s if the fileName blob is missing. Otherwise it does not wait. I applies to fileName, not for fileNames 
}
Output:
{
    "destinationContainer": "asset-2e26fd08-1436-44b1-8b92-882a757071dd" // container of asset
    "missingBob" : "True" // True if one of the source blob(s) is missing
}
```

## submit-job

This function submits a job wth encoding and/or analytics.

```c#
Input:
{
    "assetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Mandatory, Id of the source asset
    "mes" :                 // Optional but required to encode with Media Encoder Standard (MES)
    {
        "preset" : "Content Adaptive Multiple Bitrate MP4", // Optional but required to encode with Media Encoder Standard (MES). If MESPreset contains an extension "H264 Multiple Bitrate 720p with thumbnail.json" then it loads this file from ..\Presets
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    }
    "mesThumbnails" :      // Optional but required to generate thumbnails with Media Encoder Standard (MES)
    {
        "start" : "{Best}",  // Optional. Start time/mode. Default is "{Best}"
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    }
    "mepw" :                // Optional but required to encode with Premium Workflow Encoder
    {
        "workflowAssetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Required. Id for the workflow asset
        "workflowConfig"  : "",                                                  // Optional. Premium Workflow Config for the task
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "indexV1" :             // Optional but required to index audio with Media Indexer v1
    {
        "language" : "English", // Optional. Default is "English"
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "indexV2" :             // Optional but required to index audio with Media Indexer v2
    {
        "language" : "EnUs", // Optional. Default is EnUs
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "ocr" :             // Optional but required to do OCR
    {
        "language" : "AutoDetect", // Optional (Autodetect is the default)
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "faceDetection" :             // Optional but required to do Face Detection
    {
        "mode" : "PerFaceEmotion", // Optional (PerFaceEmotion is the default)
        "outputStorage" : "amsstorage01" // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
    "faceRedaction" :             // Optional but required to do Face Redaction
    {
        "mode" : "analyze"                  // Optional (analyze is the default)
        "outputStorage" : "amsstorage01"    // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
     "motionDetection" :             // Optional but required to do Motion Detection
    {
        "level" : "medium",                 // Optional (medium is the default)
        "outputStorage" : "amsstorage01"    // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
     "summarization" :                      // Optional but required to do Motion Detection
    {
        "duration" : "0.0",                 // Optional (0.0 is the default)
        "outputStorage" : "amsstorage01"    // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
     "hyperlapse" :             // Optional but required to do Motion Detection
    {
        "speed" : "8", // Optional (8 is the default)
        "outputStorage" : "amsstorage01"    // Optional. Storage account name where to put the output asset (attached to AMS account)
    },
     "videoAnnotation" :             // Optional but required to do Video Annotator
    {
        "outputStorage" : "amsstorage01"    // Optional. Storage account name where to put the output asset (attached to AMS account)
    },

    // General job properties
    "priority" : 10,                            // Optional, priority of the job
    "useEncoderOutputForAnalytics" : true,      // Optional, use generated asset by MES or Premium Workflow as a source for media analytics
    "jobName" : ""                              // Optional, job name  

    // For compatibility only with old workflows. Do not use anymore!
    "mesPreset" : "Adaptive Streaming",         // Optional but required to encode with Media Encoder Standard (MES). If MESPreset contains an extension "H264 Multiple Bitrate 720p with thumbnail.json" then it loads this file from ..\Presets
    "workflowAssetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Optional, but required to encode the asset with Premium Workflow Encoder. Id for the workflow asset
    "workflowConfig"  : ""                      // Optional. Premium Workflow Config for the task
    "indexV1Language" : "English",              // Optional but required to index the asset with Indexer v1
    "indexV2Language" : "EnUs",                 // Optional but required to index the asset with Indexer v2
    "ocrLanguage" : "AutoDetect" or "English",  // Optional but required to do OCR
    "faceDetectionMode" : "PerFaceEmotion,      // Optional but required to trigger face detection
    "faceRedactionMode" : "analyze",            // Optional, but required for face redaction
    "motionDetectionLevel" : "medium",          // Optional, required for motion detection
    "summarizationDuration" : "0.0",            // Optional. Required to create video summarization. "0.0" for automatic
    "hyperlapseSpeed" : "8"                     // Optional, required to hyperlapse the video
}

Output:
{
    "jobId" :  // job id
    "otherJobsQueue" = 3 // number of jobs in the queue
    "mes" : // Output asset generated by MES (if mesPreset was specified)
        {
            assetId : "",
            taskId : ""
        },
    "mesThumbnails" :// Output asset generated by MES
        {
            assetId : "",
            taskId : ""
        },
    "mepw" : // Output asset generated by Premium Workflow Encoder
        {
            assetId : "",
            taskId : ""
        },
    "indexV1" :  // Output asset generated by Indexer v1
        {
            assetId : "",
            taskId : "",
            language : ""
        },
    "indexV2" : // Output asset generated by Indexer v2
        {
            assetId : "",
            taskId : "",
            language : ""
        },
    "ocr" : // Output asset generated by OCR
        {
            assetId : "",
            taskId : ""
        },
    "faceDetection" : // Output asset generated by Face detection
        {
            assetId : ""
            taskId : ""
        },
    "faceRedaction" : // Output asset generated by Face redaction
        {
            assetId : ""
            taskId : ""
        },
     "motionDetection" : // Output asset generated by motion detection
        {
            assetId : "",
            taskId : ""
        },
     "summarization" : // Output asset generated by video summarization
        {
            assetId : "",
            taskId : ""
        },
     "hyperlapse" : // Output asset generated by Hyperlapse
        {
            assetId : "",
            taskId : ""
        },
    "videoAnnotation" :// Output asset generated by Video Annotator
        {
            assetId : "",
            taskId : ""
        }
 }
```


## sync-asset

This function declares the asset files in the AMS asset based on the blobs in the asset container.

```c#
Input:
{
    "assetId" : "the Id of the asset"
}

Output:
{}
```