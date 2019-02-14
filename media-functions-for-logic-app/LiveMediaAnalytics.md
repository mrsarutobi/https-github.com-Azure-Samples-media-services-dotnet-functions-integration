# Near real time media analytics with Video Indexer

Use Video Indexer to process a live stream and display the data in a test player !

![Test Player](images/live-media-analytics-player1.png?raw=true)

This solution uses Azure functions and two Logic apps to process a live program (from a live channel in Azure Media Services) with Video Indexer v2, and display the result with Azure Media Player playing the live stream.

The Logic apps workflow does the following :

**Step 1 Logic app**
* it runs every 60 seconds
* subclips the last minute
* sends this subclip asset to Video Indexer, which runs in a Media Services Account (recommended)

![Screen capture](images/logicapp6-live1.png?raw=true)

**Step 2 Logic app**

* called by Video Indexer when indexing is complete (using a callback url)
* gets the insights, update the timestamps
* sends this data to a Cosmos database
* deletes the Video Indexer video and the subclip asset

![Screen capture](images/logicapp6-live2.png?raw=true)

## Step by step configuration

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


### 5. Deploy Video Indexer to run in your AMS account
Use the "Connect" button to Azure in Video Indexer portal. It should be the same Media Services account than the one used by the functions (and the live steam).

### 6. Subscribe to Video Indexer API

### 7. Create a Cosmos database and collection
By default, the template is configured to use a database named "vidb" and a collection named "vicol". So please create such database and collection. Use "/date" as the partition key for the collection.
Create a settings 'CosmosDBConnectionString' in the Azure functions app settings and store in it the Cosmos DB Connection string. It is used by the function to retrieve the insights and pass them to the player.

### 8. AMS configuration and operations
Use AMS v2 (API, Azure portal or AMSE for v2).
Create a channnel "Channel1" and program "Program1" in the Media Services account used by the functions. Start them. Connect a live encoder (for example, Wirecast) and push the live stream to the channel. If you want to use another name for the channel and program, then you will have to edit the step 1 logic app to reflect the new names.

Important : setup 10 S3 media reserved units in the Media Services account.

### 9. Logic apps deployment
Deploy the two logic apps using this template:

Click the button to deploy the template in your subscription:
<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fmedia-services-dotnet-functions-integration%2Fmaster%2Fmedia-functions-for-logic-app%2Flogicapp6-livevideoindexer-deploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

Once deployed, fix the errors in both logic apps (go to designer):
- Video Indexer components (select the location and subscription in all Video Indexer connectors)
- Check the Cosmos DB components and connection

### 10. Setup the test player
A sample html player is [provided here](LiveMediaAnalyticsPlayer.html).
You need to download it, edit it and publish it on a web server.
Editing must be done to change the following links:
- update the media player source url to use your custom live program URL (search for '<source src='),
- specify the URL of the Azure function **query-cosmosdb-insights** ('var functionquerycosmosdbinsights =')
- specify the location of the Video Indexer ('var videoindexerregion = ')

Make sure that you add * to the CORS configuration of the Azure function deployment.

## Notes

* You can to customize the channel name, program name and language of the audio. To do so, change the parameters in the live-subclip-analytics function call and  video indexer upload component from the step1 logic app.
* to increase the performance, it is recommended to limit the resolution of the live stream. This will speed up the processing of Video Indexer. For example, start testing by sending a SD resolution stream (example: 854x480)
* monitor the job queue(s) and allocate the right number of S3 media reserved units  

![Screen capture](images/logicapp6-live-param1.png?raw=true)

![Screen capture](images/logicapp6-live-param2.png?raw=true)

## Functions documentation
This [page](Functions-documentation.md) lists the functions available and describes the input and output parameters.