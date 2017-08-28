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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Specialized;
using System.Web;
using System.Text;
using System.Net;
using System.Net.Http.Headers;

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

                    if (eventHandler != null)
                    {
                        //Enhance the Event Data for certain situations

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

                        //Was a documentId sent over? If so, populate the Document Metadata.
                        if (eventData.SvDocumentId > 0)
                        {
                                var manager = new DocumentManager();
                                eventData.DocumentMetaData = manager.GetDocumentMetaData(eventData.SvDocumentId.Value);
                        }

                        //Check if this event notification is a communication event. If so, we need to call back to SV to get metadata about the communication
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
                        
                        //Now Send it to the correct handler
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
                        else if (eventHandler.HandleMethod == "FileStore")
                        {
                            FileStoreHandler(eventData);
                        }
                        else if (eventHandler.HandleMethod == "FileStoreAndDocumentRetrieval")
                        {
                            FileStoreHandler(eventData);
                            DocumentRetrievalHandler(eventData);
                        }
                        else if (eventHandler.HandleMethod == "AwardLetterPrint")
                        {
                            AwardLetterDocumentRetrievalHandler(eventData);
                        }
                        else if (eventHandler.HandleMethod == "BatchProcessingAwardLetterPrint")
                        {
                            BatchProcessRetrievalHandler(ConfigConstants.AwardLetterPrintBatchType, eventHandler.BatchName, eventData);
                        }
                        else if (eventHandler.HandleMethod == "ApiIntegration")
                        {
                            ApiIntegrationsHandler(eventHandler.ApiEndpointName, eventData);
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
            var documentValues = new EventPropertyValues(eventData);
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
                EventPropertyValues eventPropertyValues = new EventPropertyValues(eventData);
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

            //First make sure we have the document metadata otherwise get it
            if (eventData.DocumentMetaData.Id == 0)
            {
                eventData.DocumentMetaData = manager.GetDocumentMetaData(eventData.SvDocumentId.Value);
            }

            //Get and Store the Documents
            var dataFiles = manager.GetDocumentFiles(eventData.SvDocumentId.Value, eventData);

            //If required create an index file
            if (dataFiles.Any() && (campusLogicConfigSection.DocumentSettings.IndexFileEnabled ?? false))
            {
                manager.CreateDocumentsIndexFile(dataFiles, eventData);
            }
        }

        /// <summary>
        /// Used for getting a batch of AL PDFs for printing.
        /// </summary>
        /// <param name="eventData"></param>
        private static void BatchProcessRetrievalHandler(string type, string name, EventNotificationData eventData)
        {
            var message = JsonConvert.SerializeObject(eventData).Replace("'", "''");

            using (var dbContext = new CampusLogicContext())
            {
                //Insert the event into the BatchProcessRecord table so that it can be processed by the Automated Batch Process job.
                dbContext.Database.ExecuteSqlCommand($"INSERT INTO [dbo].[BatchProcessRecord]([Type], [Name], [Message], [ProcessGuid]) VALUES('{type}', '{name}', '{message}', NULL)");
            }
        }

        /// <summary>
        /// Used to get PDF
        /// version of AL 
        /// for printers
        /// </summary>
        /// <param name="eventData"></param>
        private static void AwardLetterDocumentRetrievalHandler(EventNotificationData eventData)
        {
            var manager = new DocumentManager();

            if (eventData.AlRecordId == null)
            {
                logger.ErrorFormat("DataService ProcessPostedEvent Missing Record Id for Event Id: {0}", eventData.Id);
                return;
            }

            //Get and Store the Documents
            manager.GetAwardLetterPdfFile(eventData.AlRecordId.Value, eventData);
        }


        /// <summary>
        /// The File Store Handler. Surprise!
        /// </summary>
        public static void FileStoreHandler(EventNotificationData eventData)
        {
            try
            {
                using (var dbContext = new CampusLogicContext())
                {
                    var dataToSerialize = JsonConvert.SerializeObject(eventData).Replace("'", "''");
                    //Insert the event into the EventNotification table so that it can be processed by the Automated File Store job.
                    dbContext.Database.ExecuteSqlCommand($"INSERT INTO [dbo].[EventNotification]([EventNotificationId], [Message], [CreatedDateTime], [ProcessGuid]) VALUES({eventData.EventNotificationId}, '{dataToSerialize}', GetUtcDate(), NULL)");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"An error occured when attempting to handle the event data for file store: {ex}");
            }
        }

        /// <summary>
        /// Converts a collection of parameters and their values into HttpContent.
        /// </summary>
        /// <param name="eventParams"></param>
        /// <param name="mimeType"></param>
        /// <returns></returns>
        private static StringContent GetHttpContent(NameValueCollection eventParams, string mimeType)
        {
            var dict = eventParams.AllKeys.ToDictionary(k => k, k => eventParams[k]);
            var jsonString = JsonConvert.SerializeObject(dict);
            return new StringContent(jsonString, Encoding.UTF8, mimeType);
        }

        /// <summary>
        /// Retrieves an access token via OAuth 2.0 protocol.
        /// </summary>
        /// <param name="apiIntegration"></param>
        /// <returns></returns>
        private static async Task<string> GetOauth2TokenAsync(ApiIntegrationElement apiIntegration)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(
                            Encoding.ASCII.GetBytes(
                                string.Format("{0}:{1}", apiIntegration.Username, apiIntegration.Password))));
                    
                    var body = "grant_type=client_credentials";

                    StringContent theContent = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");

                    HttpResponseMessage response = await httpClient.PostAsync(new Uri(apiIntegration.TokenService), theContent).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseJson = await response.Content.ReadAsStringAsync();
                        return (string)JObject.Parse(responseJson)["access_token"];
                    }
                    else
                    {
                        throw new Exception("Invalid response");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("DataService GetOauth2TokenAsync Error: {0}", ex);
                throw;
            }
        }

        /// <summary>
        /// Retrieves an access token via OAuth WRAP protocol.
        /// </summary>
        /// <param name="apiIntegration"></param>
        /// <returns></returns>
        private static async Task<string> GetOauthWrapTokenAsync(ApiIntegrationElement apiIntegration)
        {
            try
            {
                // Make the Web API call
                using (var client = new HttpClient())
                {
                    // Build the form body that will be posted
                    var body = string.Format("wrap_name={0}&wrap_password={1}&wrap_scope={2}",
                        HttpUtility.UrlEncode(apiIntegration.Username),
                        HttpUtility.UrlEncode(apiIntegration.Password),
                        apiIntegration.Root);

                    StringContent theContent = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");

                    // Call the API and post the form data to request the token
                    HttpResponseMessage response = await client.PostAsync(new Uri(apiIntegration.TokenService), theContent).ConfigureAwait(false);  //ConfigureAwait is required when use with ASP.NET because of SynchronizationContext.
                    if (response.IsSuccessStatusCode)
                    {
                        // Pull the content of the response, decode the response, strip off certain parts of the access token and return just the token part for use
                        var accessToken = await response.Content.ReadAsStringAsync();
                        var wrapAccessPart = HttpUtility.UrlDecode(accessToken).Split('&').FirstOrDefault(x => x.Contains("wrap_access_token_expires_in"));
                        return HttpUtility.UrlDecode(accessToken).Replace("wrap_access_token=", string.Empty).Replace("&" + wrapAccessPart, string.Empty);
                    }
                    else
                    {
                        // Handle invalid responses here
                        throw new Exception("Invalid Response.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("DataService GetOauthWrapTokenAsync Error: {0}", ex);
                throw;
            }
        }

        /// <summary>
        /// Handles an HTTP request for an endpoint.
        /// </summary>
        /// <param name="endpointName"></param>
        /// <param name="eventData"></param>
        private static void ApiIntegrationsHandler(string endpointName, EventNotificationData eventData)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var apiEndpoint = campusLogicConfigSection.ApiEndpoints.GetEndpoints().Where(e => e.Name == endpointName).FirstOrDefault();
                    if (apiEndpoint == null)
                    {
                        throw new Exception("Invalid API Endpoint");
                    }

                    var apiIntegration = campusLogicConfigSection.ApiIntegrations.GetApiIntegrations().Where(a => a.ApiId == apiEndpoint.ApiId).FirstOrDefault();
                    if (apiIntegration == null)
                    {
                        throw new Exception("Invalid API Integration");
                    }

                    httpClient.BaseAddress = new Uri(apiIntegration.Root);

                    // Allow 5 minutes for response
                    httpClient.Timeout = new TimeSpan(0,5,0);
                                        
                    var authType = apiIntegration.Authentication;
                    switch (authType)
                    {
                        case ConfigConstants.Basic:
                            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", 
                                Convert.ToBase64String(
                                    Encoding.ASCII.GetBytes(
                                        string.Format("{0}:{1}", apiIntegration.Username, apiIntegration.Password))));
                            break;
                        case ConfigConstants.OAuth2:
                            var oauth2Token = GetOauth2TokenAsync(apiIntegration).Result;
                            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oauth2Token);
                            break;
                        case ConfigConstants.OAuth_WRAP:
                            var oauthwrapToken = GetOauthWrapTokenAsync(apiIntegration).Result;
                            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("WRAP", "access_token=\"" + oauthwrapToken + "\"");
                            break;
                        default:
                            break;
                    }            

                    // use the eventdata and its values to link up to the endpoint's parameters
                    var parameterMappings = JArray.Parse(apiEndpoint.ParameterMappings);
                    var eventPropertyValues = new EventPropertyValues(eventData);
                    NameValueCollection eventParams = new NameValueCollection();

                    // foreach mapping, get event property, find its corresponding eventdata, get that eventdata's value, attach it to the parameter in mapping
                    foreach (JObject mapping in parameterMappings)
                    {
                        var eventValue = eventPropertyValues.GetType().GetProperty(mapping.Value<string>("eventData")).GetValue(eventPropertyValues, null);
                        eventParams.Add(mapping.Value<string>("parameter"), eventValue.ToString());
                    }

                    HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);

                    var endpoint = apiEndpoint.Endpoint;

                    switch (apiEndpoint.Method)
                    {
                        case WebRequestMethods.Http.Get:
                            var array = (from key in eventParams.AllKeys
                                         from value in eventParams.GetValues(key)
                                         select string.Format("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(value))).ToArray();
                            endpoint += "?" + string.Join("&", array);
                            response = httpClient.GetAsync(endpoint).Result;
                            break;
                        case WebRequestMethods.Http.Post:
                            response = httpClient.PostAsync(endpoint, GetHttpContent(eventParams, apiEndpoint.MimeType)).Result;
                            break;
                        case WebRequestMethods.Http.Put:
                            response = httpClient.PutAsync(endpoint, GetHttpContent(eventParams, apiEndpoint.MimeType)).Result;
                            break;
                        default:
                            break;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception("Invalid response - " + (int)response.StatusCode + " " + response.StatusCode + " - Attempted to call " + apiEndpoint.Method + " " + apiIntegration.Root + endpoint);
                    }
                }                              
            }
            catch (Exception e)
            {
                logger.ErrorFormat("DataService ApiIntegrationsHandler Error: {0}", e);
                throw;
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

                    //Clean up event notifications logged in the EventNotification table
                    dbContext.Database.ExecuteSqlCommand("DELETE FROM [dbo].[EventNotification] WHERE [CreatedDateTime] < DateAdd(d, -" + purgeReceivedEventsAfterDays + ", GetUtcDate()) AND [ProcessGuid] IS NOT NULL");
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