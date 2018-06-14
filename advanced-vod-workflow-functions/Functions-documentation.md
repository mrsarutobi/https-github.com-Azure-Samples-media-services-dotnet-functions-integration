---
services: media-services,functions,logic-app
platforms: dotnet
author: shigeyf
---

# Functions documentation

This section lists the functions available and describes the input and output parameters.
This Functions example is based on AMS REST API v2 and pre-compiled functions.


## AddAssetFile
This function adds asset files to the asset.

```c#
Input:
    {
        "assetId":                              // Id of the asset for copy destination
            "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",
        "primaryFileName": "filename.mp4",      // File name of the primary AssetFile in the asset
        "fileNames":                            // (Optional) File names of copy target contents
            [ "filename.mp4" , "filename2.mp4" ]
    }
Output:
    {
        "assetId":                              // Id of the asset for copy destination
            "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810"
    }

```

## ApplyDynamicEncryption
This function applies Dynamic Encryption to the asset.

[contentKeyType](https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.mediaservices.client.contentkeytype?view=azure-dotnet) can be passed with the following values:
* CommonEncryption
* CommonEncryptionCbcs
* EnvelopeEncryption

```c#
Input:
    {
        "assetId":                              // Id of the asset for copy destination
            "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",
        "contentKeyAuthorizationPolicyId":      // Id of the ContentKeyAuthorizationPolicy object
                    "nb:ckpid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",
        "assetDeliveryPolicyId":                // Id of the AssetDeliveryPolicy object
                    "nb:adpid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",
        "contentKeyType": "CommonEncryption",   // Name of the ContentKeyType
        "contentKeyName":                       // (Optional) Name of the ContentKey object
            "Common Encryption ContentKey"
    }
Output:
    {
        "contentKeyId":                         // Id of the ContentKey object
            "nb:kid:UUID:489a97f4-9a31-4174-ac92-0c76e8dbdc06"
    }

```

## CreateAccessPolicy
This function creates AccessPolicy object.
accessDuration must be followed with the specific format (d:hh:mm:ss.fffffff). You can see more details of the format: [long general format = "G"](https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-timespan-format-strings#the-general-long-g-format-specifier)

```c#
Input:
    {
        "accessPolicyName":                     // Name of the access policy
            "1-day StreamingPolicy",
        "accessDuration": "1:00:00:00.0000000"  // Duration of time span
    }
Output:
    {
        "accessPolicyId":                       // Id of the access policy
            "nb:pid:UUID:a8bc8819-c2cf-4b7a-bc54-e3667f92dc80"
    }

```

## CreateAssetDeliveryPolicy
This function creates new AssetDeliveryPolicy object in the AMS account.
[assetDeliveryPolicyType](https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.mediaservices.client.dynamicencryption.assetdeliverypolicytype?view=azure-dotnet) can be passed with the following values:
* NoDynamicEncryption
* DynamicEnvelopeEncryption
* DynamicCommonEncryption
* DynamicCommonEncryptionCbcs

[assetDeliveryPolicyProtocol](https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.mediaservices.client.dynamicencryption.assetdeliveryprotocol?view=azure-dotnet) can be passed with the following values:
* SmoothStreaming
* Dash
* HLS
* Hds
* ProgressiveDownload
* All

assetDeliveryPolicyContentProtection can be passed with the following values:
* AESClearKey
* PlayReady
* Widevine
* FairPlay

```c#
Input:
    {
        "assetDeliveryPolicyName":              // Name of the AssetDeliveryPolicy object
            "Clear Policy",
        "assetDeliveryPolicyType":              // Type of the AssetDeliveryPolicy object
            "NoDynamicEncryption",
        "assetDeliveryPolicyProtocol": [
            "SmoothStreaming",
            "Dash",
            "HLS"
        ],
        "assetDeliveryPolicyContentProtection": [ // (Optional) List of the content protection technology
            "PlayReady",
            "Widevine"
        ],
        "fairPlayContentKeyAuthorizationPolicyOptionId":
                                                // (Optional) Id of tContent Key Authorization Policy Option for FairPlay
            "nb:ckpoid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810"
    }
Output:
    {
        "assetDeliveryPolicyId":                // Id of the AssetDeliveryPolicy object
            "nb:adpid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810"
    }

```

## CreateContentKeyAuthorizationPolicy
This function creates new ContentKeyAuthorizationPolicy object in the AMS account.

```c#
Input:
    {
        "contentKeyAuthorizationPolicyName":    // Name of the ContentKeyAuthorizationPolicy object
            "Open CENC Key Authorization Policy",

        "contentKeyAuthorizationPolicyOptionIds": [
                                                // List of the ContentKeyAuthorizationPolicyOption Identifiers
            "nb:ckpoid:UUID:68adb036-43b7-45e6-81bd-8cf32013c821",
            "nb:ckpoid:UUID:68adb036-43b7-45e6-81bd-8cf32013c822"
        ]
    }
Output:
    {
        "contentKeyAuthorizationPolicyId":      // Id of the AssetDeliveryPolicy object
            "nb:ckpid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",
    }

```

## CreateEmptyAsset
This function creates an empty asset.
[assetCreationOption](https://docs.microsoft.com/en-us/rest/api/media/operations/asset#asset_entity_properties) can be passed with the following values:
* None: Normal asset type (no encryption)
* StorageEncrypted: Storage Encryption encrypted asset type
* CommonEncryptionProtected: Common Encryption encrypted asset type
* EnvelopeEncryptionProtected: Envelope Encryption encrypted asset type

```c#
Input:
    {
        "assetName": "Asset Name",              // Name of the asset
        "assetCreationOption": "None",          // (Optional) Name of asset creation option
        "assetStorageAccount": "storage01"      // (Optional) Name of attached storage account where to create the asset
    }
Output:
    {
        "assetId": "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810"   // Id of the asset created
        "destinationContainer":                 // Container Name of the asset for copy destination
            "asset-2e26fd08-1436-44b1-8b92-882a757071dd"
    }

```
## GetCaptionBlobSasUri
This function gets caption data URI.

```c#
Input:
    {
        "assetId":                              // Id of the asset for copy destination
            "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",
        "timecodeOffset": "00:01:00"            // (Optional) Offset to add to captions
    }
Output:
    {
        "vttBlobSasUri":                        // URI of VTT Blob with SAS token
            "https://mediademostorage.blob.core.windows.net/asset-67f7e54a-4141-4e30-becf-3f508fbdd85f/HoloLensDemo_aud_SpReco.vtt?sv=2017-04-17&sr=b&sig=EFk1BMbk4QveTXuXS8HS065fB76%2FjX90aeIrSzh8d5I%3D&st=2018-06-12T14%3A06%3A40Z&se=2018-06-12T14%3A21%3A40Z&sp=r",
        "ttmlBlobSasUri":                       // URI of TTML Blob with SAS token
            "https://mediademostorage.blob.core.windows.net/asset-67f7e54a-4141-4e30-becf-3f508fbdd85f/HoloLensDemo_aud_SpReco.ttml?sv=2017-04-17&sr=b&sig=EFk1BMbk4QveTXuXS8HS065fB76%2FjX90aeIrSzh8d5I%3D&st=2018-06-12T14%3A06%3A40Z&se=2018-06-12T14%3A21%3A40Z&sp=r"
    }

```

## MonitorBlobContainerCopyStatus
This function monitors blob copy.
[blobCopyStatus](https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.storage.blob.copystatus?view=azure-dotnet) is returned with the following values:
* 0 : Invalid - The copy status is invalid.
* 1 : Pending - The copy operation is pending.
* 2 : Success - The copy operation succeeded.
* 3 : Aborted - The copy operation has been aborted.
* 4 : Failed - The copy operation encountered an error.

```c#
Input:
    {
        "storageAccountName": "amsstorage",     // Storage account name of the asset for copy destination
        "destinationContainer":                 // Container Name of the asset for copy destination
            "asset-2e26fd08-1436-44b1-8b92-882a757071dd",
        "fileNames":                            // (Optional) File names of copy target contents
            [ "filename.mp4" , "filename2.mp4"]
    }
Output:
    {
        "copyStatus": true|false,               // Return Blob Copy Status: true or false
        "blobCopyStatusList": [
            {
                "blobName": "filename.mp4",     // Name of the Blob
                "blobCopyStatus": 2             // Return Blob CopyStatus (see below)
            },
            {
                "blobName": "filename2.mp4",    // Name of the Blob
                "blobCopyStatus": 2             // Return Blob CopyStatus (see below)
            }
        ]
    }

```

## MonitorMediaJob
This function monitors media job.
[jobState](https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.mediaservices.client.jobstate?view=azure-dotnet) is returned with the following values:
* 0 : Queued
* 1 : Scheduled
* 2 : Processing
* 3 : Finished
* 4 : Error
* 5 : Canceled
* 6 : Canceling

```c#
Input:
    {
        "jobId":                                // Id of the media job
            "nb:jid:UUID:1904c0ff-0300-80c0-9cb2-f1e868091e81"
    }
Output:
    {
        "jobState": 0                           // Status code of the media job
    }

```

## PublishAsset
This function publishes asset.
startDateTime must be followed with the specific format ("yyyy'-'MM'-'dd HH':'mm':'ss'Z'"). You can see more details of the [format](https://msdn.microsoft.com/en-us/library/system.globalization.datetimeformatinfo(v=vs.110).aspx).

```c#
Input:
    {
        "assetId":                              // Id of the asset to be published
            "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",
        "accessPolicyId":                       // Id of the access policy
            "nb:pid:UUID:a8bc8819-c2cf-4b7a-bc54-e3667f92dc80",   
        "startDateTime": "2018-12-31 00:00:00Z" // (Optionnal) Start date of publishing
    }
Output:
    {
    }

```

## StartBlobContainerCopyToAsset
This function starts copying blob container to the asset.

```c#
Input:
    {
        "assetId":                              // Id of the asset for copy destination
            "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",
        "sourceStorageAccountName":             // Name of the storage account for copy source
            "mediaimports",
        "sourceStorageAccountKey":  "xxxkey==", // Key of the storage account for copy source
        "sourceContainer":  "movie-trailer",    // Blob container name of the storage account for copy source
        "fileNames":                            // (Optional) File names of source contents
            [ "filename.mp4" , "filename2.mp4" ]
    }
Output:
    {
        "destinationContainer":                 // Container Name of the asset for copy destination
            "asset-2e26fd08-1436-44b1-8b92-882a757071dd"
    }

```

## SubmitMediaJob
This function submits media job.
configuration string can be set with Base64 encoded text data which starts with "base64,".

```c#
Input:
    {
        "assetId":                              // Id of the asset for copy destination
            "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c810",
        "mediaTasks" [
            {
                "mediaTaskName": "MediaEncoding",           // Name of the media task
                "mediaProcessor": "Media Encoder Standard", // Name of Media Processor for the media task
                "configuration": "configuration string",    // Configuration parameter of Media Processor
                "additionalInputAssetIds": [                // (Optional) Id list of additional input assets
                    "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c811",
                    "nb:cid:UUID:68adb036-43b7-45e6-81bd-8cf32013c812"
                ],
                "outputStorageAccount": "amsstorage01"      // (Optional) Name of the Storage account where to put the output asset (attached to AMS account)
            },
            {
                ...
            }
        ],
        "jobPriority" : 10,                     // (Optional) Priority of the media job
        "jobName" : "Azure Function Media Job"  // (Optional) Name of the media job
    }
Output:
    {
        "jobId":                                // Id of the media job
            "nb:jid:UUID:1904c0ff-0300-80c0-9cb2-f1e868091e81",
        "mediaTaskOutputs" [
            {
                "mediaTaskIndex": 0,            // Index of the media task
                "mediaTaskId":                  // Id of the media task
                    "nb:tid:UUID:1904c0ff-0300-80c0-9cb3-f1e868091e81",
                "mediaTaskName":                // Name of the nedia task
                    "Azure Functions: Task for Media Encoder Standard",
                "mediaProcessorId":             // Id of Media Processor for the media task
                    "nb:mpid:UUID:ff4df607-d419-42f0-bc17-a481b1331e56",
                "mediaTaskOutputAssetId":       // Id of the output asset for the media task
                    "nb:cid:UUID:8739680c-2708-4cb9-b8f1-300c41a92423"
            },
            {
                ...
            }
        ]
    }

```