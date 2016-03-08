using CampusLogicEvents.Implementation;
using log4net;
using System;
using System.Linq;
using Hangfire;

namespace CampusLogicEvents.Web.Models
{
    public static class ISIRService
    {
        private static readonly ILog logger = LogManager.GetLogger("AdoNetAppender");

        /// <summary>
        /// Batches ISIR Corrections
        /// Runs on a daily scheduled basis.  Configured in the Startup.cs and the web.config
        /// </summary>
        [AutomaticRetry(Attempts = 0)]
        public static void ISIRCorrections()
        {
            try
            {
                DocumentManager manager = new DocumentManager();
                
                //Batch todays ISIR Corretions
                var result = manager.BatchISIRCorrections().Result;

                //Log all(if any) of the error emails sent during the Automated ISIR Batch process
                NotificationService.LogNotifications(result.NotificationDataList);

                //result will be used for TD Client Processing

            }
            catch (Exception ex)
            {
                NotificationService.ErrorNotification("Automated ISIR Corrections Process", ex);
                logger.ErrorFormat("ISIRService ISIRUpload Error: {0}", ex);
            }

        }

    }
}