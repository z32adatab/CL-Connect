using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Odbc;
using System.Linq;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using CampusLogicEvents.Implementation;
using CampusLogicEvents.Implementation.Configurations;
using CampusLogicEvents.Implementation.Models;
using CampusLogicEvents.Web.Areas.HelpPage.ModelDescriptions;
using Hangfire;
using log4net;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json.Linq;

namespace CampusLogicEvents.Web.Models
{
    public static class DataService
    {
        private static readonly ILog logger = LogManager.GetLogger("AdoNetAppender");
        private static readonly CampusLogicSection campusLogicConfigSection = (CampusLogicSection)ConfigurationManager.GetSection(ConfigConstants.CampusLogicConfigurationSectionName);
        
        /// <summary>
        /// Process the Notification Events by storing each event received and then queue them each for processing
        /// </summary>
        /// <param name="eventData"></param>
        public static void ReceivePostedEvents(IEnumerable<JObject> eventData)
        {
            using (var dbContext = new CampusLogicContext())
            {
                foreach (JObject notificationEvent in eventData)
                {
                    //First verify we didn't already get and process this event using the unique identifier within the configured purge day window before we clear old events
                    var id = notificationEvent["Id"].ToString();
                    var eventExists = dbContext.ReceivedEvents.Any(x => x.Id == id && x.ProcessedDateTime != null);

                    if (!eventExists)
                    {
                        dbContext.ReceivedEvents.Add(new ReceivedEvent()
                        {
                            Id = notificationEvent["Id"].ToString(),
                            EventData = notificationEvent.ToString(),
                            ReceivedDateTime = DateTime.UtcNow
                        });
                        dbContext.SaveChanges();

                        BackgroundJob.Enqueue(() => ProcessPostedEvent(notificationEvent));
                    }
                }
            }
        }

        /// <summary>
        /// Process the Queued Job with the Notification Events
        /// </summary>
        /// <param name="notificationEvent"></param>
        [SendEmailFailure()]
        public static void ProcessPostedEvent(JObject notificationEvent)
        {
            try
            {
                using (var dbContext = new CampusLogicContext())
                {
                    var eventData = notificationEvent.ToObject<EventNotificationData>();

                    var eventHandler = campusLogicConfigSection.EventNotifications.FirstOrDefault(x => x.EventNotificationId == eventData.EventNotificationId) ??
                                       campusLogicConfigSection.EventNotifications.FirstOrDefault(x => x.EventNotificationId == 0); //If no specific handler was provided check for the catch all handler

                    if (eventHandler != null)
                    {
                        //Send it to the correct handler
                        if (eventHandler.HandleMethod == "DatabaseCommandNonQuery")
                        {
                            DatabaseCommandNonQueryHandler(eventData, eventHandler.DbCommandFieldValue);
                        }
                        else if (eventHandler.HandleMethod == "DatabaseStoredProcedure")
                        {
                            DatabaseStoredProcedure(eventData, eventHandler.DbCommandFieldValue);
                        }
                        else if (eventHandler.HandleMethod == "DocumentRetrieval")
                        {
                            DocumentRetrievalHandler(eventData);
                        }
                    } 
                    
                    //Update the received event with a processed date time
                    var storedEvent = dbContext.ReceivedEvents.FirstOrDefault(x => x.Id == eventData.Id);
                    if (storedEvent != null)
                    {
                        storedEvent.ProcessedDateTime = DateTime.UtcNow;
                        dbContext.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                //Log here any exceptions
                logger.ErrorFormat("DataService ProcessPostedEvent Error: {0}", ex);
                throw;
            }
        }

        /// <summary>
        /// Handles events that require the system to execute a non-query database command
        /// </summary>
        /// <param name="eventData"></param>
        /// <param name="databaseCommand"></param>
        private static void DatabaseCommandNonQueryHandler(EventNotificationData eventData, string databaseCommand)
        {
            var documentValues = new EventPropertyValues(null, null, eventData);
            var commandText = documentValues.ReplaceStringProperties(databaseCommand);
            ClientDatabaseManager.ExecuteDatabaseNonQuery(commandText);
        }

        /// <summary>
        /// Handles events that require the system to execute a database stored procedure
        /// TODO: Currently this is setup with a default stored procedure, may want to expand the config so they can define parameters there
        /// </summary>
        /// <param name="eventData"></param>
        /// <param name="storedProcedureName"></param>
        private static void DatabaseStoredProcedure(EventNotificationData eventData, string storedProcedureName)
        {
            var parameters = new List<OdbcParameter>();

            //Define parameters
            parameters.Add(new OdbcParameter()
            {
                ParameterName = "StudentId",
                OdbcType = OdbcType.VarChar,
                Size = 9,
                Value = eventData.StudentId
            });

            parameters.Add(new OdbcParameter()
            {
                ParameterName = "AwardYear",
                OdbcType = OdbcType.VarChar,
                Size = 4,
                Value = String.IsNullOrWhiteSpace(eventData.AwardYear) ? string.Empty : (eventData.AwardYear.Substring(2, 2) + eventData.AwardYear.Substring(7, 2))
            });

            parameters.Add(new OdbcParameter()
            {
                ParameterName = "TransactionCategoryId",
                OdbcType = OdbcType.Int,
                Value = eventData.SvTransactionCategoryId ?? 0
            });

            parameters.Add(new OdbcParameter()
            {
                ParameterName = "EventNotificationId",
                OdbcType = OdbcType.Int,
                Value = eventData.EventNotificationId
            });

            if (eventData.SvDocumentId > 0)
            {
                var manager = new DocumentManager();
                DocumentMetaData metaData = manager.GetDocumentMetaData(eventData.SvDocumentId.Value);

                parameters.Add(new OdbcParameter()
                {
                    ParameterName = "DocumentName",
                    OdbcType = OdbcType.VarChar,
                    Size = 128,
                    Value = metaData != null ? metaData.DocumentName : string.Empty
                });
            }
            else
            {
                parameters.Add(new OdbcParameter()
                {
                    ParameterName = "DocumentName",
                    OdbcType = OdbcType.VarChar,
                    Size = 128,
                    Value = string.Empty
                });
            }
            
            ClientDatabaseManager.ExecuteDatabaseStoredProcedure("{CALL " + storedProcedureName + " (?, ?, ?, ?, ?)}", parameters);

        }

        /// <summary>
        /// Handles events that require the document retrieval
        /// This will get the metadata and pull the documents and optionally create an index file for the documents
        /// </summary>
        /// <param name="eventData"></param>
        private static void DocumentRetrievalHandler(EventNotificationData eventData)
        {
            var manager = new DocumentManager();

            if (eventData.SvDocumentId == null)
            {
                logger.ErrorFormat("DataService ProcessPostedEvent Missing Document Id for Event Id: {0}", eventData.Id);
                return;
            }

            //First pull the document metadata
            DocumentMetaData metaData = manager.GetDocumentMetaData(eventData.SvDocumentId.Value);

            //Get and Store the Documents
            var dataFiles = manager.GetDocumentFiles(eventData.SvDocumentId.Value, eventData);

            //If required create an index file
            if (dataFiles.Any() && campusLogicConfigSection.DocumentSettings.IndexFileEnabled)
            {
                manager.CreateDocumentsIndexFile(dataFiles, eventData, metaData);
            }
        }

        /// <summary>
        /// Data Cleanup - Run on a scheduled basis.  Configured in the Startup.cs
        /// Removes old records from the ReceivedEvents and Logging to keep the database size down
        /// </summary>
        public static void DataCleanup()
        {
            try
            {
                var purgeReceivedEventsAfterDays = String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["PurgeReceivedEventsAfterDays"]) ? 30 : Convert.ToInt32(ConfigurationManager.AppSettings["PurgeReceivedEventsAfterDays"]);
                var purgeLogRecordsAfterDays = String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["PurgeLogRecordsAfterDays"]) ? 30 : Convert.ToInt32(ConfigurationManager.AppSettings["PurgeLogRecordsAfterDays"]);
                var purgeNotificaitonLogRecordsAfterDays = String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["PurgeNotificationLogRecordsAfterDays"]) ? 30 : Convert.ToInt32(ConfigurationManager.AppSettings["PurgeNotificationLogRecordsAfterDays"]);
                
                using (var dbContext = new CampusLogicContext())
                {
                    //Clean up log records older then configured number of days
                    dbContext.Database.ExecuteSqlCommand("Delete from [Log] where [Date] < DateAdd(d, -" + purgeLogRecordsAfterDays + ", GetUtcDate())");

                    //Clean up notification log records older then configured number of days
                    dbContext.Database.ExecuteSqlCommand("Delete from [NotificationLog] where [TimeSent] < DateAdd(d, -" + purgeNotificaitonLogRecordsAfterDays + ", GetUtcDate())");

                    //Clean up Received event records older then configured number of days
                    dbContext.Database.ExecuteSqlCommand("Delete from [ReceivedEvent] where [ReceivedDateTime] < DateAdd(d, -" + purgeReceivedEventsAfterDays + ", GetUtcDate())");
                }
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("DataService DataCleanup Error: {0}", ex);
            }
            
        }

        /// <summary>
        /// Gets a list of the log records
        /// </summary>
        /// <returns></returns>
        public static List<Log> LogRecords(int count)
        {
            using (var dbContext = new CampusLogicContext())
            {
                return dbContext.Logs.OrderByDescending(x => x.Date).Take(count).ToList();
            }
        }

        /// <summary>
        /// Gets a list of the received event records
        /// </summary>
        /// <returns></returns>
        public static List<ReceivedEvent> EventRecords(int count)
        {
            using (var dbContext = new CampusLogicContext())
            {
                return dbContext.ReceivedEvents.OrderByDescending(x => x.ReceivedDateTime).Take(count).ToList();
            }
        }

        /// <summary>
        /// Logs the notification in the notification log table
        /// </summary>
        /// <param name="notificationData"></param>
        public static void LogNotification(NotificationData notificationData)
        {
            try
            {
                if (campusLogicConfigSection.SMTPSettings.NotificationsEnabled)
                {
                    using (var dbContext = new CampusLogicContext())
                    {
                        dbContext.NotificationLogs.Add(new NotificationLog()
                        {
                            Recipients = notificationData.MailMessage.To.ToString(),
                            Sender = notificationData.MailMessage.From.ToString(),
                            DateSent = DateTime.UtcNow,
                            Subject = notificationData.MailMessage.Subject,
                            Body = notificationData.MailMessage.Body,
                            FailedSending = !notificationData.SendCompleted ?? false,
                        });

                        dbContext.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                //Log here any exceptions
                logger.ErrorFormat("DataService LogNotification Error logging recently sent email: {0}", ex);
            }
        }
    }
}