using Autofac;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Connector;
using SupportBot.Dialogs;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace SupportBot
{
	[BotAuthentication]
	public class MessagesController : ApiController
	{
		/// <summary>
		/// POST: api/Messages
		/// Receive a message from a user and reply to it
		/// </summary>
		public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
		{
			if (activity.Type == ActivityTypes.Message | activity.Type == ActivityTypes.Invoke)
			{
				// Set the last activity date for the conversation.
				await SetLastActivityDateAsync(activity);

				// Send the activity to the bot for processing.
				await Conversation.SendAsync(activity, () => new RootDialog());
			}
			else
			{
				await HandleSystemMessage(activity);
			}

			HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.OK);
			return response;
		}

		private async Task<HttpResponseMessage> HandleSystemMessage(Activity message)
		{
			if (message.Type == ActivityTypes.DeleteUserData)
			{
				// Implement user deletion here
				// If we handle user deletion, return a real message
			}
			else if (message.Type == ActivityTypes.ConversationUpdate)
			{
				// Handle conversation state changes, like members being added and removed
				// Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
				// Not available in all channels
				IConversationUpdateActivity conversationupdate = message;
				using (ILifetimeScope scope = DialogModule.BeginLifetimeScope(Conversation.Container, message))
				{
					IConnectorClient client = scope.Resolve<IConnectorClient>();
					if (conversationupdate.MembersAdded.Any())
					{
						Activity reply = message.CreateReply();
						foreach (ChannelAccount newMember in conversationupdate.MembersAdded)
						{
							if (newMember.Id != message.Recipient.Id)
							{
								reply.Text = $"Welcome {newMember.Name}! ";
							}
							else
							{
								reply.Text = $"Welcome {message.From.Name}";
							}
							await client.Conversations.ReplyToActivityAsync(reply);
						}
					}
				}
			}
			else if (message.Type == ActivityTypes.ContactRelationUpdate)
			{
				// Handle add/remove from contact lists
				// Activity.From + Activity.Action represent what happened
			}
			else if (message.Type == ActivityTypes.Typing)
			{
				// Handle knowing tha the user is typing
			}
			else if (message.Type == ActivityTypes.Ping)
			{
			}

			return null;
		}

		/// <summary>
		/// Sets the last activity date for the specified <see cref="Activity"/>.
		/// </summary>
		/// <param name="serviceUrl">The <see cref="Activity"/> for which to set last activity.</param>
		/// <returns>The <see cref="Task"/> for the async operation.</returns>
		private async Task SetLastActivityDateAsync(Activity activity)
		{
			try
			{
				string channelId = activity.ChannelId;
				string conversationId = activity.Conversation.Id;

				using (ILifetimeScope scope = DialogModule.BeginLifetimeScope(Conversation.Container, activity))
				{
					IConnectorClient client = scope.Resolve<IConnectorClient>();
					IStateClient sc = scope.Resolve<IStateClient>();
					BotData conversationData = await sc.BotState.GetConversationDataAsync(channelId, conversationId);

					conversationData.SetProperty(ContextConstants.LastActivity, DateTime.UtcNow);
					await sc.BotState.SetConversationDataAsync(channelId, conversationId, conversationData);
				}
			}
			catch (Exception ex)
			{
				if (Debugger.IsAttached)
					Debugger.Break();

				Trace.TraceError(ex.ToString());
			}
		}
	}
}