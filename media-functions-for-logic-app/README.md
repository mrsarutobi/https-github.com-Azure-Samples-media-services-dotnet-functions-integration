# Logic Apps which use Azure Functions and Azure Media Services

## Video presentation about serverless video workflows

[![Watch the presentation](images/player-serverless.png?raw=true)](https://aka.ms/ampembed?url=https%3A%2F%2Fxpouyatdemo.streaming.mediaservices.windows.net%2F1e504b53-e1b3-45c3-89ea-9bc01975c3c6%2Fconnect2017-v3.ism%2Fmanifest)

## Prerequisites for all Logic Apps deployments

### 1. Create an Azure Media Services account

Create a Media Services account in your subscription if don't have it already.

### 2. Create a Service Principal

Create a Service Principal and save the password. It will be needed in step #4. To do so, go to the API tab in the account ([follow this article](https://docs.microsoft.com/en-us/azure/media-services/media-services-portal-get-started-with-aad#service-principal-authentication))

### 3. Make sure the AMS streaming endpoint is started

To enable streaming, go to the Azure portal, select the Azure Media Services account which has been created, and start the default streaming endpoint.

![Screen capture](images/start-se-1.png?raw=true)

![Screen capture](images/start-se-2.png?raw=true)

### 4. Deploy the Azure functions
If not already done : fork the repo, deploy Azure Functions and select the **"media-functions-for-logic-app"** Project (IMPORTANT!)

Follow the guidelines in the [git tutorial](1-CONTRIBUTION-GUIDE/git-tutorial.md) for details on how to fork the project and use Git properly with this project.

Note : if you never provided your GitHub account in the Azure portal before, the continous integration probably will probably fail and you won't see the functions. In that case, you need to setup it manually. Go to your azure functions deployment / Functions app settings / Configure continous integration. Select GitHub as a source and configure it to use your fork.

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fmedia-services-dotnet-functions-integration%2Fmaster%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>



## First Logic App : A Simple VOD workflow

### Presentation

This template creates a Logic app that listens to an onedrive folder and will copy it to an Azure Media Services asset, triggers an encoding job, publish the output asset and send an email when the process is complete.

![Screen capture](images/logicapp1-simplevod-1.png?raw=true)
![Screen capture](images/logicapp1-simplevod-2.png?raw=true)

[See the detailed view of the logic app.](logicapp1-simplevod-screen.md)



### 1. Deploy the logic app

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fmedia-services-dotnet-functions-integration%2Fmaster%2Fmedia-functions-for-logic-app%2Flogicapp1-simplevod-deploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

![Screen capture](images/form-logicapp1-simplevod.png?raw=true)

It is recommended to use the same resource group for the functions and the logic app.
The functions and Logic App must be deployed in the same region.
Please specify the name of the storage account used by Media Services.

### 2. Fix the connections and errors

When deployed, go to the Logic App Designer and fix the connections (Onedrive, Outlook.com...). Make sure to (re)select the OneDrive folder that you want to use for the ingest.


## Second Logic App : using the Azure Storage trigger

This is the same workflow that the first logic app with two main differences:
- the source is monitored using blob trigger (new file coming to an Azure Storage container)
- the asset creation / blob copy is done through Azure functions to workaround the limitation of 50 MB. These functions have been tested with 1.8 GB files.

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fmedia-services-dotnet-functions-integration%2Fmaster%2Fmedia-functions-for-logic-app%2Flogicapp2-simplevod-storage-deploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

![Screen capture](images/logicapp2-1.png?raw=true)

## Third Logic App : An advanced VOD workflow

This template creates a Logic app which

* monitors a container in Azure Storage (blob trigger),
* copies new file to an Azure Media Services asset,
* triggers an encoding job,
* converts the English audio to text (using Media Indexer v2),
* translates the English subtitles to French (using Bing translator),
* copies back the French subtitles to the subtitles asset,
* publishes the output assets,
* generates a short playback URL (using bitlink)
* sends an email with Outlook when the process is complete or if the job failed. In the email, the playback link includes the two subtitles.

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fmedia-services-dotnet-functions-integration%2Fmaster%2Fmedia-functions-for-logic-app%2Flogicapp3-advancedvod-deploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

![Screen capture](images/logicapp3-advancedvod-1.png?raw=true)
![Screen capture](images/logicapp3-advancedvod-2.png?raw=true)
![Screen capture](images/logicapp3-advancedvod-3.png?raw=true)

## Fourth Logic App : Live analytics processing

This template creates a Logic app which processes a live program (from a live channel in Azure Media Services) for media analytics. What it does :

* subclips the last minute
* sends this subclip asset to Azure Media Indexer, Motion Detection and Face Redaction processors (3 tasks in one job)
* gets the text, faces and motion detection information and sends this data to a Cosmos database,
* optionnaly copy the faces to a dedicated Azure storage container.

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fmedia-services-dotnet-functions-integration%2Fmaster%2Fmedia-functions-for-logic-app%2Flogicapp4-liveanalytics-deploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

Then fix the errors.

You need to customize the channel name, program name and language of the audio. To do so, change the parameters in the live-subclip-analytics function call.

![Screen capture](images/logicapp4-live-1.png?raw=true)

Notes

* you need to create a Cosmos database prior to the deployment of the logic app. Partition key should be named "processor"
* you should allocate sufficient reserved units in the Media Services account otherwise the job queue will grow over time. Start with 4 S2 reserved units and monitor the queue. 


## Fifth Logic App : Importing pre-encoded assets to Azure Media Services

This template creates a Logic app which

* monitors a container in Azure Storage (blob trigger) for new JSON semaphore files,
  * See an [example of semaphore file](encodedasset0.json) below 
* imports all the video files declared in the semaphore file to a single asset,
  * Note: it waits for all the video files to arrive in the container. If you upload all the files with AZCOPY or Aspera, the video files arrive after the semaphore file given the size, so the need for the wait.
* creates a client manifest in the asset,
* publishes the asset with dynamic packaging for adaptive streaming.


<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fmedia-services-dotnet-functions-integration%2Fmaster%2Fmedia-functions-for-logic-app%2Flogicapp5-preencoded-asset-deploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

![Screen capture](images/logicapp5-1.png?raw=true)
![Screen capture](images/logicapp5-2.png?raw=true)
![Screen capture](images/logicapp5-3.png?raw=true)


Example of [semaphore file](encodedasset0.json) that must be created and uploaded along with the video files.

```json
[
  {
    "fileName": "video-400.mp4"
  },
  {
    "fileName": "video-700.mp4"
  },
  {
    "fileName": "video-1200.mp4"
  }
]
```

## Sixth Logic App : Live stream analysis using Video Indexer

This [page](LiveStreamAnalysis.md) presents a near real time video analytics solution which relies on Video Indexer to process a live stream.

[![Test Player](images/live-media-analytics-player1.png?raw=true)](LiveStreamAnalysis.md)


## Functions documentation
This [page](Functions-documentation.md) lists the functions available and describes the input and output parameters.