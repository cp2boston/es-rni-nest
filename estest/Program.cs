using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Nest;

using rni_name = System.String; // For readability only, it has no underlying effect on results

namespace estest {
    class Program {
        static void Main(string[] args) {
            string indexName = "rni-test";
            var node = new Uri("http://10.1.9.206:32773");
            var settings = new ConnectionSettings(node)
                .DefaultIndex(indexName);
            var client = new ElasticClient(settings);



            var createResponse = client.CreateIndex(indexName);

            // Map custom type
            // Since the RNI plugin requires a custom type, rni_name, and the fluent NEST model doesn't appear to 
            // have a way to map a custom type, use the LowLevel feature of NEST to apply the correct mapping.
            // This only needs to be done once.
            string body = @"{""name_type"": {""properties"": {""primary_name"": {""type"": ""rni_name""}}}}";
            var mapResponse = client.LowLevel.IndicesPutMapping<Record>(indexName, "name_type", body);

            // Add the sample record
            Record record = new estest.Program.Record {
                Id = "1",
                primary_name = "Joe Schmoe",
                aka = "Joe the Schmoe",
                occupation = "Longshoreman"
            };

            var indexResponse = client.Index(record);

            // This is here to make sure that the record was added.  It's not necessary for operation.
            var getResponse = client.Get<Record>(1, idx => idx.Index(indexName));

            // The search is the other area in which a lowlevel query is needed.  Rather than use LowLevel exclusively,
            // I opted to use the Query.Raw feature to specify the custom function_score, name_score.

            string customFunctionScoreQuery = @"{ ""function_score"": { ""name_score"": { ""field"": ""primary_name"", ""query_name"": ""Jo Shmoe""} } }";
            var searchResponse = client.Search<Record>(s => s
                .From(0)
                .Size(100)
                .Query(q => q
                    .Match( m => m.Field( f => f.primary_name).Query("Joe Schmoe") )
                        
                )
                .Rescore( rq => rq
                    .WindowSize(200)
                    .RescoreQuery( q => q
                        .QueryWeight(0.0)
                        .RescoreQueryWeight(1.0)
                        .Query( qy => qy
                            .Raw(customFunctionScoreQuery)  // Raw required for custom function_score query
                        )
                    )
                )
                    
            );

            System.Diagnostics.Debug.WriteLine(searchResponse.ToString());

            // cleanup
            client.DeleteIndex(indexName);
        }
        /// <summary>
        /// Sample record to use for testing.  Note that "rni_name" is just an alias for string.  Under the hood
        /// it gets resolved to string, so its only value is readability.
        /// </summary>
        public class Record {
            public string Id { get; set; }
            public rni_name primary_name { get; set; }
            public rni_name aka { get; set; }
            public string occupation { get; set; }
        }
        
    }
}
