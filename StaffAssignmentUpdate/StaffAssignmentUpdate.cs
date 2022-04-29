using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using Microsoft.Xrm.Sdk;
using System.Text;
using System.Runtime.Serialization.Json;
using Newtonsoft.Json.Linq;
using StaffAssignmentUpdate.Controller;

namespace StaffAssignmentUpdate
{
    public static class StaffAssignmentUpdate
    {
        [FunctionName("CreateEmmyEntityHelper")]
        public static async Task Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            var json = await req.Content.ReadAsStringAsync();
            var jObj = JObject.Parse(json);
            log.LogInformation("PreEntity Image : " + jObj["PreEntityImages"].ToString());
            log.LogInformation("PostEntity Image : " + jObj["PostEntityImages"].ToString());

            RemoteExecutionContext cdsContext = GetContext(json);        
            Entity preEntityImage = (Entity)cdsContext.PreEntityImages["PermitImage"];
            
            string preStateCode = preEntityImage.FormattedValues["statecode"].ToString();
            string staffAssignmentId = preEntityImage.Attributes["vit_staffassignmentid"].ToString();
            string preStaffStatus = preEntityImage.Attributes["vit_staffstatus"].ToString();

            Entity postEntityImage = (Entity)cdsContext.PostEntityImages["PermitImage"];
            string postStateCode = postEntityImage.FormattedValues["statecode"].ToString();
            string postStaffStatus = postEntityImage.Attributes["vit_staffstatus"].ToString();

            
            try
            {
                if ((!String.IsNullOrEmpty(preStateCode) && !String.IsNullOrEmpty(postStateCode) && preStateCode == "Active" && postStateCode == "Inactive") || (!String.IsNullOrEmpty(preStaffStatus) && !String.IsNullOrEmpty(postStaffStatus) && preStaffStatus == "Active" && postStaffStatus == "Inactive"))
                {
                    log.LogInformation("Creating record in EmmyEntityHelper");
                    JObject emmyEntityHelperRecord = new JObject {
                            {"vit_operationtype", 909890001},
                            {"vit_name", "StaffStatusUpdate"},
                            {"vit_staffassignmentid", staffAssignmentId},
                            {"vit_operationcompleted", 909890001}
                    };

                    DataController dataController = new DataController();
                    bool success = await dataController.postDataToCRMAsync("vit_emmyentityhelpers", emmyEntityHelperRecord);
                    if (success)
                    {
                        log.LogInformation("Record created in EmmyEntityHelper");
                    }
                    else
                    {
                        log.LogInformation("No Record created.");
                    }
                }
            }

            catch (Exception ex)
            {
                log.LogInformation("Exception Message " + ex.ToString());
            }
        }



        public static RemoteExecutionContext GetContext(string contextJSON)
        {
            RemoteExecutionContext rv = null;
            using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(contextJSON)))
            {
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(RemoteExecutionContext));

                rv = (RemoteExecutionContext)ser.ReadObject(ms);
            }
            return rv;
        }
    }
}