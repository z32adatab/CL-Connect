﻿using System;
using System.Collections.Generic;
using CampusLogicEvents.Web.Models;
using Hangfire;
using Microsoft.Owin;
using Owin;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net.Configuration;
using System.Net.Mail;
using System.Text.RegularExpressions;
using CampusLogicEvents.Implementation;
using CampusLogicEvents.Implementation.Configurations;
using CampusLogicEvents.Implementation.Models;
using log4net;

[assembly: OwinStartup(typeof(CampusLogicEvents.Web.Startup))]

namespace CampusLogicEvents.Web
{
    public class Startup
    {
        private static readonly ILog logger = LogManager.GetLogger("AdoNetAppender");
        private static readonly CampusLogicSection campusLogicSection = (CampusLogicSection)ConfigurationManager.GetSection(ConfigConstants.CampusLogicConfigurationSectionName);
        private static readonly NotificationManager notificationManager = new NotificationManager();


        public void Configuration(IAppBuilder app)
        {
            var workerCount = ConfigurationManager.AppSettings["BackgroundWorkerCount"];
            var workerRetryAttempts = ConfigurationManager.AppSettings["BackgroundWorkerRetryAttempts"];

            //Hangfire configurations
            GlobalConfiguration.Configuration.UseSqlServerStorage("CampusLogicConnection");
            GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = string.IsNullOrWhiteSpace(workerRetryAttempts) ? 10 : Convert.ToInt32(workerRetryAttempts) });

            var options = new BackgroundJobServerOptions { WorkerCount = string.IsNullOrWhiteSpace(workerCount) ? Environment.ProcessorCount : Convert.ToInt32(workerCount) };

            // Map Dashboard to the `http://<your-app>/hangfire` URL.
            app.UseHangfireDashboard();
            app.UseHangfireServer(options);

            // Setup/Update scheduled job
            RecurringJob.AddOrUpdate(() => DataService.DataCleanup(), Cron.Daily);

            ValidateSMTPSettings();
            AutomatedISIRUpload();
            AutomatedAwardLetterUpload();
            AutomatedFileMappingUpload();
            AutomatedISIRCorrectionBatching();
            AutomatedDocumentImportWorker();
            AutomatedFileStoreJob();
        }

        /// <summary>
        /// Job that runs when File Store is enabled
        /// </summary>
        private void AutomatedFileStoreJob()
        {
            //Does the EventNotification table exist? If not, create it.
            VerifyEventNotificationTableExists();

            bool? filestoreEnabled = campusLogicSection.EventNotifications.EventNotificationsEnabled;
            var fileStoreConfigured = campusLogicSection.EventNotifications.Cast<EventNotificationHandler>().Any(x => x.HandleMethod.Contains("FileStore"));

            if (filestoreEnabled == true && fileStoreConfigured)
            {
                if (string.IsNullOrWhiteSpace(campusLogicSection.FileStoreSettings.FileStorePath))
                {
                    NotificationService.ErrorNotification("Automated File Store Job", $"The following path is either unavailable or does not have the appropriate permissions: {campusLogicSection.FileStoreSettings.FileStorePath}");
                }
                else
                {
                    string minutes = campusLogicSection.FileStoreSettings.FileStoreMinutes;
                    RecurringJob.AddOrUpdate(() => FileStoreService.ProcessFileStore(),
                        "*/" + minutes + " " + "*" + " " + "*" + " " + "*" + " " + "*");
                }
            }
        }

        /// <summary>
        /// Validating all of the SMTP Settings
        /// </summary>
        private void ValidateSMTPSettings()
        {
            var smtpSection = (SmtpSection)ConfigurationManager.GetSection("system.net/mailSettings/smtp");
            if (campusLogicSection.SMTPSettings == null)
            {
                logger.Error("SMTP settings are missing");
                return;
            }
            if (!IsValidEmail(smtpSection.From))
            {
                logger.Error("SMTP from email address is not a valid email address");
                return;
            }
            foreach (var email in campusLogicSection.SMTPSettings.SendTo.Split(Convert.ToChar(",")).Where(email => !IsValidEmail(email.Trim())))
            {
                logger.Error($"SMTP to email address {email} is not a valid email address");
                return;
            }
            if (smtpSection.DeliveryMethod != SmtpDeliveryMethod.Network && smtpSection.DeliveryMethod != SmtpDeliveryMethod.SpecifiedPickupDirectory && smtpSection.DeliveryMethod != SmtpDeliveryMethod.PickupDirectoryFromIis)
            {
                logger.Error($"SMTP delivery method {smtpSection.DeliveryMethod} is not a valid delivery method");
                return;
            }
            if (smtpSection.DeliveryMethod == SmtpDeliveryMethod.Network)
            {
                if (!smtpSection.Network.DefaultCredentials && (string.IsNullOrEmpty(smtpSection.Network.UserName.Trim()) || string.IsNullOrEmpty(smtpSection.Network.Password.Trim())))
                {
                    logger.Error("SMTP network credentials (username or password) are missing");
                    return;
                }
            }
            if (smtpSection.DeliveryMethod == SmtpDeliveryMethod.SpecifiedPickupDirectory && string.IsNullOrEmpty(smtpSection.SpecifiedPickupDirectory.PickupDirectoryLocation))
            {
                logger.Error("Speicified Pickup Directory was indicated as the delivery method for SMTP but no pickup directory location was specified ");
            }
        }

        /// <summary>
        /// Checking the configurations and setting them 
        /// for the automated ISIR Upload process
        /// </summary>
        private void AutomatedISIRUpload()
        {
            //validation
            if (campusLogicSection.ISIRUploadSettings == null)
            {
                NotificationService.ErrorNotification("Automated ISIR Upload", "ISIR Upload settings are missing from the config file");
                logger.Error("ISIR Upload settings are missing");
                return;
            }

            UploadConfigurationValidation(new UploadSettings
            {
                UploadEnabled = campusLogicSection.ISIRUploadSettings.ISIRUploadEnabled ?? false,
                UploadFilePath = campusLogicSection.ISIRUploadSettings.ISIRUploadFilePath,
                ArchiveFilePath = campusLogicSection.ISIRUploadSettings.ISIRArchiveFilePath,
                UploadFrequencyType = campusLogicSection.ISIRUploadSettings.ISIRUploadFrequencyType,
                DaysToRun = campusLogicSection.ISIRUploadSettings.ISIRUploadDaysToRun,
                UploadType = UploadSettings.ISIR
            });
        }

        /// <summary>
        /// Checking the configurations and setting them 
        /// for the automated AwardLetter Upload process
        /// </summary>
        private void AutomatedAwardLetterUpload()
        {
            //validation
            if (campusLogicSection.AwardLetterUploadSettings == null)
            {
                NotificationService.ErrorNotification("Automated AwardLetter Upload", "The award letter upload settings are missing");
                logger.Error("Award Letter Upload settings are missing");
                return;
            }

            UploadConfigurationValidation(new UploadSettings
            {
                UploadEnabled = campusLogicSection.AwardLetterUploadSettings.AwardLetterUploadEnabled ?? false,
                UploadFilePath = campusLogicSection.AwardLetterUploadSettings.AwardLetterUploadFilePath,
                ArchiveFilePath = campusLogicSection.AwardLetterUploadSettings.AwardLetterArchiveFilePath,
                UploadFrequencyType = campusLogicSection.AwardLetterUploadSettings.AwardLetterUploadFrequencyType,
                DaysToRun = campusLogicSection.AwardLetterUploadSettings.AwardLetterUploadDaysToRun,
                UploadType = UploadSettings.AwardLetter
            });
        }

        /// <summary>
        /// Checking the configurations and setting them
        /// for the automated AwardLetter File Mapping upload 
        /// process
        /// </summary>
        private void AutomatedFileMappingUpload()
        {
            //validation
            if (campusLogicSection.FileMappingUploadSettings == null)
            {
                NotificationService.ErrorNotification("Automated AwardLetter File Mapping Upload", "The award letter file mapping settings are missing");
                logger.Error("Award Letter File Mapping Upload settings are missing");
                return;
            }

            UploadConfigurationValidation(new UploadSettings
            {
                UploadEnabled = campusLogicSection.FileMappingUploadSettings.FileMappingUploadEnabled ?? false,
                UploadFilePath = campusLogicSection.FileMappingUploadSettings.FileMappingUploadFilePath,
                ArchiveFilePath = campusLogicSection.FileMappingUploadSettings.FileMappingArchiveFilePath,
                UploadFrequencyType = campusLogicSection.FileMappingUploadSettings.FileMappingUploadFrequencyType,
                DaysToRun = campusLogicSection.FileMappingUploadSettings.FileMappingUploadDaysToRun,
                UploadType = UploadSettings.FileMapping
            });
        }

        /// <summary>
        /// ISIR corrections setup for automation
        /// </summary>
        private void AutomatedISIRCorrectionBatching()
        {
            const string DAILY = " * * *";

            //validation
            if (campusLogicSection.ISIRCorrectionsSettings == null)
            {
                NotificationService.ErrorNotification("Automated ISIR Corrections Batch Processing", "ISIR Corrections settings are missing");
                logger.Error("ISIR Corrections settings are missing");
                return;
            }

            if (campusLogicSection.ISIRCorrectionsSettings.CorrectionsEnabled ?? false)
            {
                //    if (!IsValidFilePathFormat(campusLogicSection.ISIRCorrectionsSettings.CorrectionsFilePath))
                //    {
                //        NotificationService.ErrorNotification("Automated ISIR Corrections Batch Processing", "Corrections file path is in incorrect format");
                //        logger.Error("Corrections file path is in incorrect format");
                //        return;
                //    }

                if (string.IsNullOrEmpty(campusLogicSection.ISIRCorrectionsSettings.TimeToRun))
                {
                    NotificationService.ErrorNotification("Automated ISIR Corrections Batch Processing", "ISIR corrections enabled but TimeToRun is empty");
                    logger.Error("ISIR corrections enabled but TimeToRun is empty");
                    return;
                }

                var acceptedDatsToRun = new List<string> { "SUN", "MON", "TUE", "WED", "THUR", "FRI", "SAT" };
                if (campusLogicSection.ISIRCorrectionsSettings.DaysToRun.Split(Convert.ToChar(",")).Any(day => !acceptedDatsToRun.Contains(day.ToUpperInvariant().Trim())))
                {
                    NotificationService.ErrorNotification("Automated ISIR Corrections Batch Processing", "ISIR corrections DaysToRun is in the incorret format");
                    logger.Error("ISIR corrections DaysToRun is in the incorret format");
                    return;
                }

                //Correct format is: HH:MMAM or HH:MMPM.. we will also except H:MMAM or HH:MMam(and their PM versions)
                if (!Regex.IsMatch(campusLogicSection.ISIRCorrectionsSettings.TimeToRun, @"^([0-9]|0[0-9]|1[0-9]|2[0-3]):[0-5][0-9]+(AM|PM|am|pm)$"))
                {
                    NotificationService.ErrorNotification("Automated ISIR Corrections Batch Processing", "ISIR corrections TimeToRun is in the incorret format");
                    logger.Error("ISIR corrections TimeToRun is in the incorret format");
                    return;
                }

                var amOrPm = campusLogicSection.ISIRCorrectionsSettings.TimeToRun.Substring(campusLogicSection.ISIRCorrectionsSettings.TimeToRun.Length - 2, 2).ToUpperInvariant();
                if (amOrPm != "AM" && amOrPm != "PM")
                {
                    NotificationService.ErrorNotification("Automated ISIR Corrections Batch Processing", "ISIR corrections enabled but TimeToRun invalid format");
                    logger.Error("ISIR corrections enabled but TimeToRun invalid format");
                    return;
                }

                if (campusLogicSection.ISIRCorrectionsSettings.FileExtension != "txt" && campusLogicSection.ISIRCorrectionsSettings.FileExtension != "dat" && campusLogicSection.ISIRCorrectionsSettings.FileExtension != "")
                {
                    NotificationService.ErrorNotification("Automated ISIR Corrections Batch Processing", "ISIR corrections enabled but file extension invalid format, valid formats are txt or dat");
                    logger.Error("ISIR corrections enabled but TimeToRun invalid format");
                    return;
                }

                //Converting hours to military time
                var hour = (campusLogicSection.ISIRCorrectionsSettings.TimeToRun.Substring(0, campusLogicSection.ISIRCorrectionsSettings.TimeToRun.IndexOf(":")) == "12" && amOrPm == "AM") ? "0" : amOrPm == "PM" ? (int.Parse(campusLogicSection.ISIRCorrectionsSettings.TimeToRun.Substring(0, campusLogicSection.ISIRCorrectionsSettings.TimeToRun.IndexOf(":"))) + 12).ToString() : campusLogicSection.ISIRCorrectionsSettings.TimeToRun.Substring(0, campusLogicSection.ISIRCorrectionsSettings.TimeToRun.IndexOf(":"));
                var minutes = campusLogicSection.ISIRCorrectionsSettings.TimeToRun.Substring(campusLogicSection.ISIRCorrectionsSettings.TimeToRun.IndexOf(":") + 1, 2) == "00" ? "0" : campusLogicSection.ISIRCorrectionsSettings.TimeToRun.Substring(campusLogicSection.ISIRCorrectionsSettings.TimeToRun.IndexOf(":") + 1, 2);

                if (!IsDigitsOnly(hour) || !IsDigitsOnly(minutes))
                {
                    NotificationService.ErrorNotification("Automated ISIR Corrections Batch Processing", "ISIR corrections enabled but TimeToRun invalid format");
                    logger.Error("ISIR corrections enabled but TimeToRun invalid format");
                    return;
                }

                //This is done using Cron expression
                //https://www.quartz-scheduler.org/documentation/quartz-2.1.x/tutorials/crontrigger
                //http://www.quartz-scheduler.org/documentation/quartz-2.x/tutorials/tutorial-lesson-06
                //This site details how to create this expression, we are currently using a daily at
                //a certain time expression: 0 MM HH ? * * but be adjusted to use something else if needed

                //Also the RecurringJob by default uses UTC time, to convert to local time see:
                //http://www.timeanddate.com/worldclock/converter.html

                //Set reoccurance based on configs
                RecurringJob.AddOrUpdate(() => ISIRService.ISIRCorrections(), minutes + " " + hour + DAILY);
                //For easy testing, uncomment this line
                //RecurringJob.AddOrUpdate(() => ISIRService.ISIRCorrections(), Cron.Minutely);
            }
            else
            {
                RecurringJob.RemoveIfExists("ISIRService.ISIRCorrections");
            }
        }

        /// <summary>
        /// Validate all of the configurations for a
        /// file upload process (ISIR or Award Letter)
        /// </summary>
        /// <param name="uploadSettings"></param>
        private void UploadConfigurationValidation(UploadSettings uploadSettings)
        {
            if (uploadSettings.UploadEnabled)
            {
                if (uploadSettings.UploadType != UploadSettings.AwardLetter && uploadSettings.UploadType != UploadSettings.ISIR && uploadSettings.UploadType != UploadSettings.FileMapping)
                {
                    NotificationService.ErrorNotification($"Automated {uploadSettings.UploadType} Upload", $"The upload type of {uploadSettings.UploadType} is not a valid upload type");
                    logger.Error($"{uploadSettings.UploadType} is not a valid upload type");
                    return;
                }
                if (string.IsNullOrEmpty(uploadSettings.ArchiveFilePath))
                {
                    NotificationService.ErrorNotification($"Automated {uploadSettings.UploadType} Upload", $"{uploadSettings.UploadType} Upload archive file path is missing");
                    logger.Error($"{uploadSettings.UploadType} Upload archive file path is missing");
                    return;
                }

                if (string.IsNullOrEmpty(uploadSettings.UploadFilePath))
                {
                    NotificationService.ErrorNotification($"Automated {uploadSettings.UploadType} Upload", $"{uploadSettings.UploadType} Upload upload file path is missing");
                    logger.Error($"{uploadSettings.UploadType} Upload upload file path is missing");
                    return;
                }

                //if (!IsValidFilePathFormat(uploadSettings.ArchiveFilePath))
                //{
                //    NotificationService.ErrorNotification($"Automated {uploadSettings.UploadType} Upload", $"{uploadSettings.UploadType} Upload archive file path is in incorrect format");
                //    logger.Error($"{uploadSettings.UploadType} Archive file path is in incorrect format");
                //    return;
                //}

                //if (!IsValidFilePathFormat(uploadSettings.UploadFilePath))
                //{
                //    NotificationService.ErrorNotification($"Automated {uploadSettings.UploadType} Upload", $"{uploadSettings.UploadType} Upload upload file path is in incorrect format");
                //    logger.Error($"{uploadSettings.UploadType} Upload file path is in incorrect format");
                //    return;
                //}

                var acceptedUploadFrequencies = new List<string> { "daily", "weekly", "minutes", "hourly" };
                if (!acceptedUploadFrequencies.Contains(uploadSettings.UploadFrequencyType))
                {
                    NotificationService.ErrorNotification($"Automated {uploadSettings.UploadType} Upload", $"{uploadSettings.UploadType} Upload frequency type is not in correct format");
                    logger.Error($"{uploadSettings.UploadType} Upload frequency type is not in correct format");
                    return;
                }

                var acceptedDatsToRun = new List<string> { "SUN", "MON", "TUE", "WED", "THUR", "FRI", "SAT" };
                if (uploadSettings.DaysToRun.Split(Convert.ToChar(",")).Any(day => !acceptedDatsToRun.Contains(day.ToUpperInvariant().Trim())))
                {
                    NotificationService.ErrorNotification($"Automated {uploadSettings.UploadType} Upload", $"{uploadSettings.UploadType} Days to Run is not in correct format");
                    logger.Error($"{uploadSettings.UploadType} Days to Run is not in correct format");
                    return;
                }

                //Set reoccurance based on configs
                var cronValue = "";

                switch (uploadSettings.UploadFrequencyType)
                {
                    case "daily":
                        cronValue = Cron.Daily();
                        break;
                    case "weekly":
                        cronValue = Cron.Weekly();
                        break;
                    case "minutes":
                        cronValue = Cron.Minutely();
                        break;
                    case "hourly":
                        cronValue = Cron.Hourly();
                        break;
                }

                RecurringJob.AddOrUpdate(uploadSettings.UploadType, () => UploadService.Upload(uploadSettings), cronValue);
            }
            else
            {
                RecurringJob.RemoveIfExists(uploadSettings.UploadType);
            }

        }



        /// <summary>
        /// Validates configuration for the document import feature,
        /// then adds a background job if valid.
        /// </summary>
        private void AutomatedDocumentImportWorker()
        {
            DocumentImportSettings config = campusLogicSection.DocumentImportSettings;
            string serviceName = DocumentImportService.Name;

            if (config == null)
            {
                NotificationService.ErrorNotification(serviceName, $"The {serviceName} settings are missing");
                logger.Error($"The {serviceName} settings are missing");
                return;
            }

            if (config.Enabled)
            {
                if (string.IsNullOrEmpty(config.ArchiveDirectory))
                {
                    NotificationService.ErrorNotification(serviceName, $"{serviceName} archive directory empty");
                    logger.Error($"{serviceName} archive directory empty");
                    return;
                }

                if (string.IsNullOrEmpty(config.FileDirectory))
                {
                    NotificationService.ErrorNotification(serviceName, $"{serviceName} file directory empty");
                    logger.Error($"{serviceName} file directory path empty");
                    return;
                }

                var acceptedUploadFrequencies = new List<string> { "daily", "weekly", "minutes", "hourly" };
                if (!acceptedUploadFrequencies.Contains(config.Frequency))
                {
                    NotificationService.ErrorNotification(serviceName, $"{serviceName} frequency not valid");
                    logger.Error($"{serviceName} frequency not valid");
                    return;
                }

                //Set reoccurance based on configs
                var cronValue = "";

                switch (config.Frequency)
                {
                    case "daily":
                        cronValue = Cron.Daily();
                        break;
                    case "weekly":
                        cronValue = Cron.Weekly();
                        break;
                    case "minutes":
                        cronValue = Cron.Minutely();
                        break;
                    case "hourly":
                        cronValue = Cron.Hourly();
                        break;
                }

                RecurringJob.AddOrUpdate(serviceName, () => DocumentImportService.ProcessImports(new DocumentImportSettingsDto
                {
                    ArchiveDirectory = config.ArchiveDirectory,
                    FileDirectory = config.FileDirectory,
                    FileExtension = config.FileExtension,
                    HasHeaderRow = config.HasHeaderRow,
                    UseSSN = config.UseSSN
                }), cronValue);
            }
            else
            {
                RecurringJob.RemoveIfExists(serviceName);
            }
        }

        /// <summary>
        /// Check if value is numerical
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private bool IsDigitsOnly(string str)
        {
            return str.All(c => c >= '0' && c <= '9');
        }

        /// <summary>
        /// Check if file path follows format of :
        /// Begin with x:\ or \\
        ///  valid characters are a-z| 0-9|-|.|_ 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        //private bool IsValidFilePathFormat(string path)
        //{
        //    return Regex.IsMatch(path, @"^(?:[a-zA-Z]\:|\\\\[\w\.]+\\[\w.$]+)\\(?:[\w]+\\)*\w([\w.])+$");
        //}

        /// <summary>
        /// Validating an email address via regex
        /// </summary>
        /// <param name="emailAddress"></param>
        /// <returns></returns>
        private bool IsValidEmail(string emailAddress)
        {
            if (String.IsNullOrEmpty(emailAddress))
                return false;

            // Use IdnMapping class to convert Unicode domain names.
            try
            {
                emailAddress = Regex.Replace(emailAddress, @"(@)(.+)$", DomainMapper,
                                      RegexOptions.None, TimeSpan.FromMilliseconds(200));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }

            // Return true if emailAddress is in valid e-mail format.
            try
            {
                return Regex.IsMatch(emailAddress,
                      @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
                      @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
                      RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        /// <summary>
        /// Verifies that the EventNotification table exists in LocalDB and if it doesn't, creates it.
        /// </summary>
        /// <param name="dbContext"></param>
        private static void VerifyEventNotificationTableExists()
        {
            using (var dbContext = new CampusLogicContext())
            {
                try
                {
                    dbContext.Database.ExecuteSqlCommand(
                        "IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EventNotification]'))" +
                        "BEGIN CREATE TABLE[dbo].[EventNotification]" +
                        "([Id] INT IDENTITY NOT NULL PRIMARY KEY" +
                        ",[EventNotificationId] INT NOT NULL" +
                        ",[Message] VARCHAR(MAX) NOT NULL" +
                        ",[CreatedDateTime] DATETIME NOT NULL" +
                        ",[ProcessGuid] UNIQUEIDENTIFIER NULL)" +
                        "CREATE INDEX ProcessGuid_Event ON [dbo].[EventNotification] ([ProcessGuid]) END");
                }
                catch (Exception ex)
                {
                    logger.Error($"There was an issue with validating and/or creating the EventNotification table in LocalDB: {ex}");
                }
            }
        }

        /// <summary>
        /// In case the domain is for whatever reason 
        /// in unicode
        /// </summary>
        /// <param name="match"></param>
        /// <returns></returns>
        private string DomainMapper(Match match)
        {
            // IdnMapping class with default property values.
            IdnMapping idn = new IdnMapping();

            string domainName = match.Groups[2].Value;

            domainName = idn.GetAscii(domainName);

            return match.Groups[1].Value + domainName;
        }
        /// <summary>
        /// Send error notification and return
        /// information to be logged back in web
        /// layer
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        private static NotificationData SendErrorNotification(string operation, string errorMessage, Exception ex = null)
        {
            if (campusLogicSection.SMTPSettings.NotificationsEnabled ?? false)
            {
                return ex == null ? notificationManager.SendErrorNotification(operation, errorMessage).Result : notificationManager.SendErrorNotification(operation, ex).Result;
            }
            else
            {
                return new NotificationData();
            }
        }
    }


}