# Rosette for Elasticsearch + NEST
Using the Basis Technology Elasticsearch RNI plugin with NEST can be a bit confusing due to the use of custom types, rni-name and rni-date, and a custom function_score, name_score.

The code in this repo provides an example of how to use NEST's lowlevel and raw functions to properly map a class to the RNI types and to perform a name_score query.
### Requirements
- Elasticsearch 5.3
- Rosette for Elasticsearch plugin and license (see [Basis Technology](https://www.rosette.com/elastic/))
- Nuget NEST 5.3.1
- Nuget Elasticsearch.Net 5.3.1

### Code
- Program.cs - simple console application that demonstrates a working name_score query
- RNIBase.cs - base class that provides the mapping from String -> rni_name and DateTime -> rni_date

