using Microsoft.Bot.Builder.Dialogs;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SupportBot.Cherwell.Entities
{
    public class NotificationDataEntity : TableEntity
    {
        public NotificationDataEntity(string ChannelId, string User)
        {
            this.RowKey = ChannelId;
            this.PartitionKey = User;
        }

        public NotificationDataEntity() { }

        public string MessageActivity { get; set; }
      

    }
}