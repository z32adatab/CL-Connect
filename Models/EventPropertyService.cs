using System;
using System.Linq;
using CampusLogicEvents.Implementation;
using log4net;

namespace CampusLogicEvents.Web.Models
{
    public static class EventPropertyService
    {
        private static readonly ILog logger = LogManager.GetLogger("AdoNetAppender");

        public static void UpdateEventPropertyData()
        {
            var updateFromPmSuccessful = EventPropertyManager.Instance.TryUpdateProperties();
            using (var dbContext = new CampusLogicContext())
            {
                // if PM data is available, backup to local CL DB
                if (updateFromPmSuccessful)
                {
                    using (var tran = dbContext.Database.BeginTransaction())
                    {
                        try
                        {
                            dbContext.Database.ExecuteSqlCommand("DELETE FROM [dbo].[EventProperty]");

                            dbContext.EventProperty.AddRange(EventPropertyManager.Instance.EventProperties.Select(p =>
                                new EventProperty
                                {
                                    Id = p.Id,
                                    Name = p.Name,
                                    DisplayName = p.DisplayName,
                                    DisplayFormula = p.DisplayFormula
                                }));

                            tran.Commit();
                            dbContext.SaveChanges();
                        }
                        catch (Exception e)
                        {
                            tran.Rollback();
                            logger.Error($"EventPropertyService UpdateEventPropertyData Error: {e}");
                        }
                    }
                }
                // else, use the local CL Connect data
                else
                {
                    var properties = dbContext.EventProperty.Select(p => new Implementation.Models.EventProperty
                    {
                        Id = p.Id,
                        Name = p.Name,
                        DisplayName = p.DisplayName,
                        DisplayFormula = p.DisplayFormula
                    });
                    EventPropertyManager.Instance.EventProperties = properties.ToList();
                }
            }
        }
    }
}