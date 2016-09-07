namespace CampusLogicEvents.Web.Models
{
    public class EventNotificationType
    {
        public string EventNotificationTypeId { get; set; }
        public string Label { get; set; }
        public bool IsConnectionStringRequired { get; set; }
        public bool IsFileStoreTypeRequired { get; set; }
        public bool IsCommandAttributeRequired { get; set; }
        public bool IsDocumentSettingsRequired { get; set; }
    }
}