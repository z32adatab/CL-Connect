using log4net;
using System.Net.Http;
using System.Web.Http;
using CampusLogicEvents.Implementation;
using System;
using System.Net;
using System.Collections.Generic;
using CampusLogicEvents.Implementation.Configurations;

namespace CampusLogicEvents.Web.WebAPI
{
    public class CredentialsController : ApiController
    {

        private static readonly ILog logger = LogManager.GetLogger("AdoNetAppender");

        public CredentialsController()
        {
        }

        [HttpGet]
        public HttpResponseMessage TestAPICredentials(string username, string password, string environment, bool awardLetterUploadEnabled = false)
        {
            try
            {
                string stsURL = string.Empty;
                List<string> apiURLs = new List<string>();

                switch (environment)
                {
                    case EnvironmentConstants.SANDBOX:
                        {
                            apiURLs.Add(ApiUrlConstants.SV_APIURL_SANDBOX);
                            apiURLs.Add(ApiUrlConstants.PM_APIURL_SANDBOX);
                            if (awardLetterUploadEnabled)
                            {
                                apiURLs.Add(ApiUrlConstants.AL_APIURL_SANDBOX);
                            }
                            stsURL = ApiUrlConstants.STSURL_SANDBOX;
                            break;
                        }
                    case EnvironmentConstants.PRODUCTION:
                        {
                            apiURLs.Add(ApiUrlConstants.SV_APIURL_PRODUCTION);
                            apiURLs.Add(ApiUrlConstants.PM_APIURL_PRODUCTION);
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
                            apiURLs.Add(ApiUrlConstants.PM_APIURL_SANDBOX);
                            if (awardLetterUploadEnabled)
                            {
                                apiURLs.Add(ApiUrlConstants.AL_APIURL_SANDBOX);
                            }
                            stsURL = ApiUrlConstants.STSURL_SANDBOX;
                            break;
                        }
                }
                CredentialsManager credentialsManager = new CredentialsManager();
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