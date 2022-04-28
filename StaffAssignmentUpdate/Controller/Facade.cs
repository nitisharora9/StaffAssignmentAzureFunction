using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Net;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Text;

namespace StaffAssignmentUpdate.Controller
{
    public class Facade
    {
        public HttpClient Client
        {
            get;
            set;
        }

        public Facade()
        {
            Client = getClient().Result;
        }
        // Connect To Dynamics 365
        public async Task<HttpClient> getClient()
        {
            Console.WriteLine("Starting App getting connection...");
            var organizationUrl = Environment.GetEnvironmentVariable("OrganizationEndpoint");
            var clientId = Environment.GetEnvironmentVariable("ClientId");
            var clientSecret = Environment.GetEnvironmentVariable("ClientSecret");
            var aadInstance = Environment.GetEnvironmentVariable("AadInstance");
            var tenantId = Environment.GetEnvironmentVariable("TenantId");

            // Get Access Token
            string authenticationResult = await AccessTokenGenerator(organizationUrl, clientId, clientSecret, aadInstance, tenantId);

            // Create HttpClient
            HttpClient httpClient = new HttpClient
            {
                BaseAddress = new Uri(Environment.GetEnvironmentVariable("OrganizationEndpoint")),
                Timeout = new TimeSpan(0, 10, 0)
            };
            httpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            httpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authenticationResult);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            // Return HttpClient
            return httpClient;
        }

        // Get Access Token
        private static async Task<string> AccessTokenGenerator(string organizationUrl, string clientId, string clientSecret, string aadInstance, string tenantId)
        {
            // Create Authority by combining 
            string authority = aadInstance + tenantId; // Azure AD App Tenant ID  

            var credentials = new ClientCredential(clientId, clientSecret);
            var authContext = new AuthenticationContext(authority);
            var result = await authContext.AcquireTokenAsync(organizationUrl, credentials);
            return result.AccessToken;
        }

        //Get Data from CRM
        public async Task<JObject> RetrieveMultipleAsync(string collection, string queryString)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"api/data/v9.1/{collection}?$filter={queryString}");
            request.Headers.Add("Prefer", "odata.include-annotations=OData.Community.Display.V1.FormattedValue");

            var response = await Client.SendAsync(request);

            //Console.WriteLine("Request filter: "+ queryString );
            JObject allRecords = new JObject();

            if (response.StatusCode == HttpStatusCode.OK)
            {
                allRecords = JsonConvert.DeserializeObject<JObject>(response.Content.ReadAsStringAsync().Result);

                var rawResult = JsonConvert.DeserializeObject<JObject>(response.Content.ReadAsStringAsync().Result);

                string nextPageurl = null;

                if (rawResult["@odata.nextLink"] != null)
                    nextPageurl = rawResult["@odata.nextLink"].ToString(); //This URI is already encoded.

                while (nextPageurl != null)
                {
                    request = new HttpRequestMessage(HttpMethod.Get, nextPageurl);
                    response = await Client.SendAsync(request);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        allRecords.Merge((JsonConvert.DeserializeObject<JObject>(response.Content.ReadAsStringAsync().Result)), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat });

                        rawResult = JsonConvert.DeserializeObject<JObject>(response.Content.ReadAsStringAsync().Result);

                        if (rawResult["@odata.nextLink"] == null)
                            nextPageurl = null;
                        else
                            nextPageurl = rawResult["@odata.nextLink"].ToString();
                    }
                    else
                        nextPageurl = null;
                }
                return allRecords;
            }
            Console.WriteLine($"Failed to retrieve records from '{collection}' collection.", (object)response.ReasonPhrase);
            return (JObject)null;
        }

        //Update a record

        public async Task<bool> UpdateAsync(string collection, string id, JObject record)
        {
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"api/data/v9.1/{collection}({id})");
            request.Content = new StringContent(record.ToString(), Encoding.UTF8, "application/json");

            var response = await Client.SendAsync(request);

            return (response.StatusCode == HttpStatusCode.NoContent);
        }

        // Create new record
        public async Task<bool> CreateAsync(string collection, JObject record)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"api/data/v9.1/{collection}");
            request.Content = new StringContent(record.ToString(), Encoding.UTF8, "application/json");
            var response = await Client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.NoContent)
                Console.WriteLine($"Record created");
            else
                Console.WriteLine($"Failed to create record.\nReason: {response.ReasonPhrase}");

            return (response.StatusCode == HttpStatusCode.NoContent);
        }

    }
}
