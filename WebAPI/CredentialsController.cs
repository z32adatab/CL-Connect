using log4net;
using System.Net.Http;
using System.Web.Http;
using CampusLogicEvents.Implementation;
using System;
using System.Net;
using System.Collections.Generic;
using System.Configuration;
using CampusLogicEvents.Implementation.Configurations;

namespace CampusLogicEvents.Web.WebAPI
{
    public class CredentialsController : ApiController
    {

        private static readonly ILog logger = LogManager.GetLogger("AdoNetAppender");

        #region constants 
        private const string SANDBOX = "sandbox";
        private const string PRODUCTION = "production";
        
        #endregion constants

        public CredentialsController()
        {
        }

        [HttpGet]
        public HttpResponseMessage TestAPICredentials(string username, string password, string environment, bool awardLetterUploadEnabled = false)
        {
            try
            {
                string stsURL;
                var apiURLs = new List<string>();

                // Check for "disable auto update"
                var disableAutoUpdate = false;
                var value = ConfigurationManager.AppSettings["DisableAutoUpdate"] ?? "false";
                bool.TryParse(value, out disableAutoUpdate);

                if (disableAutoUpdate)
                {
                    // use the values from web.config, not the ApiUrlConstants
                    apiURLs.Add(ConfigurationManager.AppSettings["SvWebApiUrl"]);
                    if (awardLetterUploadEnabled)
                    {
                        apiURLs.Add(ConfigurationManager.AppSettings["AwardLetterWebAPIURL"]);
                    }
                    stsURL = ConfigurationManager.AppSettings["StsUrl"];
                }
                // else, use the environment ApiUrlConstants
                else
                {
                    switch (environment)
                    {
                        case SANDBOX:
                        {
                            apiURLs.Add(ApiUrlConstants.SV_APIURL_SANDBOX);
                            if (awardLetterUploadEnabled)
                            {
                                apiURLs.Add(ApiUrlConstants.AL_APIURL_SANDBOX);
                            }
                            stsURL = ApiUrlConstants.STSURL_SANDBOX;
                            break;
                        }
                        case PRODUCTION:
                        {
                            apiURLs.Add(ApiUrlConstants.SV_APIURL_PRODUCTION);
                            if (awardLetterUploadEnabled)
                            {
                                apiURLs.Add(ApiUrlConstants.AL_APIURL_PRODUCTION);
                            }
                            stsURL = ApiUrlConstants.STSURL_PRODUCTION;
                            break;
                        }
                        default:
                        {
                            apiURLs.Add(ApiUrlConstants.SV_APIURL_SANDBOX);
                            if (awardLetterUploadEnabled)
                            {
                                apiURLs.Add(ApiUrlConstants.AL_APIURL_SANDBOX);
                            }
                            stsURL = ApiUrlConstants.STSURL_SANDBOX;
                            break;
                        }
                    }
                }
                var credentialsManager = new CredentialsManager();
                HttpResponseMessage response = credentialsManager.GetAuthorizationToken(username, password, apiURLs, stsURL);

                return response;
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("TestAPICredentials Get Error: {0}", ex);
                return Request.CreateResponse(HttpStatusCode.ExpectationFailed);
            }
        }
    }
}