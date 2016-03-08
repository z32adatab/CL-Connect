﻿using System.Configuration;
﻿using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace CampusLogicEvents.Web.Filters
{
    public class IdentityBasicAuthenticationAttribute : BasicAuthenticationAttribute
    {
        protected override async Task<IPrincipal> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken)
        {
            if (!(userName == ConfigurationManager.AppSettings["IncomingAPIUsername"] &&
                  password == ConfigurationManager.AppSettings["IncomingAPIPassword"]))
            {
                return null;
            }

            var identity = new GenericIdentity(userName);
            return new ClaimsPrincipal(identity);
        }
    }
}