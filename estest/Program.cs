using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nest;

namespace estest {
    class Program {
        static void Main(string[] args) {
            string indexName = "rni-test";
            string esHostURL = "http://10.1.9.206:32773"; // Set to the host:port of elasticsearch
            var node = new Uri(esHostURL);
            var settings = new ConnectionSettings(node)
                .DefaultIndex(indexName);
            var client = new ElasticClient(settings);



            var createResponse = client.CreateIndex(indexName);

            // Create a sample record
            Record sampleRecord = new estest.Program.Record {
                Id = "1",
                FullName = "Joe Schmoe",
                LocalName = "Joe the Schmoe",
                DateOfBirth = new DateTime(1980, 11, 11)
            };

            // Map custom type
            // Since the RNI plugin requires a custom type, rni_name, and the fluent NEST model doesn't appear to 
            // have a way to map a custom type, use the LowLevel feature of NEST to apply the correct mapping.
            // This only needs to be done once.
            // NOTE: The type, e.g. Record and the second argument, Type, should match in case.  Why the IndicesPutMapping
            //       method can't figure out the type from the generic is a mystery.
            var mapResponse = client.LowLevel.IndicesPutMapping<Record>(indexName, "Record", sampleRecord.MapToRNITypes());



            var indexResponse = client.Index(sampleRecord);

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
        /// Sample record to use for testing.  Derive from RNIBase to use the
        /// custom type mapping method.
        /// </summary>
        public class Record : RNIBase {
            public string Id { get; set; }
            public string FullName { get; set; }
            public string LocalName { get; set; }
            public DateTime DateOfBirth { get; set; }
        }
        
    }
}
