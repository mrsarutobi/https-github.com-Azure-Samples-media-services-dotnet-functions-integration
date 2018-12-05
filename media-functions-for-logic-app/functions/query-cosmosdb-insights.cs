/*

Azure Media Services REST API v2 Function
 
This function gets Video Indexer insights from Cosmos DB

POST:
https://{subdomain}.azurewebsites.net/api/query-cosmosdb-insights/{dbname/{colname}/{starttime}
- dbname: Name of the database (f.e. "vidb")
- colname: Name of the collection (f.e. "vicol")
- starttime: Time when insight was generated (f.e. "2.23:03:48")

Returns one single JSON with Video Indexer insights for the specified time
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace media_functions_for_logic_app.functions
{
    public static class query_cosmosdb_insights
    {
        [FunctionName("query-cosmosdb-insights")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "query-cosmosdb-insights/{dbname}/{colname}/{starttime}")]HttpRequestMessage req,
            string starttime,
            [DocumentDB(
                databaseName: "{dbname}",
                collectionName: "{colname}",
                ConnectionStringSetting = "CosmosDBConnectionString",
                SqlQuery = "SELECT TOP 1 * from c WHERE c.insights[0].insights.transcript[0].instances[0].adjustedStart <= {starttime} ORDER BY c.insights[0].insights.transcript[0].instances[0].adjustedStart DESC")] IEnumerable<dynamic> results,
            TraceWriter log
            )
        {
            JObject result = results.First();
            
            return req.CreateResponse(HttpStatusCode.OK, result);
        }
    }
}
