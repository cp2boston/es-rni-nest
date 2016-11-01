using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

            // Building the JSON using Newtonsoft is the safest way to ensure that the resulting JSON is correctly formatted
            JObject body = new JObject(
                new JProperty("record",
                    new JObject(
                        new JProperty("properties",
                            new JObject(
                                new JProperty("FullName", new JObject( new JProperty("type", "rni_name")) ),
                                new JProperty("LocalName", new JObject( new JProperty("type", "rni_name")) ),
                                new JProperty("DateOfBirth", new JObject( new JProperty("type", "rni_date") ))
                            )
                        )
                    )
                )
            );
            // Use LowLevel to map the custom types
            var mapResponse = client.LowLevel.IndicesPutMapping<Record>(indexName, "record", body.ToString());

            // Add the sample record
            Record record = new estest.Program.Record {
                Id = "1",
                FullName = "Joe Schmoe",
                LocalName = "Joe the Schmoe",
                DateOfBirth = new DateTime(1980, 11, 11)
            };

            var indexResponse = client.Index(record);

            // This is here to make sure that the record was added.  It's not necessary for operation.
            var getResponse = client.Get<Record>(1, idx => idx.Index(indexName));

            // The search is the other area in which a lowlevel query is needed.  Rather than use LowLevel exclusively,
            // I opted to use the Query.Raw feature to specify the custom function_score, name_score.

            //string customQuery = @"{ ""function_score"": { ""name_score"": { ""field"": ""FullName"", ""query_name"": ""Jo Shmoe""} } }";
            // Again, to be safe, build the JSON using Newtonsoft
            JObject customQuery = new JObject(
                new JProperty("function_score",
                    new JObject(
                        new JProperty("name_score",
                            new JObject(
                                new JProperty("field", "FullName"),
                                new JProperty("query_name", "Jo Schmoe")
                            )
                        )
                    )
                )
            );
            var searchResponse = client.Search<Record>(search => search
                .From(0)
                .Size(100)
                .Query(query => query
                    .Match(m => m.Field(f => f.FullName).Query("Joe Schmoe"))

                )
                .Rescore(rescore => rescore
                    .WindowSize(200)
                    .RescoreQuery(rescore_query => rescore_query
                        .QueryWeight(0.0)
                        .RescoreQueryWeight(1.0)
                        .Query(query => query
                            .Raw(customQuery.ToString())  // Raw required for custom function_score query
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
            public rni_name FullName { get; set; }
            public rni_name LocalName { get; set; }
            public DateTime DateOfBirth { get; set; }
        }
        
    }
}
