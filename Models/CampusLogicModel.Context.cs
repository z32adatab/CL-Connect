﻿//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace CampusLogicEvents.Web.Models
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class CampusLogicContext : DbContext
    {
        public CampusLogicContext()
            : base("name=CampusLogicContext")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public DbSet<AggregatedCounter> AggregatedCounters { get; set; }
        public DbSet<Counter> Counters { get; set; }
        public DbSet<Hash> Hashes { get; set; }
        public DbSet<Job> Jobs { get; set; }
        public DbSet<JobParameter> JobParameters { get; set; }
        public DbSet<JobQueue> JobQueues { get; set; }
        public DbSet<List> Lists { get; set; }
        public DbSet<Schema> Schemata { get; set; }
        public DbSet<Server> Servers { get; set; }
        public DbSet<Set> Sets { get; set; }
        public DbSet<State> States { get; set; }
        public DbSet<ReceivedEvent> ReceivedEvents { get; set; }
        public DbSet<Log> Logs { get; set; }
        public DbSet<NotificationLog> NotificationLogs { get; set; }
        public DbSet<EventNotification> EventNotifications { get; set; }
        public DbSet<BatchProcessRecord> BatchProcessRecords { get; set; }
        public DbSet<PowerFaidsRecord> PowerFaidsRecords { get; set; }
    }
}
