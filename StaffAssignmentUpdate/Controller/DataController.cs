using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace StaffAssignmentUpdate.Controller
{
    public class DataController
    {
        Facade DataProcessor;
        public DataController()
        {
            DataProcessor = new Facade();
        }
        public async Task<JArray> getDataFromCRMAsync(string collection, string query)
        {
            Task<JObject> getRecords = DataProcessor.RetrieveMultipleAsync(collection, query);
            JObject resultGetRecords = await getRecords;
            if (resultGetRecords != null)
            {
                JArray recordArray = (JArray)resultGetRecords["value"];
                return recordArray;
            }
            return (JArray)null;
        }
        public async Task<bool> postDataToCRMAsync(string collection, JObject config)
        {
            Task<bool> createAlert = DataProcessor.CreateAsync(collection, config);
            bool resultUpdate = await createAlert;
            return resultUpdate;
        }

        public async Task<bool> patchDataToCRMAsync(string collection, string recordId, JObject configEntity)
        {
            Task<bool> updateAlertDate = DataProcessor.UpdateAsync("vit_alertses", recordId, configEntity);
            bool resultUpdate = await updateAlertDate;
            return resultUpdate;
        }       
    }
}
