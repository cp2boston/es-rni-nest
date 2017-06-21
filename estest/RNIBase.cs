using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace estest {
    /// <summary>
    /// RNIBase provides a base class that provides RNI specific methods to inherited classes.  Specifically, it:
    ///     - provides a JSON formatter to properly format the public properties of a child class for use in
    ///       LowLevel mapping of the schema.
    /// </summary>
    public class RNIBase {
        public RNIBase() { }

        /// <summary>
        /// Returns a properly formatted JSON string that can be used for LowLevel NEST mapping
        /// 
        /// </summary>
        /// <returns>Properly formatted JSON string</returns>
        public string MapToRNITypes() {
            var className = this.GetType().Name;
            var publicProperties = this.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            // Building the JSON using Newtonsoft is the safest way to ensure that the resulting JSON is correctly formatted
            JObject body = new JObject(
                new JProperty(className,
                    new JObject(
                        new JProperty("properties",
                            new JObject (
                            from property in publicProperties //.Where( t=> !t.Name.Contains("Id") )
                                select new JProperty(property.Name, new JObject(new JProperty("type", RniType(property.PropertyType.Name))))
                            )
                        )
                    )
                )
            );
            return body.ToString();
        }

        /// <summary>
        /// Maps the standard type to the RNI Type
        /// </summary>
        /// <param name="propertyType">Standard type</param>
        /// <returns>RNI type</returns>
        private string RniType(string propertyType) {
            Dictionary<string, string> rniTypes = new Dictionary<string, string>() {
                { "String", "rni_name" },
                { "DateTime", "rni_date" }
            };

            if (rniTypes.ContainsKey(propertyType)) {
                return rniTypes[propertyType];
            }
            else {
                return propertyType;
            }
        }
    }
}
