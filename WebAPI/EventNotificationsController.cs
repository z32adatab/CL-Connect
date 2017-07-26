using CampusLogicEvents.Implementation;
using CampusLogicEvents.Web.Models;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace CampusLogicEvents.Web.WebAPI
{
    public class EventNotificationsController : ApiController
    {
        private readonly IEnumerable<EventNotificationType> eventNotificationTypes = new EventNotificationType[] {
            new EventNotificationType { EventNotificationTypeId = "DatabaseCommandNonQuery", IsCommandAttributeRequired = true, IsFileStoreTypeRequired = false, IsConnectionStringRequired = true, IsDocumentSettingsRequired = false, IsBatchProcessingRequired = false, Label = "Command" },
            new EventNotificationType { EventNotificationTypeId = "DatabaseStoredProcedure", IsCommandAttributeRequired = true, IsFileStoreTypeRequired = false, IsConnectionStringRequired = true, IsDocumentSettingsRequired = false, IsBatchProcessingRequired = false, Label = "Procedure" },
            new EventNotificationType { EventNotificationTypeId = "DocumentRetrieval", IsCommandAttributeRequired = false, IsFileStoreTypeRequired = false, IsConnectionStringRequired = false, IsDocumentSettingsRequired = true, IsBatchProcessingRequired = false, Label = "Document Retrieval" },
            new EventNotificationType { EventNotificationTypeId = "DocumentRetrievalAndStoredProc", IsCommandAttributeRequired = true, IsFileStoreTypeRequired = false, IsConnectionStringRequired = true, IsDocumentSettingsRequired = true, IsBatchProcessingRequired = false, Label = "Document retrieval / procedure" },
            new EventNotificationType { EventNotificationTypeId = "DocumentRetrievalAndNonQuery", IsCommandAttributeRequired = true, IsFileStoreTypeRequired = false, IsConnectionStringRequired = true, IsDocumentSettingsRequired = true, IsBatchProcessingRequired = false, Label = "Document retrieval / command" },
            new EventNotificationType { EventNotificationTypeId = "FileStore", IsCommandAttributeRequired = false, IsConnectionStringRequired = false, IsFileStoreTypeRequired = true, IsDocumentSettingsRequired = false, IsBatchProcessingRequired = false, Label = "File Store" },
            new EventNotificationType { EventNotificationTypeId = "FileStoreAndDocumentRetrieval", IsCommandAttributeRequired = false, IsConnectionStringRequired = false, IsFileStoreTypeRequired = true, IsDocumentSettingsRequired = true, IsBatchProcessingRequired = false, Label = "File Store / Document Retrieval" },
            new EventNotificationType { EventNotificationTypeId = "AwardLetterPrint", IsCommandAttributeRequired = false, IsConnectionStringRequired = false, IsFileStoreTypeRequired = false, IsDocumentSettingsRequired = false, IsBatchProcessingRequired = false, Label = "Print" },
            new EventNotificationType { EventNotificationTypeId = "BatchProcessingAwardLetterPrint", IsCommandAttributeRequired = false, IsConnectionStringRequired = false, IsFileStoreTypeRequired = false, IsDocumentSettingsRequired = false, IsBatchProcessingRequired = true, Label = "Batch Processing / AwardLetter Print" }
        };

        [HttpGet]
        public HttpResponseMessage EventNotificationTypes()
        {
            return Request.CreateResponse(HttpStatusCode.OK, eventNotificationTypes);
        }

        [HttpGet]
        public HttpResponseMessage TestConnectionString(string connectionString)
        {
            string result = string.Empty;
            HttpResponseMessage httpResponseMessage = null;

            result = ClientDatabaseManager.TestConnectionString(connectionString);

            if (string.IsNullOrEmpty(result))
                httpResponseMessage = Request.CreateResponse(HttpStatusCode.OK);
            else
                httpResponseMessage = Request.CreateErrorResponse(HttpStatusCode.BadRequest, result);

            return httpResponseMessage;
        }
    }
}