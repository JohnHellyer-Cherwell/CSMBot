using Microsoft.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SupportBot.Cherwell.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;

namespace SupportBot.Cherwell
{
	public static class LongRunningPull
	{
		private static bool runningSwitch;
        private static object SingletonSwitch=null;
		private static string label;
		public static int PullSeconds { get; set; }

		/// <summary>
		/// Send Proactive Message to user
		/// </summary>
		/// <param name="incomingMessage"></param>
		/// <param name="textMessage"></param>
		/// <param name="attachments"></param>
		/// <returns></returns>
		private static async Task ActionNotification(IMessageActivity incomingMessage, string textMessage, IList<Attachment> attachments = null)
		{
			ChannelAccount userAccount = incomingMessage.Recipient;
			ConnectorClient connector = new ConnectorClient(new Uri(incomingMessage.ServiceUrl));
			IMessageActivity message = Activity.CreateMessageActivity();
			message.From = incomingMessage.From;
			message.Recipient = incomingMessage.Recipient;
			message.Conversation = incomingMessage.Conversation;

			message.Text = string.Format(textMessage);
			message.Locale = "en-Us";
			message.Attachments = attachments;
			
			await connector.Conversations.SendToConversationAsync((Activity)message);
		}

        /// <summary>
        /// Execute Notification actions for a specific User:fields["CustomerDisplayName"]
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="checkmarkUrl"></param>
        /// <param name="busObPublicId"></param>
        /// <param name="busObRecId"></param>
        /// <returns></returns>
        private static async Task ExecutePullActionAsync(Dictionary<string, string> fields, string checkmarkUrl, string busObPublicId, string busObRecId)
        {

            string userName = fields["CustomerDisplayName"].ToLower();
           
            // Retrieve the storage account from the connection string.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("NotificationStorage"));
            // Create the table client.
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            // Create the CloudTable object that represents the "people" table.
            CloudTable table = tableClient.GetTableReference("NotificationList");
            table.CreateIfNotExists();
            // Construct the query operation for all customer entities where PartitionKey="Smith".
            TableQuery<NotificationDataEntity> notificationList = new TableQuery<NotificationDataEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, userName));

			// Print the fields for each customer.
			foreach (NotificationDataEntity userChannelInfo in table.ExecuteQuery(notificationList))
			{
				List<Attachment> attachment = null;

				// TODO: Allow processing of notifications even if no message activity is provided in the user channel info.
				if (userChannelInfo.MessageActivity == null)
					return;

				JObject messageActivityData = JObject.Parse(userChannelInfo.MessageActivity);

				// Determine whether to allow notifications for the conversation by checking the conversation data.
				bool allowNotifications = true;
				string serviceUrl = messageActivityData["serviceUrl"].ToString();
				string conversationId = messageActivityData["conversation"]["id"].ToString();
				JObject conversationData = await GetConversationDataAsync(serviceUrl, userChannelInfo.RowKey, conversationId);
				if (conversationData != null)
				{
					// Obtain the last activity date and whether to allow notifications from the conversation data.
					DateTime lastActivity = TryGetValue(conversationData["data"], ContextConstants.LastActivity, DateTime.UtcNow);
					allowNotifications = TryGetValue(conversationData["data"], ContextConstants.AllowNotifications, true);

					// If notifications are not currently allowed, ensure the delay timeout has not been exceeded. The delay timeout may occur,
					// for example, if the user sits idle for a period of time in the middle of a conversation. If this happens, then
					// we will send notifications to the user even if it interrupts the current conversation flow.
					if (!allowNotifications)
					{
						// Default to no delay timeout if not configured.
						int delayTimeout = 0; 
						int.TryParse(ConfigurationManager.AppSettings["NotificationDelayTimeout"], out delayTimeout);

						// Allow notifications if the notification delay timeout has been exceeded.
						allowNotifications = (DateTime.UtcNow - lastActivity).TotalMinutes > delayTimeout;
					}
				}

				// If notifications are not allowed for the current conversation, continue to the next notification list item.
				if (!allowNotifications)
					continue;

				IMessageActivity newMessage = Activity.CreateMessageActivity();
				newMessage.Type = ActivityTypes.Message;
				ChannelAccount myFrom = new ChannelAccount()
				{
					Id = messageActivityData["from"]["id"].ToString(),
					Name = messageActivityData["from"]["name"].ToString()
				};
				newMessage.From = myFrom;
				ConversationAccount myConversationAccount = new ConversationAccount()
				{
					Id = conversationId
				};
				newMessage.Conversation = myConversationAccount;
				ChannelAccount myRecipient = new ChannelAccount()
				{
					Id = messageActivityData["recipient"]["id"].ToString(),
					Name = messageActivityData["recipient"]["name"].ToString()
				};
				newMessage.Recipient = myRecipient;

				newMessage.Text = "Yo yo yo!";
				newMessage.ServiceUrl = serviceUrl;

				switch (fields["Operation"])
				{
					case "BotMessage-Resolved":
						string surveyLink = $"{ConfigurationManager.AppSettings["Portal.Endpoint"]}/surveyresponse/Command/Queries.GoToRecord?BusObID=93e5787a6fbc8e475c0f464e248c193a1f1cc1704c&RecID={fields["RecIDForSurvey"]}&EditMode=True&username=survey&password=response";

						newMessage.Text = string.Format(
							"Hello {0}, {1} #{2} has been Resolved. [{3}]({4})",
							fields["CustomerDisplayName"],
							fields["Conversation"],
							fields["PublicID"],
							"Please take a Survey",
							surveyLink,
							checkmarkUrl);
						break;
					case "BotMessage-Notify":
						if (!string.IsNullOrEmpty(fields["PublicID"]))
						{
							attachment = new List<Attachment>(1);
							ThumbnailCard card = new ThumbnailCard
							{
								Text = $"Hi {userName}, {fields["Conversation"]}",
								Buttons = new List<CardAction> { new CardAction(ActionTypes.ImBack, "Status", null, $"Status of ticket #{fields["PublicID"]}") }
							};
							attachment.Add(card.ToAttachment());
							newMessage.Text = "";
						}
						else
						{
							newMessage.Text = string.Format(
								"Hi {0}, {1}",
								userName,
								fields["Conversation"]);
						}
						break;
					case "BotMessage-Chat":
						// way for CSM to send any sort of message back to the user. We just print the text sent.
						newMessage.Text = fields["Conversation"].ToString();
						break;
					default:
						Trace.TraceInformation("Not Aciton for user " + userName);
						break;
				}

				try
				{
					//Trigger Action
					await ActionNotification(newMessage, newMessage.Text, attachment);
					//Mark Event processed
					string x = InstanceMapping.UpdateEventNotification(busObPublicId, busObRecId);
					Trace.TraceInformation(x);
				}
				catch (Exception X)
				{
					Trace.TraceError(X.ToString());
				}
			}
        }

		/// <summary>
		/// Attempts to get the value from a <see cref="JToken"/> for the speicified property.
		/// </summary>
		/// <typeparam name="T">The type of value to return.</typeparam>
		/// <param name="token">The <see cref="JToken"/> from which to get a property value.</param>
		/// <param name="propertyName">The name of the property to return.</param>
		/// <param name="defaultValue">The default value for the property if none is found or the value cannot be converted to the specified type.</param>
		/// <returns>The property value or the default value.</returns>
		private static T TryGetValue<T>(JToken token, string propertyName, T defaultValue = default(T))
		{
			T value = defaultValue;

			try
			{
				if (token[propertyName] != null)
				{
					value = token.Value<T>(propertyName);
				}
			}
			catch (Exception ex)
			{
				Trace.TraceError(ex.Message);
			}

			return value;
		}

		/// <summary>
		/// Gets the current conversation data for the specified channel and conversation.
		/// </summary>
		/// <param name="serviceUrl">The Url for the bot service from which to get conversation data.</param>
		/// <param name="channelId">The channel Id for which to get conversation data.</param>
		/// <param name="conversationId">The conversation Id for which to get conversation data.</param>
		/// <returns>The data for the specified conversation.</returns>
		private static async Task<JObject> GetConversationDataAsync(string serviceUrl, string channelId, string conversationId)
		{
			try
			{
				Uri baseAddress = new Uri(serviceUrl);
				using (ConnectorClient connector = new ConnectorClient(baseAddress))
				{
					connector.HttpClient.BaseAddress = baseAddress;

					string getDataUrl = $"/v3/botstate/{channelId}/conversations/{conversationId}";
					HttpResponseMessage message = await connector.HttpClient.GetAsync(getDataUrl);

					if (message.IsSuccessStatusCode)
					{
						return await message.Content.ReadAsAsync<JObject>();
					}
				}
			}
			catch (Exception ex)
			{
				if (Debugger.IsAttached)
					Debugger.Break();

				Trace.TraceError(ex.ToString());
			}

			return null;
		}

		/// <summary>
		/// Always running QueueBackgroundWorkItem for notifications
		/// </summary>
		private static void AlwayRunning()
		{
			string checkmarkUrl = Images.CheckmarkUrl;
            runningSwitch = Convert.ToBoolean(ConfigurationManager.AppSettings["NotificationOn"].ToLower());

            HostingEnvironment.QueueBackgroundWorkItem(async token =>
			{
				while (runningSwitch)
				{
					PullSeconds = int.Parse(ConfigurationManager.AppSettings["NotificationPullSecs"]);
					await Task.Delay(TimeSpan.FromSeconds(PullSeconds));
					Trace.TraceInformation("QueueBackgroundWorkItem {0} start", label);

					try
					{
						//call API
						string xSerachResult = InstanceMapping.ReadEvents();
						Trace.TraceInformation(xSerachResult);
						JObject jSerResult = JObject.Parse(xSerachResult);

						foreach (JToken businessObject in jSerResult["businessObjects"])
						{
							string busObPublicId = businessObject["busObPublicId"].ToString();
							string busObRecId = businessObject["busObRecId"].ToString();

							
							Dictionary<string, string> fields = (from field in businessObject["fields"]
																 select new { Name = field["displayName"].ToString(), Value = field["value"].ToString() })
																 .ToDictionary(k => k.Name, v => v.Value);

                            //Execute Acction for this expecifi notification
                            await ExecutePullActionAsync(fields, checkmarkUrl, busObPublicId, busObRecId);
                            
						}
					}
					catch (Exception X)
					{
						Debug.Fail("QueueBackgroundWorkItem Error" + X.ToString());
						Trace.TraceError("QueueBackgroundWorkItem Error" + X.Message);
					}

                    runningSwitch = Convert.ToBoolean(ConfigurationManager.AppSettings["NotificationOn"].ToLower());
                }
			});
		}

		/// <summary>
		/// Update Global notification List!
		/// </summary>
		/// <param name="myContext"></param>
		private static void AddNotificationReciep(/*string magicKey,*/ IDialogContext myContext)
		{
			
            try
            {
                // Retrieve the storage account from the connection string.
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("NotificationStorage"));
                // Create the table client.
                CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
                // Create the CloudTable object that represents the "people" table.
                CloudTable table = tableClient.GetTableReference("NotificationList");
                table.CreateIfNotExists();
                // Create a new customer entity.
                NotificationDataEntity notification = new NotificationDataEntity(myContext.Activity.ChannelId.ToLower(), myContext.GetUser().ToLower())
                {
                    MessageActivity = Newtonsoft.Json.JsonConvert.SerializeObject(myContext.MakeMessage()),
                   
                };

                // Create the TableOperation object that inserts the customer entity.
                TableOperation insertOperation = TableOperation.InsertOrReplace(notification);
                // Execute the insert operation.
                table.Execute(insertOperation);
            }
            catch (Exception X)
            {

                Trace.TraceError("Error on AddNotificationReciep:  " + X.Message);
            }
   

        }
		/// <summary>
		/// Add user context to notification List
		/// </summary>
		/// <param name="contex"></param>
		public static void AddNotificationList(IDialogContext contex)
		{			
            if (SingletonSwitch==null)
            {
                SingletonSwitch = new object();
                PullSeconds = 10;
                label = DateTime.Now.Ticks.ToString();
                AlwayRunning();
            }

            AddNotificationReciep(contex);
        }
	}
}