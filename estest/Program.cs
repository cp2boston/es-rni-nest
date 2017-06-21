using System;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Nest;
using Newtonsoft.Json;
using Elasticsearch.Net;

namespace estest {
    class Program {
        static void Main(string[] args) {
            string indexName = "rni-test";
            string esHostURL = "http://10.1.1.169:9200"; // Set to the host:port of elasticsearch
            var pool = new SingleNodeConnectionPool(new Uri(esHostURL));
            // Add a custom JSON serializer to the connection settings in order to force a
            // conforming date format
            var connectionSettings = new ConnectionSettings(pool, settings => new RniJsonNetSerializer(settings))
                .DefaultIndex(indexName)
                // Add the following to make sure that class property names are left as-is,
                // otherwise, they will be camelCased by NEST, causing confusion with the low-level mapping
                .DefaultFieldNameInferrer(p => p);
            var client = new ElasticClient(connectionSettings);

            Console.WriteLine("Checking for existing index");
            if (client.IndexExists(indexName).Exists) {
                Console.WriteLine("Deleting index");
                client.DeleteIndex(indexName);
            }


            Console.WriteLine("Creating Index");
            var createResponse = client.CreateIndex(indexName);

            Console.WriteLine("Creating Record");
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
            Console.WriteLine("Low-Level Map");
            var mapResponse = client.LowLevel.IndicesPutMapping<Record>(indexName, "Record", sampleRecord.MapToRNITypes());


            Console.WriteLine("Indexing");
            var indexResponse = client.Index(sampleRecord);

            // This is here to make sure that the record was added.  It's not necessary for operation.
            Console.WriteLine("Retrieve record as a check");
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

            Console.WriteLine("Perform Search");
            var searchResponse = client.Search<Record>(search => search
                .From(0)
                .Size(100)
                .Query(query => query
                    .Match(m => m.Field(f => f.FullName).Query("Joe Schmoe"))

                )
                .Rescore(fn => new RescoringDescriptor<Record>()
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
                )
            );

            Console.WriteLine(searchResponse.ToString());
            System.Diagnostics.Debug.WriteLine(searchResponse.ToString());

            // cleanup
            Console.WriteLine("Remove Index");
            client.DeleteIndex(indexName);
        }

        /// <summary>
        /// Custom JsonNetSerializer to specify the date format.  RNI currently only supports date, not time, and
        /// the format needs to be in a recognized ElasticSearch format.
        /// </summary>
        public class RniJsonNetSerializer : JsonNetSerializer {
            public RniJsonNetSerializer(IConnectionSettingsValues settings) : base(settings) { }

            protected override System.Collections.Generic.IList<Func<Type, JsonConverter>> ContractConverters => new System.Collections.Generic.List<Func<Type, JsonConverter>> { t => {
                if (t == typeof(DateTime) ||
                    t == typeof(DateTime?) ||
                    t == typeof(DateTimeOffset) ||
                    t == typeof(DateTimeOffset?)) {
                        return new  Newtonsoft.Json.Converters.IsoDateTimeConverter {
                            DateTimeFormat = "yyyy-MM-dd"
                        };
                    }

                    return null;
                }
            };
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
