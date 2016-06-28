using CampusLogicEvents.Implementation.Configurations;
using CampusLogicEvents.Implementation.Models;
using CampusLogicEvents.Web.Models;
using log4net;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Configuration;
using System.Net.Http;
using System.Reflection;
using System.Web;
using System.Web.Configuration;
using System.Web.Http;

namespace CampusLogicEvents.Web.WebAPI
{
    public class SetupController : ApiController
    {
        private static readonly ILog logger = LogManager.GetLogger("AdoNetAppender");

        private const string SANDBOX_ENVIRONMENT = "sandbox";

        public SetupController()
        {

        }

        /// <summary>
        /// Get current configurations 
        /// from web.config
        /// or new configurations model
        /// if this is an initial setup
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage Configurations()
        {
            try
            {
                var response = new ConfigurationModel();
                response.CampusLogicSection = (CampusLogicSection)ConfigurationManager.GetSection(ConfigConstants.CampusLogicConfigurationSectionName);

                //appSettings, for some reason it will not serialize appSettingSection, so we set
                // it to a simple dictionary to pass back
                var appSettings = new Dictionary<string, string>();
                for (int i = 0; i < ConfigurationManager.AppSettings.Count; i++)
                {
                    //Remove the placeholders if this is an initial setup
                    appSettings.Add(ConfigurationManager.AppSettings.GetKey(i), ConfigurationManager.AppSettings[i]);
                }

                //Environment was added with the most recent version
                //If it does not exist, add it and version, and 
                //adjust all naming conventions that have changed
                if (!appSettings.ContainsKey("Environment"))
                {
                    appSettings.Add("Environment", "initial");
                    appSettings.Add("ClConnectVersion", "2.1.1");
                    appSettings.Add("ApiUsername", appSettings["APIUsername"]);
                    appSettings.Remove("APIUsername");
                    appSettings.Add("ApiPassword", appSettings["APIPassword"]);
                    appSettings.Remove("APIPassword");
                    appSettings.Add("StsUrl", appSettings["STSURL"]);
                    appSettings.Remove("STSURL");
                    appSettings.Add("SvWebApiUrl", appSettings["SVWebAPIURL"]);
                    appSettings.Remove("SVWebAPIURL");
                    appSettings.Add("IncomingApiUsername", appSettings["IncomingAPIUsername"]);
                    appSettings.Remove("IncomingAPIUsername");
                    appSettings.Add("IncomingApiPassword", appSettings["IncomingAPIPassword"]);
                    appSettings.Remove("IncomingAPIPassword");
                    if (appSettings.ContainsKey("FileStoragePath"))
                    {
                        response.CampusLogicSection.DocumentSettings.DocumentStorageFilePath = appSettings["FileStoragePath"];
                        appSettings.Remove("FileStoragePath");
                    }
                }

                if (response.CampusLogicSection.EventNotifications.Count > 0)
                {
                    response.CampusLogicSection.EventNotificationsEnabled = true;
                }

                response.CampusLogicSection.DocumentSettings.DocumentsEnabled = response.CampusLogicSection.DocumentSettings.IndexFileEnabled;
                response.AppSettingsSection = appSettings;
                //temp workaround for deserialization issue
                response.CampusLogicSection.EventNotificationsEnabled = (response.CampusLogicSection.EventNotifications.EventNotificationsEnabled ?? false) 
                                                                            || (response.CampusLogicSection.StoredProcedures.StoredProceduresEnabled ?? false) 
                                                                            || (response.CampusLogicSection.DocumentSettings.DocumentsEnabled ?? false); 
                response.CampusLogicSection.StoredProceduresEnabled = response.CampusLogicSection.StoredProcedures.StoredProceduresEnabled;
                response.SmtpSection = (SmtpSection)ConfigurationManager.GetSection("system.net/mailSettings/smtp");
                response.CampusLogicSection.StoredProcedureList =
                    response.CampusLogicSection.StoredProcedures.GetStoredProcedures()
                        .Select(sp => new StoredProcedureDto(sp.Name, sp.GetParameters().ToList()))
                        .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("SetupController GetCurrentConfigurations Error: {0}", ex);
                return Request.CreateResponse(HttpStatusCode.ExpectationFailed);
            }
        }

        [HttpPost]
        public HttpResponseMessage ArchiveWebConfig()
        {
            try
            {
                var sourcePath = HttpContext.Current.Server.MapPath("~/Web.config");
                var targetPath = HttpContext.Current.Server.MapPath("~/WebArchive/");
                var fileName = $"webConfigCopy{DateTime.UtcNow.ToFileTimeUtc()}.config";
                targetPath = Path.Combine(targetPath, fileName);
                File.Copy(sourcePath, targetPath);
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception exception)
            {
                logger.ErrorFormat("SetupController ArchiveWebConfig Error: {0}", exception);
                return Request.CreateResponse(HttpStatusCode.ExpectationFailed);
            }

        }

        /// <summary>
        /// Save new configurations
        /// </summary>
        /// <param name="configurationModel"></param>
        [HttpPost]
        public HttpResponseMessage Configurations(ConfigurationModel configurationModel)
        {
            try
            {
                Configuration config = WebConfigurationManager.OpenWebConfiguration(HttpContext.Current.Request.ApplicationPath);
                CampusLogicSection campusLogicSection = (CampusLogicSection)config.GetSection("campusLogicConfig");
                SmtpSection smtpSection = (SmtpSection)config.GetSection("system.net/mailSettings/smtp");
                AppSettingsSection appSettings = config.AppSettings;

                //Workarounds until we can figure out why the properties won't deserialize as is
                campusLogicSection.EventNotifications = configurationModel.CampusLogicSection.EventNotifications;
                campusLogicSection.EventNotifications.EventNotificationsEnabled =
                    configurationModel.CampusLogicSection.EventNotificationsEnabled;
                foreach (EventNotificationHandler eventNotificationHandler in configurationModel.CampusLogicSection.EventNotificationsList)
                {
                    campusLogicSection.EventNotifications.Add(eventNotificationHandler);
                }

                campusLogicSection.DocumentSettings = configurationModel.CampusLogicSection.DocumentSettings;
                foreach (FieldMapSettings fieldMapSetting in configurationModel.CampusLogicSection.DocumentSettings.FieldMappingCollection)
                {
                    campusLogicSection.DocumentSettings.FieldMappingCollectionConfig.Add(fieldMapSetting);
                }

                campusLogicSection.StoredProcedures = configurationModel.CampusLogicSection.StoredProcedures;
                campusLogicSection.StoredProcedures.StoredProceduresEnabled =
                    configurationModel.CampusLogicSection.StoredProceduresEnabled;

                foreach (StoredProcedureDto storedProcedure in configurationModel.CampusLogicSection.StoredProcedureList)
                {
                    StoredProcedureElement storedProcedureElement = new StoredProcedureElement();
                    storedProcedureElement.Name = storedProcedure.Name;

                    foreach (ParameterDto parameter in storedProcedure.ParameterList)
                    {
                        ParameterElement parameterElement = new ParameterElement();
                        parameterElement.Name = parameter.Name;
                        parameterElement.DataType = parameter.DataType;
                        parameterElement.Length = parameter.Length;
                        parameterElement.Source = parameter.Source;

                        storedProcedureElement.Add(parameterElement);
                    }

                    campusLogicSection.StoredProcedures.Add(storedProcedureElement);
                }

                campusLogicSection.ISIRUploadSettings = configurationModel.CampusLogicSection.ISIRUploadSettings;
                campusLogicSection.ISIRCorrectionsSettings = configurationModel.CampusLogicSection.ISIRCorrectionsSettings;
                campusLogicSection.AwardLetterUploadSettings = configurationModel.CampusLogicSection.AwardLetterUploadSettings;
                campusLogicSection.SMTPSettings = configurationModel.CampusLogicSection.SMTPSettings;
                campusLogicSection.ClientDatabaseConnection = configurationModel.CampusLogicSection.ClientDatabaseConnection;
                smtpSection.DeliveryMethod = configurationModel.SmtpSection.DeliveryMethod;
                smtpSection.DeliveryFormat = configurationModel.SmtpSection.DeliveryFormat;
                smtpSection.From = configurationModel.SmtpSection.From;
                smtpSection.Network.ClientDomain = configurationModel.SmtpSection.Network.ClientDomain;
                smtpSection.Network.DefaultCredentials = configurationModel.SmtpSection.Network.DefaultCredentials;
                smtpSection.Network.EnableSsl = configurationModel.SmtpSection.Network.EnableSsl;
                smtpSection.Network.Host = configurationModel.SmtpSection.Network.Host;
                smtpSection.Network.Password = configurationModel.SmtpSection.Network.Password;
                smtpSection.Network.Port = configurationModel.SmtpSection.Network.Port;
                smtpSection.Network.TargetName = configurationModel.SmtpSection.Network.TargetName;
                smtpSection.Network.UserName = configurationModel.SmtpSection.Network.UserName;
                smtpSection.SpecifiedPickupDirectory.PickupDirectoryLocation = configurationModel.SmtpSection.SpecifiedPickupDirectory.PickupDirectoryLocation;
                appSettings.Settings.Clear();

                foreach (var appSetting in configurationModel.AppSettingsSection)
                {
                    appSettings.Settings.Add(FirstCharToUpper(appSetting.Key), appSetting.Value);
                }

                Assembly clConnectAssembly = Assembly.GetExecutingAssembly();
                AssemblyName clConnect = clConnectAssembly.GetName();

                appSettings.Settings["ClConnectVersion"].Value = clConnect.Version.ToString();
                appSettings.Settings["Webpages:Version"].Value = "2.0.0.0";
                appSettings.Settings["Webpages:Enabled"].Value = "false";
                appSettings.Settings["PreserveLoginUrl"].Value = "true";
                appSettings.Settings["ClientValidationEnabled"].Value = "true";
                appSettings.Settings["UnobtrusiveJavaScriptEnabled"].Value = "true";

                if (appSettings.Settings["Environment"].Value == SANDBOX_ENVIRONMENT)
                {
                    appSettings.Settings["StsUrl"].Value = ApiUrlConstants.STSURL_SANDBOX;
                    appSettings.Settings["SvWebApiUrl"].Value = ApiUrlConstants.SV_APIURL_SANDBOX;
                    appSettings.Settings["AwardLetterWebApiUrl"].Value = ApiUrlConstants.AL_APIURL_SANDBOX;
                }
                else
                {
                    appSettings.Settings["StsUrl"].Value = ApiUrlConstants.STSURL_PRODUCTION;
                    appSettings.Settings["SvWebApiUrl"].Value = ApiUrlConstants.SV_APIURL_PRODUCTION;
                    appSettings.Settings["AwardLetterWebApiUrl"].Value = ApiUrlConstants.AL_APIURL_PRODUCTION;
                }
                config.Save();
                return Request.CreateResponse(HttpStatusCode.OK);

            }
            catch (Exception ex)
            {
                logger.ErrorFormat("SetupController SaveConfigurations Error: {0}", ex);
                return Request.CreateResponse(HttpStatusCode.ExpectationFailed);
            }
        }

        /// <summary>
        /// Get initial configuration validation model
        /// with all section valid
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage GetInitialConfigurationValidationModel()
        {
            try
            {
                var newConfigurationValidationModel = new ConfigurationValidationModel
                {
                    EnvironmentValid = true,
                    ApiCredentialsValid = true,
                    ApplicationSettingsValid = true,
                    SMTPValid = true,
                    ISIRUploadValid = true,
                    AwardLetterUploadValid = true,
                    ISIRCorrectionsValid = true,
                    EventNotificationsValid = true,
                    ConnectionStringValid = true,
                    DocumentSettingsValid = true,
                    StoredProcedureValid = true,
                    DuplicatePath = false,
                    DuplicateEvent = false
                };
                return Request.CreateResponse(HttpStatusCode.OK, newConfigurationValidationModel);
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("SetupController GetInitialConfigurationValidationModel Error: {0}", ex);
                return Request.CreateResponse(HttpStatusCode.ExpectationFailed);
            }
        }

        /// <summary>
        /// Get the version of CL Connect
        /// to display on the home page :)
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage Version()
        {
            try
            {

                Assembly web = Assembly.GetExecutingAssembly();
                AssemblyName webName = web.GetName();

                var myVersion = webName.Version;

                return Request.CreateResponse(HttpStatusCode.OK, myVersion);
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("SetupController Version Error: {0}", ex);
                return Request.CreateResponse(HttpStatusCode.ExpectationFailed);
            }
        }

        /// <summary>
        /// Get all of the 
        /// enabled configurations validation
        /// status from the server
        /// </summary>
        /// <param name="configurationModel"></param>
        /// <returns></returns>
        [HttpPost]
        public HttpResponseMessage ValidateConfigurations(ConfigurationModel configurationModel)
        {
            try
            {
                var response = ValidationService.ValidateAll(configurationModel);
                if (response.DuplicateEvent || response.DuplicatePath
                    || !response.EnvironmentValid
                    || !response.ApiCredentialsValid
                    || (response.SMTPValid != null && (bool)!response.SMTPValid)
                    || (response.ISIRUploadValid != null && (bool)!response.ISIRUploadValid)
                    || (response.AwardLetterUploadValid != null && (bool)!response.AwardLetterUploadValid)
                    || (response.ISIRCorrectionsValid != null && (bool)!response.ISIRCorrectionsValid)
                    || (response.EventNotificationsValid != null && (bool)!response.EventNotificationsValid)
                    || (response.ConnectionStringValid != null && (bool)!response.ConnectionStringValid)
                    || (response.DocumentSettingsValid != null && (bool)!response.DocumentSettingsValid)
                    || (response.StoredProcedureValid != null && (bool)!response.StoredProcedureValid)
                    || !response.ApiCredentialsValid)
                {
                    return Request.CreateResponse(HttpStatusCode.ExpectationFailed, response);
                }
                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("SetupController GetCurrentConfigurations Error: {0}", ex);
                return Request.CreateResponse(HttpStatusCode.ExpectationFailed);
            }
        }

        /// <summary>
        /// Undo camel casing that is 
        /// caused when appSettings is sent 
        /// to the client side
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static string FirstCharToUpper(string input)
        {
            if (String.IsNullOrEmpty(input))
                throw new ArgumentException("ARGH!");
            return input.First().ToString().ToUpper() + input.Substring(1);
        }
    }
}