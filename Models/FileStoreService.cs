using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using CampusLogicEvents.Implementation;
using CampusLogicEvents.Implementation.Configurations;
using CampusLogicEvents.Implementation.Models;
using Hangfire;
using log4net;
using Newtonsoft.Json;

namespace CampusLogicEvents.Web.Models
{
    public static class FileStoreService
    {
        private static readonly ILog logger = LogManager.GetLogger("AdoNetAppender");
        public const string Name = "File Storage";
        private static readonly CampusLogicSection campusLogicConfigSection = (CampusLogicSection)ConfigurationManager.GetSection(ConfigConstants.CampusLogicConfigurationSectionName);

        /// <summary>
        /// Processes the File Store Job 
        /// </summary>
        [AutomaticRetry(Attempts = 0)]
        public static void ProcessFileStore()
        {
            //Guid to be applied to the records in the EventNotification table
            Guid processGuid = Guid.NewGuid();

            using (var dbContext = new CampusLogicContext())
            {
                //1. Create GUID
                //2. Assign GUID to all existing records in LocalDB table that have null ProcessGuid
                //3. Write to file(s)
                //4. Rinse & Repeat every x minutes.
                try
                {
                    List<EventNotificationData> eventNotificationDataList = new List<EventNotificationData>();

                    //update all the current eventNotification records with no processguid to be processed.
                    dbContext.Database.ExecuteSqlCommand($"UPDATE [dbo].[EventNotification] SET [ProcessGuid] = '{processGuid}' WHERE [ProcessGuid] IS NULL");

                    //make sure there are events to process
                    if (dbContext.EventNotifications.Any(s => s.ProcessGuid != null && s.ProcessGuid == processGuid))
                    {
                        //Get any individual or shared FileStore events configured from the web.config
                        var individualEvents =
                            campusLogicConfigSection.EventNotifications.Cast<EventNotificationHandler>()
                                .Where(s => s.FileStoreType == "Individual").Select(e => e.EventNotificationId)
                                .ToList();

                        var sharedEvents = campusLogicConfigSection.EventNotifications.Cast<EventNotificationHandler>()
                            .Where(s => s.FileStoreType == "Shared").Select(e => e.EventNotificationId)
                            .ToList();

                        if (sharedEvents.Any())
                        {
                            //Make sure the event's processguid matches what we just generated so we're not re-processing events.
                            var sharedEventsToProcess =
                                dbContext.EventNotifications
                                .Where(e => sharedEvents.Contains(e.EventNotificationId) && e.ProcessGuid == processGuid)
                                .Select(m => m.Message);

                            foreach (string message in sharedEventsToProcess)
                            {
                                //Convert the json back into EventNotificationData
                                EventNotificationData eventData = JsonConvert.DeserializeObject<EventNotificationData>(message);

                                eventNotificationDataList.Add(eventData);
                            }

                            if (eventNotificationDataList.Any())
                            {
                                //send the list of events over to be processed into a file
                                FileStoreManager filestoreManager = new FileStoreManager();
                                filestoreManager.CreateFileStoreFile(eventNotificationDataList);
                                eventNotificationDataList.Clear();
                            }
                        }

                        //Process any individual events
                        if (individualEvents.Any())
                        {
                            var individualEventsToProcess =
                                dbContext.EventNotifications
                                .Where(e => individualEvents.Contains(e.EventNotificationId) && e.ProcessGuid == processGuid);


                            foreach (int eventNotificationId in individualEvents)
                            {
                                foreach (string message in individualEventsToProcess.Where(s => s.EventNotificationId == eventNotificationId).Select(s => s.Message))
                                {
                                    //Convert the json back into EventNotificationData
                                    EventNotificationData eventData = JsonConvert.DeserializeObject<EventNotificationData>(message);
                                    eventNotificationDataList.Add(eventData);
                                }

                                //process these events into their own file
                                FileStoreManager filestoreManager = new FileStoreManager();
                                filestoreManager.CreateFileStoreFile(eventNotificationDataList);
                                //clear out the list now that we've completed processing
                                eventNotificationDataList.Clear();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    //Something happened during processing. Update any records that may have been marked for processing back to null so that they can be re-processed.
                    logger.Error($"An error occured while attempting to process the event(s) for file store: {ex}");
                    dbContext.Database.ExecuteSqlCommand($"UPDATE [dbo].[EventNotification] SET [ProcessGuid] = NULL WHERE [ProcessGuid] = '{processGuid}'");
                }
            }
        }
    }
}