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
using Newtonsoft.Json.Linq;
using StaffAssignmentUpdate.Controller;
using StaffAssignmentUpdate.Model;
using System.Collections.Generic;

namespace StaffAssignmentUpdate
{
    public static class CreateAlert
    {

        [FunctionName("CreateAlert")]
        public static async Task Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            var json = await req.Content.ReadAsStringAsync();
            //log.LogInformation(json);

            var jObject = JObject.Parse(json);
            string staffAssignmentID = jObject["StaffAssignmentID"].ToString();

            log.LogInformation("Fetching Staff Assignment : " + staffAssignmentID);

            try
            {

                DataController dataController = new DataController();

                //fetching staff assignment

                string staffAssignmentFilter = "vit_staffassignmentid eq " + staffAssignmentID;

                JArray staffAssignmentArray = await dataController.getDataFromCRMAsync("vit_staffassignments", staffAssignmentFilter);

                List<StaffAssignmentsModel> staffAssignmentList = new List<StaffAssignmentsModel>();

                if (staffAssignmentArray != null && staffAssignmentArray.Count > 0)
                {
                    foreach (var staffAssignment in staffAssignmentArray)
                    {
                        StaffAssignmentsModel saModel = new StaffAssignmentsModel();

                        saModel.staffStatus = staffAssignment["vit_staffstatus"].ToString();
                        saModel.status = staffAssignment["statecode"].ToString();
                        saModel.patientID = staffAssignment["_vit_patient_value"].ToString();
                        saModel.empDiscipline = staffAssignment["vit_empdiscipline"].ToString();
                        saModel.patientPrimary = staffAssignment["vit_patprimary"].ToString();

                        staffAssignmentList.Add(saModel);
                    }
                }

                foreach (var staffAssignment in staffAssignmentList)
                {
                    if(staffAssignment.status == "1")
                    {
                        log.LogInformation("No New Assignment during 4 hours");

                        //checking if Team Manager exists or not 

                        List<TeamManagerModel> tmgList = await getTMG(staffAssignment.patientID, dataController, log);
                       
                        if(isTMGExists(tmgList))
                        {
                            //create Alert
                            log.LogInformation("Creating Alert");

                            foreach (var tmg in tmgList)
                            {
                                bool success = await createNoCoreTeamAssignmentAlert(dataController, tmg.contactID, staffAssignment.patientID, staffAssignment.empDiscipline);
                                
                                if(success)
                                {
                                    log.LogInformation("Success! Alert created!");
                                }

                                else
                                {
                                    log.LogInformation("Something went wrong! Alert not created!");
                                }
                            }                        
                        }
                   }
                }
            }

            catch (Exception ex)
            {
                log.LogInformation($"Failed with exception {ex}");
            }
            
        }

        public static async Task<List<TeamManagerModel>> getTMG(string patientID, DataController dataController, ILogger log)
        {       
            try
            {
                //fetching patient for teamcode
                log.LogInformation("Fetching patient");

                string patientFilter = "vit_patientid eq " + patientID;
                JArray patientArray = await dataController.getDataFromCRMAsync("vit_patients", patientFilter);
                List<PatientModel> patientList = new List<PatientModel>();

                if (patientArray != null && patientArray.Count > 0)
                {
                    foreach (var patient in patientArray)
                    {
                        PatientModel patientModel = new PatientModel();

                        patientModel.patientID = patient["vit_patientid"].ToString();
                        patientModel.teamID = patient["_vit_team_value"].ToString();
                        patientModel.teamCode = patient["vit_teamcode"].ToString();

                        patientList.Add(patientModel);
                    }
                }

                if(patientList != null && patientList.Count > 0)
                {
                    foreach (var patient in patientList)
                    {
                        log.LogInformation("Fetching Team Manager");

                        string tmgFilter = "(vit_teamid eq '" + patient.teamCode + "') and (vit_discipline_id eq 'TMG') and (statecode eq 0) and (vit_status eq 'Active')";
                        JArray tmgArray = await dataController.getDataFromCRMAsync("contacts", tmgFilter);
                        List<TeamManagerModel> tmgList = new List<TeamManagerModel>();

                        if (tmgArray != null && tmgArray.Count > 0)
                        {
                            foreach (var tmg in tmgArray)
                            {
                                TeamManagerModel tmgModel = new TeamManagerModel();

                                tmgModel.contactID = tmg["contactid"].ToString();
                                tmgModel.teamCode = tmg["vit_teamid"].ToString();

                                tmgList.Add(tmgModel);
                            }

                            return tmgList;
                        }

                        log.LogInformation("Team Manager Not Found");
                        return null;
                    }
                }

                log.LogInformation("Patient Not Found");
                return null;
            }
            
            catch(Exception ex)
            {
                log.LogInformation($"Failed with exception {ex}");
                return null;
            }
        }

        public static bool isTMGExists(List<TeamManagerModel> tmgList)
        {
            if (tmgList != null && tmgList.Count > 0)
            {
                return true;
            }

            return false;
        }

        public static async Task<bool> createNoCoreTeamAssignmentAlert(DataController dataController, string contactID, string patientID, string empDiscipline)
        {
            JObject newAlertConfig = new JObject();
            string currentUTCDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            newAlertConfig.Add("vit_alertcreated", currentUTCDate);                   
            newAlertConfig.Add("vit_alertdescription", "Patient assignment of one core team clinician (" + empDiscipline + ")");
            newAlertConfig.Add("vit_alerttitle", "No Core Team Assignment");
            newAlertConfig.Add("vit_alerttimeframe", 909890002);
            newAlertConfig.Add("vit_notificationtype", 909890001); // Alert Type
            newAlertConfig.Add("vit_disciplinecodeimpacted", empDiscipline);
            newAlertConfig.Add("vit_Clinician@odata.bind", "/contacts(" + contactID + ")");
            newAlertConfig.Add("vit_PatientID@odata.bind", "/vit_patients(" + patientID + ")");

            bool createNewAlert = await dataController.postDataToCRMAsync("vit_alertses", newAlertConfig);

            return createNewAlert;
        }
    }
}
