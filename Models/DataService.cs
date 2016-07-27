using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Odbc;
using System.Linq;
using CampusLogicEvents.Implementation;
using CampusLogicEvents.Implementation.Configurations;
using CampusLogicEvents.Implementation.Models;
using CampusLogicEvents.Web.Areas.HelpPage.ModelDescriptions;
using Hangfire;
using log4net;
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
            if (campusLogicConfigSection.EventNotifications.EventNotificationsEnabled ?? false)
            {
                using (var dbContext = new CampusLogicContext())
                {
                    foreach (JObject notificationEvent in eventData)
                    {
                        //SV-1698 Allowing requeue of events already processed
                        //First verify we didn't already get and process this event using the unique identifier within the configured purge day window before we clear old events
                        var id = notificationEvent["Id"].ToString();
                        var eventExists = dbContext.ReceivedEvents.FirstOrDefault(x => x.Id == id);

                        if (eventExists == null)
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
                       else
                        {
                            var eventToUpdate = dbContext.ReceivedEvents.First();
                            eventToUpdate.EventData = notificationEvent.ToString();
                            eventToUpdate.ReceivedDateTime = DateTime.UtcNow;
                            dbContext.SaveChanges();

                            BackgroundJob.Enqueue(() => ProcessPostedEvent(notificationEvent));
                        }
                    }
                }
            }
        }

        public static void GetDefaultDatabaseProcedure(EventNotificationData eventData, string storedProcedureName)
        {
            var parameters = new List<OdbcParameter>();

            //Define parameters
            parameters.Add(new OdbcParameter
            {
                ParameterName = "StudentId",
                OdbcType = OdbcType.VarChar,
                Size = 9,
                Value = eventData.StudentId
            });

            parameters.Add(new OdbcParameter
            {
                ParameterName = "AwardYear",
                OdbcType = OdbcType.VarChar,
                Size = 4,
                Value = String.IsNullOrWhiteSpace(eventData.AwardYear) ? string.Empty : (eventData.AwardYear.Substring(2, 2) + eventData.AwardYear.Substring(7, 2))
            });

            parameters.Add(new OdbcParameter
            {
                ParameterName = "TransactionCategoryId",
                OdbcType = OdbcType.Int,
                Value = eventData.SvTransactionCategoryId ?? 0
            });

            parameters.Add(new OdbcParameter
            {
                ParameterName = "EventNotificationId",
                OdbcType = OdbcType.Int,
                Value = eventData.EventNotificationId
            });

            if (eventData.SvDocumentId > 0)
            {
                var manager = new DocumentManager();
                DocumentMetaData metaData = manager.GetDocumentMetaData(eventData.SvDocumentId.Value);

                parameters.Add(new OdbcParameter
                {
                    ParameterName = "DocumentName",
                    OdbcType = OdbcType.VarChar,
                    Size = 128,
                    Value = metaData != null ? metaData.DocumentName : string.Empty
                });
            }
            else
            {
                parameters.Add(new OdbcParameter
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

                    var eventHandler = campusLogicConfigSection.EventNotifications.Cast<EventNotificationHandler>().FirstOrDefault(x => x.EventNotificationId == eventData.EventNotificationId) ??
                                       campusLogicConfigSection.EventNotifications.Cast<EventNotificationHandler>().FirstOrDefault(x => x.EventNotificationId == 0); //If no specific handler was provided check for the catch all handler

                    //Check if the transaction category was one of the three appeal types
                    if (eventData.SvTransactionCategoryId != null)
                    {
                        if (((TransactionCategory)eventData.SvTransactionCategoryId == TransactionCategory.SapAppeal
                            || (TransactionCategory)eventData.SvTransactionCategoryId == TransactionCategory.PjDependencyOverrideAppeal
                            || (TransactionCategory)eventData.SvTransactionCategoryId == TransactionCategory.PjEfcAppeal) && eventData.EventNotificationName == "Transaction Completed")
                        {
                            if (eventData.SvTransactionId == null)
                            {
                                throw new Exception("A transaction Id is needed to get the appeal meta data");
                            }
                            var manager = new AppealManager();
                            manager.GetAuthorizationForSV();
                            eventData.TransactionOutcomeId = manager.GetAppealMetaData((int)eventData.SvTransactionId).Result;
                        }
                    }
                    //check if this event notification is a communication event. If so, we need to call back to SV to get metadata about the communication
                    if (eventData.EventNotificationId >= 300 && eventData.EventNotificationId <= 399)
                    {
                        if (eventData.AdditionalInfoId == null || eventData.AdditionalInfoId == 0)
                        {
                            throw new Exception("An AdditionalInfoId is needed to get the communication event meta data");
                        }
                        var manager = new CommunicationManager();
                        manager.GetAuthorizationForSV();
                        CommunicationActivityMetadata communicationActivityMetadata = manager.GetCommunicationActivityMetaData((int)eventData.AdditionalInfoId).Result;
                        eventData.CommunicationBody = communicationActivityMetadata.Body;
                        eventData.CommunicationTypeId = communicationActivityMetadata.CommunicationTypeId;
                        eventData.CommunicationType = communicationActivityMetadata.CommunicationType;
                        eventData.CommunicationAddress = communicationActivityMetadata.CommunicationAddress;
                    }
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
                        else if (eventHandler.HandleMethod == "DocumentRetrievalAndStoredProc")
                        {
                            DocumentRetrievalHandler(eventData);
                            DatabaseStoredProcedure(eventData, eventHandler.DbCommandFieldValue);
                        }
                        else if (eventHandler.HandleMethod == "DocumentRetrievalAndNonQuery")
                        {
                            DocumentRetrievalHandler(eventData);
                            DatabaseCommandNonQueryHandler(eventData, eventHandler.DbCommandFieldValue);
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
        /// Handles events that require the system to execute a database stored procedure based on the configuration.
        /// </summary>
        /// <param name="eventData"></param>
        /// <param name="storedProcedureName"></param>
        private static void DatabaseStoredProcedure(EventNotificationData eventData, string storedProcedureName)
        {
            var storedProcedureSettings = campusLogicConfigSection.StoredProcedures.GetStoredProcedure(storedProcedureName);

            if (storedProcedureSettings != null)
            {
                // Parse parameters based on config.
                EventPropertyValues eventPropertyValues = new EventPropertyValues(null, null, eventData);
                List<OdbcParameter> parameters =
                    storedProcedureSettings.GetParameters().Select(p => ParseParameter(p, eventPropertyValues)).ToList();

                // For each parameter, need to add a placeholder "?" in the sql command.  
                // This is just part of the ODBC syntax.
                string placeholders = string.Join(",", parameters.Select(p => "?").ToArray());
                if (placeholders.Length > 0)
                {
                    placeholders = " (" + placeholders + ")";
                }

                // Final output should look like this: {CALL sproc_name (?, ?, ?)}
                string command = string.Format("{{CALL {0}{1}}}", storedProcedureSettings.Name, placeholders);

                ClientDatabaseManager.ExecuteDatabaseStoredProcedure(command, parameters);
            }
            else
            {
                //Static db procedure
                GetDefaultDatabaseProcedure(eventData, storedProcedureName);
            }
        }

        /// <summary>
        /// Builds an Odbc Parameter object from the arguments.
        /// </summary>
        /// <param name="parameterElement">Parameter definition from the config.</param>
        /// <param name="data">The event notification to pull the parameter's value from.</param>
        /// <returns></returns>
        private static OdbcParameter ParseParameter(ParameterElement parameterElement, EventPropertyValues data)
        {
            try
            {
                // Need to convert the string representation of the type to the actual
                // enum OdbcType.
                string[] odbcTypes = Enum.GetNames(typeof(OdbcType));
                string odbcTypeMatch = odbcTypes.First(t => t.Equals(parameterElement.DataType, StringComparison.InvariantCultureIgnoreCase));
                OdbcType odbcType = (OdbcType)Enum.Parse(typeof(OdbcType), odbcTypeMatch);

                // Get property from the data using a string.
                object value = data.GetType().GetProperty(parameterElement.Source).GetValue(data);

                // Build return object.
                var parameter = new OdbcParameter
                {
                    ParameterName = parameterElement.Name,
                    OdbcType = odbcType,
                    Size = parameterElement.LengthAsInt,
                    Value = value ?? DBNull.Value
                };

                return parameter;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to parse parameter.", ex);
            }
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
            if (dataFiles.Any() && (campusLogicConfigSection.DocumentSettings.IndexFileEnabled ?? false))
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
                    dbContext.Database.ExecuteSqlCommand("Delete from [NotificationLog] where [DateSent] < DateAdd(d, -" + purgeNotificaitonLogRecordsAfterDays + ", GetUtcDate())");

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
                if (campusLogicConfigSection.SMTPSettings.NotificationsEnabled ?? false)
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