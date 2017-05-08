using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Connector;
using SupportBot.Cherwell.Entities;
using SupportBot.Cherwell.Search;
using SupportBot.Cherwell;
using Newtonsoft.Json;
using System.Configuration;
using System.Diagnostics;

namespace SupportBot.Dialogs
{
	[Serializable]
	public class SearchDialog : IDialog<object>
	{
		private const string startingMsg = "Ok, let's try finding a solution. What do you need help with?";

		public async Task StartAsync(IDialogContext context)
		{
			await context.PostAsync(startingMsg);
			context.Wait(ProblemSentFromUserAsync);
		}

		public async Task ProblemSentFromUserAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
		{
			IMessageActivity message = await result;
			await ProblemSearch(context, message);
		}

		internal async Task ProblemSearch(IDialogContext context, IMessageActivity message)
		{
			try
			{
				context.AllowNotifications(false);
				context.StoreUserIssue(message.Text);
				string tenantid = context.GetTenant();
				Instance instance = InstanceMapping.GetCherwellInstance(tenantid);

				//Look for service health status with what's in message.Text
				Task<string> serviceHealthReplyTask = Search.GetServiceHealthAsync(message.Text, instance);
				Task<IList<Attachment>> alertsReplyAttachmentsTask = Search.GetAlertsAsync(message.Text, instance);
				Task<IList<Attachment>> kbaseResultAttachmentsTask = Search.GetKBaseAsync(message.Text, instance);
				Task<string> staticHelpResultsTask = Search.GetQnAAnswerAsync(message.Text);
				
				await Task.WhenAll(
					serviceHealthReplyTask,
					alertsReplyAttachmentsTask,
					kbaseResultAttachmentsTask,
					staticHelpResultsTask
				);

				string serviceHealthReply = serviceHealthReplyTask.Result;
				IList<Attachment> alertsReplyAttachments = alertsReplyAttachmentsTask.Result;
				IList<Attachment> kbaseResultAttachments = kbaseResultAttachmentsTask.Result;
				string staticHelpResults = staticHelpResultsTask.Result;

				if (string.IsNullOrEmpty(serviceHealthReply) 
					&& alertsReplyAttachments?.Count == 0
					&& string.IsNullOrWhiteSpace(staticHelpResults)
					&& kbaseResultAttachments?.Count == 0
					)
				{
					await SendSearchQuestion(context, "Sorry I couldn't find any matches in our knowledge base and support databases. Do you want to start from the beginning?", false);
					context.Wait(WaitForUserChoice);
				}
				else
				{
					//Show QNAMaker results (if they exist)
					if (!string.IsNullOrEmpty(staticHelpResults))
					{
						await context.PostAsync(staticHelpResults);
					}

					//Look for alerts with what's in the message.Text
					if (alertsReplyAttachments != null && alertsReplyAttachments.Count > 0)
					{
						IMessageActivity alertsReply = context.MakeMessage();
						alertsReply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
						alertsReply.Attachments = alertsReplyAttachments;
						await context.PostAsync(alertsReply);
					}

					if (kbaseResultAttachments != null && kbaseResultAttachments.Count > 0)
					{
						IMessageActivity kbaseReply = context.MakeMessage();
						kbaseReply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
						kbaseReply.Attachments = kbaseResultAttachments;
						await context.PostAsync(kbaseReply);
					}

					if (!string.IsNullOrEmpty(serviceHealthReply))
					{
						string serviceHealthPost = $"**It appears as though the {serviceHealthReply} is experiencing some level of system failure**";
						await context.PostAsync(serviceHealthPost);
					}
					await SendSearchQuestion(context, "Was any of the above useful?");
					context.Wait(WaitForUserChoice);
				}
			}
			catch (Exception ex)
			{
				Trace.TraceError(ex.ToString());
			}
		}

		private async Task SendSearchQuestion(IDialogContext context, string message, bool showStartFromBeginning = true)
		{
			IMessageActivity searchReply = context.MakeMessage();
			searchReply.AttachmentLayout = AttachmentLayoutTypes.List;

			//Using 'invoke' string instead of ActionTypes.ImBack so MS Teams doesn't post the selection back to user
			//see https://msdn.microsoft.com/en-us/microsoft-teams/botsmessages#action---invoke-new
			HeroCard heroCard = new HeroCard
			{
				Text = message,
				Buttons = new List<CardAction> {
						new CardAction{Type = ActionTypes.ImBack, Title = "Yes", Value = "Yes" },
						new CardAction{Type = ActionTypes.ImBack, Title = "No, create a ticket", Value = "No, create a ticket" },
						new CardAction{Type = ActionTypes.ImBack, Title = "Repeat Search", Value = "Repeat Search" }                    }
			};

			if (showStartFromBeginning)
				heroCard.Buttons.Add(new CardAction { Type = ActionTypes.ImBack, Title = "Start from the beginning", Value = "Start from the beginning" });

			searchReply.Attachments = new List<Attachment> { heroCard.ToAttachment() };
			await context.PostAsync(searchReply);
		}

		public virtual async Task WaitForUserChoice(IDialogContext context, IAwaitable<IMessageActivity> result)
		{
			IMessageActivity message = await result;
			try
			{
				string botMessage;
				string userIssue = context.GetUserIssue();
				string user = context.GetUser();

				switch (message.Text)
				{
					case "No, create a ticket":
					case "create a ticket":
						PromptDialog.Text(context, this.CreateIncident, "What information should I include in the ticket?", null, 3);
						return;

					case "Yes":
						// log that we were able to help the user
						bool subscribed = await Search.LogCallDeflection(
							InstanceMapping.GetCherwellInstance(context.GetTenant()),
							$"Problem searched: {userIssue}",
							context.GetUser());
						
						
						await context.PostAsync("Great! Glad I could help.");
						context.AllowNotifications();
						context.Done(result);
						return;

					case "Repeat Search":
						// perform the same search again
						message.Text = userIssue;
						await ProblemSearch(context, message);						
						return;

					case "Start from the beginning":
					case "start over":
					case "restart":
					case "start again":
						await context.PostAsync("Ok, let's restart.");
						context.AllowNotifications();
						context.Done(result);
						return;

					default:
						//this means they clicked a button in one of the hero cards
						break;
				}

				//if we made it here it's because user clicked a button from results instead of last question
				IInvokeActivity invokedAction = message.AsInvokeActivity();
				if (invokedAction != null)
				{
					string value = invokedAction.Value.ToString();
					ListTicketCommand myCommand = null;
					if (value.IsValidJson<ListTicketCommand>(out myCommand))
					{
						switch (myCommand.Action)
						{
							//Subscribe button from Alerts
							case 100:
								bool subscribed = await Search.SubscribeToIssue(
									InstanceMapping.GetCherwellInstance(context.GetTenant()),
									myCommand.busObPublicId,
									context.GetUser()
									);
								if (subscribed)
								{
									botMessage = "Done, you're subscribed to the issue!";
									await context.PostAsync(botMessage);
									return;
								}
								else
								{
									botMessage = "Sorry, I couldn't subscribe you to the issue...";
									await context.PostAsync(botMessage);
								}
								break;
							//Like button from KBase 
							case 200:
								bool liked = await Search.LikeKBase(
									InstanceMapping.GetCherwellInstance(context.GetTenant()),
									myCommand.busObPublicId,
									context.GetUser(),
									true);
								if (liked)
								{
									botMessage = "Done, you've liked that KB article!";
									await context.PostAsync(botMessage);
									return;
								}
								else
								{
									botMessage = "Sorry, I couldn't like the article for you, I ran into an issue...";
									await context.PostAsync(botMessage);
								}
								break;
							//Dislike buttom from KBase
							case 201:
								bool disliked = await Search.LikeKBase(
									InstanceMapping.GetCherwellInstance(context.GetTenant()),
									myCommand.busObPublicId,
									context.GetUser(),
									false);
								if (disliked)
								{
									botMessage = "Done, I've disliked the article for you.";
									await context.PostAsync(botMessage);
									return;
								}
								else
								{
									botMessage = "Sorry, I couldn't dislike the article for you, I ran into an issue...";
									await context.PostAsync(botMessage);
								}
								break;
						}
					}
				}
				else
				{
					// treat this as the user wants to do another search.
					await ProblemSearch(context, message);
					return;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Trace.TraceError(ex.ToString());
			}
			context.AllowNotifications();
			context.Done(result);
		}

		/// <summary>
		/// Add comment to a Ticket
		/// </summary>
		/// <param name="context"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		private async Task CreateIncident(IDialogContext context, IAwaitable<string> result)
		{
			try
			{
				string incidentText = await result;
				
				string botMessage = "Ok, I'll create a ticket for you. Give me a few, I'll let you know when I'm done.";
				await context.PostAsync(botMessage);

				await InstanceMapping.CreateIncident(InstanceMapping.GetCherwellInstance(context.GetTenant()), incidentText, context.GetUser());
				context.Done(result);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Trace.TraceError(ex.ToString());
				throw;
			}
			finally
			{
				context.AllowNotifications();
			}
		}
	}
}