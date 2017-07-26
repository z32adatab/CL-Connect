using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Configuration;
using System.Net.Http;
using CampusLogicEvents.Implementation;
using CampusLogicEvents.Implementation.Configurations;
using CampusLogicEvents.Implementation.Models;
using log4net;

namespace CampusLogicEvents.Web.Models
{
    public static class ValidationService
    {
        private static readonly ILog logger = LogManager.GetLogger("AdoNetAppender");
        private const string SANDBOX_ENVIRONMENT = "sandbox";
        private const string PROD_ENVIRONMENT = "production";

        /// <summary>
        /// Validate all of the enabled sections
        /// in the appropriate order
        /// NOTE: ensure if your section
        /// depends on another that you nest the sections
        /// appropriately
        /// </summary>
        /// <param name="configurationModel"></param>
        /// <returns></returns>
        public static ConfigurationValidationModel ValidateAll(ConfigurationModel configurationModel)
        {
            var response = new ConfigurationValidationModel();
            response.EnvironmentValid = ValidateEnvironment(configurationModel.AppSettingsSection).IsSuccessStatusCode;
            if (response.EnvironmentValid)
            {
                response.ApiCredentialsValid = ValidateApiCredentials(configurationModel.AppSettingsSection, configurationModel.CampusLogicSection.AwardLetterUploadSettings.AwardLetterUploadEnabled ?? false).IsSuccessStatusCode;
                if (configurationModel.CampusLogicSection.AwardLetterUploadSettings.AwardLetterUploadEnabled ?? false)
                {
                    response.AwardLetterUploadValid = ValidateAwardLetterUploadSettings(configurationModel.CampusLogicSection.AwardLetterUploadSettings.AwardLetterUploadFilePath).IsSuccessStatusCode;
                }
                if (configurationModel.CampusLogicSection.SMTPSettings.NotificationsEnabled ?? false)
                {
                    response.SMTPValid = ValidateSMTPSettings(configurationModel.CampusLogicSection.SMTPSettings, configurationModel.SmtpSection).IsSuccessStatusCode;
                }
                if (configurationModel.CampusLogicSection.ISIRUploadSettings.ISIRUploadEnabled ?? false)
                {
                    response.ISIRUploadValid = ValidateISIRUploadSettings(configurationModel.CampusLogicSection.ISIRUploadSettings.ISIRUploadFilePath).IsSuccessStatusCode;
                }
                if (configurationModel.CampusLogicSection.ISIRCorrectionsSettings.CorrectionsEnabled ?? false)
                {
                    response.ISIRCorrectionsValid = ValidateIsirCorrectionSettings(configurationModel.CampusLogicSection.ISIRCorrectionsSettings).IsSuccessStatusCode;
                }
                if (configurationModel.CampusLogicSection.DocumentSettings.DocumentsEnabled ?? false)
                {
                    response.DocumentSettingsValid = ValidateDocumentSettings(configurationModel.CampusLogicSection.DocumentSettings).IsSuccessStatusCode;
                }
                if (configurationModel.CampusLogicSection.DocumentImportSettings.Enabled)
                {
                    response.DocumentImportsValid = ValidateDocumentImportSettings(configurationModel.CampusLogicSection.DocumentImportSettings).IsSuccessStatusCode;
                }
                if (configurationModel.CampusLogicSection.FileStoreSettings.FileStoreEnabled ?? false)
                {
                    response.FileStoreSettingsValid = ValidateFileStoreSettings(configurationModel.CampusLogicSection.FileStoreSettings).IsSuccessStatusCode;
                }
                if (configurationModel.CampusLogicSection.AwardLetterPrintSettings.AwardLetterPrintEnabled ?? false)
                {
                    response.AwardLetterPrintSettingsValid = ValidateAwardLetterPrintSettings(configurationModel.CampusLogicSection.AwardLetterPrintSettings).IsSuccessStatusCode;
                }
                if (configurationModel.CampusLogicSection.BatchProcessingEnabled ?? false)
                {
                    response.BatchProcessingSettingsValid = ValidateBatchProcessingSettings(configurationModel).IsSuccessStatusCode;
                    var batchNamesDictionary = GetBatchNameLists(configurationModel.CampusLogicSection.EventNotificationsList, configurationModel.CampusLogicSection.BatchProcessingTypesList);
                    response.InvalidBatchName = !ValidateBatchNames(batchNamesDictionary);
                    response.MissingBatchName = !ValidateMissingBatchNames(batchNamesDictionary);
                }

                //if any of the features that has file paths involved are enabled validate file path uniqueness
                if ((configurationModel.CampusLogicSection.AwardLetterUploadSettings.AwardLetterUploadEnabled ?? false)
                    || (configurationModel.CampusLogicSection.ISIRUploadSettings.ISIRUploadEnabled ?? false)
                    || (configurationModel.CampusLogicSection.ISIRCorrectionsSettings.CorrectionsEnabled ?? false)
                    || (configurationModel.CampusLogicSection.DocumentSettings.DocumentsEnabled ?? false)
                    || (configurationModel.CampusLogicSection.FileStoreSettings.FileStoreEnabled ?? false)
                    || (configurationModel.CampusLogicSection.AwardLetterPrintSettings.AwardLetterPrintEnabled ?? false)
                    || (configurationModel.CampusLogicSection.BatchProcessingEnabled ?? false))
                {
                    response.DuplicatePath = !ValidatePathsUnique(configurationModel);
                }

                if (!(configurationModel.CampusLogicSection.EventNotificationsEnabled ?? false) || configurationModel.CampusLogicSection.EventNotificationsList.Count == 0)
                {
                    return response;
                }

                response.DuplicateEvent = !ValidateEventNotificationsUnique(configurationModel.CampusLogicSection.EventNotificationsList);
                response.ConnectionStringValid = ValidateConnectionStringValid(configurationModel.CampusLogicSection.EventNotificationsList, configurationModel.CampusLogicSection.ClientDatabaseConnection.ConnectionString);                
            }

            return response;
        }

        /// <summary>
        /// Validate that file paths are unique
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static bool ValidatePathsUnique(ConfigurationModel configuration)
        {
            var pathsToValidate = new List<string>();
            if (configuration.CampusLogicSection.AwardLetterUploadSettings.AwardLetterUploadEnabled ?? false)
            {
                pathsToValidate.Add(configuration.CampusLogicSection.AwardLetterUploadSettings.AwardLetterUploadFilePath);
                pathsToValidate.Add(configuration.CampusLogicSection.AwardLetterUploadSettings.AwardLetterArchiveFilePath);
            }
            if (configuration.CampusLogicSection.ISIRUploadSettings.ISIRUploadEnabled ?? false)
            {
                pathsToValidate.Add(configuration.CampusLogicSection.ISIRUploadSettings.ISIRUploadFilePath);
                pathsToValidate.Add(configuration.CampusLogicSection.ISIRUploadSettings.ISIRArchiveFilePath);
            }
            if (configuration.CampusLogicSection.ISIRCorrectionsSettings.CorrectionsEnabled ?? false)
            {
                pathsToValidate.Add(configuration.CampusLogicSection.ISIRCorrectionsSettings.CorrectionsFilePath);
            }
            if (configuration.CampusLogicSection.DocumentSettings.DocumentsEnabled ?? false)
            {
                pathsToValidate.Add(configuration.CampusLogicSection.DocumentSettings.DocumentStorageFilePath);
            }
            if (configuration.CampusLogicSection.FileStoreSettings.FileStoreEnabled ?? false)
            {
                pathsToValidate.Add(configuration.CampusLogicSection.FileStoreSettings.FileStorePath);
            }
            if (configuration.CampusLogicSection.AwardLetterPrintSettings.AwardLetterPrintEnabled ?? false)
            {
                pathsToValidate.Add(configuration.CampusLogicSection.AwardLetterPrintSettings.AwardLetterPrintFilePath);
            }
            if (configuration.CampusLogicSection.BatchProcessingEnabled ?? false)
            {
                var batchProcessingTypes = configuration.CampusLogicSection.BatchProcessingTypesList;

                foreach (var batchProcessingType in batchProcessingTypes)
                {
                    var paths = batchProcessingType.BatchProcesses.Select(b => b.FilePath).Distinct();

                    foreach (var path in paths)
                    {
                        pathsToValidate.Add(path);
                    }
                }
            }

            if (pathsToValidate.Count > 1)
            {
                return pathsToValidate.Count == pathsToValidate.Distinct().Count();
            }
            return true;
        }

        /// <summary>
        /// Validating that there is only one 
        /// event notification for each event notification id
        /// </summary>
        /// <param name="eventNotifications"></param>
        /// <returns></returns>
        public static bool ValidateEventNotificationsUnique(IList<EventNotificationHandler> eventNotifications)
        {
            return eventNotifications.Count == eventNotifications.Select(x => x.EventNotificationId).Distinct().Count();
        }

        public static Dictionary<string, List<string>> GetBatchNameLists(IList<EventNotificationHandler> eventNotifications, IList<BatchProcessingTypeDto> batchProcessingTypes)
        {
            var eventHandlerBatchNames = new List<string>();
            var batchProcessNames = new List<string>();
            var dict = new Dictionary<string, List<string>>();
                        
            foreach (var eventNotification in eventNotifications)
            {
                if (eventNotification.HandleMethod == "BatchProcessingAwardLetterPrint")
                {
                    eventHandlerBatchNames.Add(eventNotification.BatchName);
                }
            }

            foreach (var batchProcessingType in batchProcessingTypes)
            {
                foreach (var batchProcess in batchProcessingType.BatchProcesses)
                {
                    batchProcessNames.Add(batchProcess.BatchName);
                }
            }

            dict.Add("event", eventHandlerBatchNames);
            dict.Add("batch", batchProcessNames);

            return dict;
        }

        public static bool ValidateMissingBatchNames(Dictionary<string, List<string>> batchNamesDictionary)
        {
            var eventHandlerBatchNames = batchNamesDictionary["event"];
            var batchProcessNames = batchNamesDictionary["batch"];

            // Compare to make sure no batch hasn't been accounted for
            if (eventHandlerBatchNames.Except(batchProcessNames).Any()) return false;

            return true;
        }

        /// <summary>
        /// Validating that no batch names are empty
        /// and that they are all unique (to a given type).
        /// </summary>
        /// <param name="eventNotifications"></param>
        /// <param name="batchProcessingTypes"></param>
        /// <returns></returns>
        public static bool ValidateBatchNames(Dictionary<string, List<string>> batchNamesDictionary)
        {
            var eventHandlerBatchNames = batchNamesDictionary["event"];
            var batchProcessNames = batchNamesDictionary["batch"];

            // Check for a blank batch name within the event handlers
            if (eventHandlerBatchNames.All(x => string.IsNullOrEmpty(x))) return false;

            // Check for duplicate event handler batch names
            if (eventHandlerBatchNames.GroupBy(x => x).Any(g => g.Count() > 1)) return false;

            // Check for batch name longer than 25 characters
            if (eventHandlerBatchNames.All(x => x.Length > 25)) return false;

            // Check for a blank batch name within the batch processes
            if (batchProcessNames.All(x => string.IsNullOrEmpty(x))) return false;

            // Check for duplicate batch process names
            if (batchProcessNames.GroupBy(x => x).Any(g => g.Count() > 1)) return false;

            // Check for batch name longer than 25 characters
            if (batchProcessNames.All(x => x.Length > 25)) return false;

            return true;
        }

        /// <summary>
        /// Validating database connection
        /// if the database connection is required
        /// </summary>
        /// <param name="eventNotifications"></param>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static bool ValidateConnectionStringValid(IList<EventNotificationHandler> eventNotifications, string connectionString)
        {
            string[] handlersWithoutConnectionString = { "DocumentRetrieval", "FileStore", "FileStoreAndDocumentRetrieval", "AwardLetterPrint", "BatchProcessingAwardLetterPrint" };

            if (eventNotifications.All(x => handlersWithoutConnectionString.Contains(x.HandleMethod)))
            {
                return true;
            }

            var result = ClientDatabaseManager.TestConnectionString(connectionString);

            return string.IsNullOrEmpty(result);
        }

        /// <summary>
        /// Validate the environment
        /// </summary>
        /// <param name="applicationAppSettingsSection"></param>
        public static HttpResponseMessage ValidateEnvironment(Dictionary<string, string> applicationAppSettingsSection)
        {
            //Ensure that the environment was set appropriately
            if (applicationAppSettingsSection["environment"] != SANDBOX_ENVIRONMENT
                && applicationAppSettingsSection["environment"] != PROD_ENVIRONMENT)
            {
                logger.Fatal($"Enivronment required to save new configurations, environment: {applicationAppSettingsSection["environment"] } is invalid");
                return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// Validate API Credentials
        /// </summary>
        /// <param name="applicationAppSettingsSection"></param>
        public static HttpResponseMessage ValidateApiCredentials(Dictionary<string, string> applicationAppSettingsSection, bool awardLetterUploadEnabled = false)
        {
            string stsUrl;
            List<string> apiURLs = new List<string>();

            switch (applicationAppSettingsSection["environment"])
            {
                case SANDBOX_ENVIRONMENT:
                    {
                        apiURLs.Add(ApiUrlConstants.SV_APIURL_SANDBOX);
                        if (awardLetterUploadEnabled)
                        {
                            apiURLs.Add(ApiUrlConstants.AL_APIURL_SANDBOX);
                        }
                        stsUrl = ApiUrlConstants.STSURL_SANDBOX;
                        break;
                    }
                case PROD_ENVIRONMENT:
                    {
                        apiURLs.Add(ApiUrlConstants.SV_APIURL_PRODUCTION);
                        if (awardLetterUploadEnabled)
                        {
                            apiURLs.Add(ApiUrlConstants.AL_APIURL_PRODUCTION);
                        }
                        stsUrl = ApiUrlConstants.STSURL_PRODUCTION;
                        break;
                    }
                default:
                    {
                        apiURLs.Add(ApiUrlConstants.SV_APIURL_SANDBOX);
                        if (awardLetterUploadEnabled)
                        {
                            apiURLs.Add(ApiUrlConstants.AL_APIURL_SANDBOX);
                        }
                        stsUrl = ApiUrlConstants.STSURL_SANDBOX;
                        break;
                    }
            }
            CredentialsManager credentialsManager = new CredentialsManager();

            //Ensure the SV API Credentials are valid, we always check this 
            var svCredentialsResponse = credentialsManager.GetAuthorizationToken(applicationAppSettingsSection["apiUsername"], applicationAppSettingsSection["apiPassword"], apiURLs, stsUrl);
            if (!svCredentialsResponse.IsSuccessStatusCode)
            {
                logger.Fatal($"API Credentials for Student Verification are not valid, username: {applicationAppSettingsSection["apiUsername"]}, password: {applicationAppSettingsSection["apiPassword"]}");
                return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
            }

            //Ensure the Award Letter API Credentials are valid, we only check this if award letter upload is being used
            if (awardLetterUploadEnabled)
            {
                var alCredentialsResponse = credentialsManager.GetAuthorizationToken(applicationAppSettingsSection["apiUsername"], applicationAppSettingsSection["apiPassword"], apiURLs, stsUrl);
                if (!alCredentialsResponse.IsSuccessStatusCode)
                {
                    logger.Fatal($"API Credentials for Award Letter are not valid, username: {applicationAppSettingsSection["apiUsername"]}, password: {applicationAppSettingsSection["apiPassword"]}");
                    return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
                }
            }

            return new HttpResponseMessage(HttpStatusCode.OK);

        }

        public static void ValidateApplicationSettings()
        {
        }

        /// <summary>
        /// Validate SMTP Settings
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="smtpSection"></param>
        /// <returns></returns>
        public static HttpResponseMessage ValidateSMTPSettings(SMTPSettings settings, SmtpSection smtpSection)
        {
            try
            {
                if (settings.NotificationsEnabled ?? false)
                {
                    NotificationService.ErrorNotification(smtpSection, settings.SendTo);
                }
            }
            catch (Exception exception)
            {
                logger.Fatal($"There was an error sending a test email to {settings.SendTo} from {smtpSection.From}", exception);
                return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);

        }

        public static HttpResponseMessage ValidateISIRUploadSettings(string directoryPath)
        {
            try
            {
                DocumentManager documentManager = new DocumentManager();
                if (documentManager.ValidateDirectory(directoryPath))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }
                else
                {
                    return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("TestWritePermissions Get Error: {0}", ex);
                return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
            }
        }


        public static HttpResponseMessage ValidateAwardLetterUploadSettings(string directoryPath)
        {
            try
            {
                DocumentManager documentManager = new DocumentManager();
                if (documentManager.ValidateDirectory(directoryPath))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }
                else
                {
                    return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("TestWritePermissions Get Error: {0}", ex);
                return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
            }
        }

        public static HttpResponseMessage ValidateDocumentImportSettings(DocumentImportSettings settings)
        {
            try
            {
                DocumentManager documentManager = new DocumentManager();
                if (documentManager.ValidateDirectory(settings.FileDirectory) &&
                    documentManager.ValidateDirectory(settings.ArchiveDirectory))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }
                else
                {
                    return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("TestWritePermissions Get Error: {0}", ex);
                return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
            }
        }

        /// <summary>
        /// Validate the ISIR Corrections
        /// file path
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns></returns>
        public static HttpResponseMessage ValidateIsirCorrectionSettings(ISIRCorrectionsSettings isirCorrectionsSettings)
        {
            try
            {
                DocumentManager documentManager = new DocumentManager();
                if (documentManager.ValidateDirectory(isirCorrectionsSettings.CorrectionsFilePath))
                {
                    if (isirCorrectionsSettings.TdClientEnabled.HasValue && isirCorrectionsSettings.TdClientEnabled.Value == true)
                    {
                        if (documentManager.ValidateDirectory(isirCorrectionsSettings.TdClientExecutablePath) &&
                            documentManager.ValidateDirectory(isirCorrectionsSettings.TdClientArchiveFilePath))
                        {
                            return new HttpResponseMessage(HttpStatusCode.OK);
                        }
                        else
                        {
                            return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
                        }
                    }
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }
                else
                {
                    return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("TestWritePermissions Get Error: {0}", ex);
                return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
            }
        }

        public static HttpResponseMessage ValidateDocumentSettings(DocumentSettings settings)
        {
            try
            {
                if (settings.DocumentsEnabled == null)
                {
                    throw new Exception();
                }

                if (settings.IndexFileEnabled == null)
                {
                    throw new Exception();
                }
                else if (settings.IndexFileEnabled == true)
                {
                    if (string.IsNullOrEmpty(settings.DocumentStorageFilePath))
                    {
                        throw new Exception();
                    }
                    else
                    {
                        DocumentManager documentManager = new DocumentManager();
                        if (!documentManager.ValidateDirectory(settings.DocumentStorageFilePath))
                        {
                            throw new Exception();
                        }
                    }
                    if (string.IsNullOrEmpty(settings.FileNameFormat))
                    {
                        throw new Exception();
                    }

                    if (settings.IncludeHeaderRecord == null)
                    {
                        throw new Exception();
                    }

                    if (string.IsNullOrEmpty(settings.IndexFileExtension))
                    {
                        throw new Exception();
                    }

                    if (string.IsNullOrEmpty(settings.IndexFileFormat))
                    {
                        throw new Exception();
                    }

                    if (settings.FieldMappingCollection.Count == 0)
                    {
                        throw new Exception();
                    }
                }

                //loop through each field mapping
                foreach (FieldMapSettings fieldMapping in settings.FieldMappingCollection)
                {
                    if (fieldMapping.FieldSize == null)
                    {
                        throw new Exception();
                    }
                    if (fieldMapping.DataType == null)
                    {
                        throw new Exception();
                    }
                    if (fieldMapping.FileFieldName == null)
                    {
                        throw new Exception();
                    }
                }
            }
            catch (Exception exception)
            {
                return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// Validates the File Store Settings
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static HttpResponseMessage ValidateFileStoreSettings(FileStoreSettings settings)
        {
            try
            {
                if (settings.FileStoreEnabled == null)
                {
                    throw new Exception();
                }

                if (string.IsNullOrEmpty(settings.FileStorePath))
                {
                    throw new Exception();
                }
                else
                {
                    FileStoreManager documentManager = new FileStoreManager();
                    if (!documentManager.ValidateDirectory(settings.FileStorePath))
                    {
                        throw new Exception();
                    }
                }
                if (string.IsNullOrEmpty(settings.FileStoreNameFormat))
                {
                    throw new Exception();
                }

                if (settings.IncludeHeaderRecord == null)
                {
                    throw new Exception();
                }

                if (string.IsNullOrEmpty(settings.FileExtension))
                {
                    throw new Exception();
                }

                if (string.IsNullOrEmpty(settings.FileStoreFileFormat))
                {
                    throw new Exception();
                }

                if (settings.FileStoreMappingCollection.Count == 0)
                {
                    throw new Exception();
                }

                //loop through each field mapping
                foreach (FieldMapSettings fieldMapping in settings.FileStoreMappingCollection)
                {
                    if (fieldMapping.FieldSize == null)
                    {
                        throw new Exception();
                    }
                    if (fieldMapping.DataType == null)
                    {
                        throw new Exception();
                    }
                    if (fieldMapping.FileFieldName == null)
                    {
                        throw new Exception();
                    }
                }
            }
            catch (Exception exception)
            {
                logger.Error(exception);
                return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// Validates the File Store Settings
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static HttpResponseMessage ValidateAwardLetterPrintSettings(AwardLetterPrintSettings settings)
        {
            try
            {
                if (settings.AwardLetterPrintEnabled == null)
                {
                    throw new Exception();
                }

                if (string.IsNullOrEmpty(settings.AwardLetterPrintFilePath))
                {
                    throw new Exception();
                }
                else
                {
                    FileStoreManager documentManager = new FileStoreManager();
                    if (!documentManager.ValidateDirectory(settings.AwardLetterPrintFilePath))
                    {
                        throw new Exception();
                    }
                }
            }
            catch (Exception exception)
            {
                logger.Error(exception);
                return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        /// <summary>
        /// Validates Batch Processing Settings
        /// </summary>
        /// <returns></returns>
        public static HttpResponseMessage ValidateBatchProcessingSettings(ConfigurationModel configurationModel)
        {
            try
            {
                var batchProcessingTypes = configurationModel.CampusLogicSection.BatchProcessingTypesList;

                foreach (var batchProcessingType in batchProcessingTypes)
                {
                    var paths = batchProcessingType.BatchProcesses.Select(b => b.FilePath);

                    foreach (var path in paths)
                    {
                        if (string.IsNullOrEmpty(path))
                        {
                            throw new Exception();
                        }
                        else
                        {
                            FileStoreManager documentManager = new FileStoreManager();
                            if (!documentManager.ValidateDirectory(path))
                            {
                                throw new Exception();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error(e);
                return new HttpResponseMessage(HttpStatusCode.ExpectationFailed);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}




