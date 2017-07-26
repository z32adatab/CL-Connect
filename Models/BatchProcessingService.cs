using CampusLogicEvents.Implementation;
using CampusLogicEvents.Implementation.Configurations;
using CampusLogicEvents.Implementation.Models;
using Hangfire;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace CampusLogicEvents.Web.Models
{
    public static class BatchProcessingService
    {
        private static readonly ILog logger = LogManager.GetLogger("AdoNetAppender");
        private static readonly CampusLogicSection campusLogicConfigSection = (CampusLogicSection)ConfigurationManager.GetSection(ConfigConstants.CampusLogicConfigurationSectionName);
        
        [AutomaticRetry(Attempts = 0)]
        public static void RunBatchProcess(string type, string name, int size)
        {
            Guid processGuid = Guid.NewGuid();
            using (var dbContext = new CampusLogicContext())
            {
                try
                {
                    // Get all records in our LocalDB to process
                    var records = dbContext.BatchProcessRecords.Where(b => b.Name == name && b.Type == type && b.ProcessGuid == null);

                    if (records.Any())
                    {
                        // Lock these records from being processed again
                        dbContext.Database.ExecuteSqlCommand($"UPDATE[dbo].[BatchProcessRecord] SET[ProcessGuid] = '{processGuid}' FROM [dbo].[BatchProcessRecord] WHERE[Id] IN(SELECT [Id] from[dbo].[BatchProcessRecord] WHERE [Type] = '{type}' AND [Name] = '{name}' AND [ProcessGuid] IS NULL)");

                        // Ensure there are locked records with this process guid
                        if (dbContext.BatchProcessRecords.Any(b => b.ProcessGuid != null && b.ProcessGuid == processGuid))
                        {
                            if (type == ConfigConstants.AwardLetterPrintBatchType)
                            {
                                var manager = new DocumentManager();
                                List<Guid> recordIds = new List<Guid>();

                                // Get all records with this process guid
                                foreach (var record in dbContext.BatchProcessRecords.Where(b => b.ProcessGuid == processGuid).Select(b => b))
                                {
                                    // Deseralize the message
                                    EventNotificationData eventData =
                                        JsonConvert.DeserializeObject<EventNotificationData>(record.Message);

                                    recordIds.Add(eventData.AlRecordId.Value);

                                    // Download the batch PDF if we have reached our size or at the end
                                    if (recordIds.Count == size)
                                    {
                                        manager.GetBatchAwardLetterPdfFile(recordIds, name);
                                        recordIds.Clear();
                                    }
                                }

                                // Process the last group of records that was smaller than the batch size
                                if (recordIds.Any())
                                {
                                    manager.GetBatchAwardLetterPdfFile(recordIds, name);
                                }
                            }
                            
                            // Processing finished, delete batch process records with this process guid
                            dbContext.Database.ExecuteSqlCommand($"DELETE FROM [dbo].[BatchProcessRecord] WHERE [ProcessGuid] = '{processGuid}'");
                        }
                    }
                }
                catch (Exception e)
                {
                    //Something happened during processing. Update any records that may have been marked for processing back to null so that they can be re-processed.
                    logger.Error($"An error occured while attempting to execute the batch process: {e}");
                    dbContext.Database.ExecuteSqlCommand($"UPDATE [dbo].[BatchProcessRecord] SET [ProcessGuid] = NULL WHERE [ProcessGuid] = '{processGuid}'");
                }
            }
        }
    }
}