using CampusLogicEvents.Web.Filters;
using CampusLogicEvents.Web.Models;
using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace CampusLogicEvents.Web.WebAPI
{
    [IdentityBasicAuthentication] // Enable Basic authentication for this controller.
    [Authorize] // Require authenticated requests.
    public class NotificationEventController : ApiController
    {
        private static readonly ILog logger = LogManager.GetLogger("AdoNetAppender");

        // POST api/NotificationEvent
        public HttpResponseMessage Post([FromBody] IEnumerable<JObject> postData)
        {
            try
            {
                //Header verification "User-Agent: StudentVerification Event API"
                if (Request.Headers.UserAgent.ToString() != "StudentVerification")
                {
                    Request.CreateResponse(HttpStatusCode.ExpectationFailed); // Anything but 200 is considered an error and StudentVerification will retry to send it.
                }

                if (postData != null)
                {
                    //Store the received event
                    DataService.ReceivePostedEvents(postData);
                }
                
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("NotificationEvent Post Error: {0}", ex);
                return Request.CreateResponse(HttpStatusCode.ExpectationFailed);
            }
        }
    }
}