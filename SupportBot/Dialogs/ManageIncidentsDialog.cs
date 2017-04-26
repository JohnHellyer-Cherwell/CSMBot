using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.Connector;
using Newtonsoft.Json.Linq;
using SupportBot.Cherwell.Entities;
using System.Web.Hosting;
using SupportBot.Cherwell;
using System.Configuration;
using System.Web;

namespace SupportBot
{
	public class myTexts
	{
		public static string hi = "What do you mean?";
		public static string noTicket = "You have no open tickets!";
		public static string myCommand = "myTicket";
		public static string letMeKnowComments = "What's your comment?";
		
		
		public static string supportPortalUrl = ConfigurationManager.AppSettings["Portal.Endpoint"] + "/IT/Command/Queries.GoToRecord?BusObID={0}&PublicID={1}&EditMode=True";
		public static string error = "We have internal connectivity problems now, please retry later";
	}

	[Serializable]
	public class ManageIncidentsDialog : IDialog<object>
	{
		private string _publicID;

		public ManageIncidentsDialog()
		{
		}

		public ManageIncidentsDialog(string publicID)
		{
			this._publicID = publicID;
		}

		/// <summary>
		/// Statring point of Dialog
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public async Task StartAsync(IDialogContext context)
		{
			// Load my Incidents
			IMessageActivity reply = context.MakeMessage();
			reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
			IList<Attachment> incidentData = GetIncidentData(context);

			if (incidentData == null)
			{
				//Error
				await context.PostAsync(myTexts.error);
				context.Done(context);
				return;
			}
			if (incidentData.Count > 0)
			{
				reply.Attachments = incidentData;
				await context.PostAsync(reply);
				context.Wait(this.OnTicketButton);
			}
			else
			{
				await context.PostAsync(myTexts.noTicket);
				context.Done(context);
			}

		}

		/// <summary>
		/// Retrieve the user's incident or incidents
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		private IList<Attachment> GetIncidentData(IDialogContext context)
		{
			IList<Attachment> incidentData = null;
			try
			{
				incidentData = (_publicID == null) ? GetIncidentList(context) : GetSpecificIncedent(context, _publicID);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Trace.TraceError(ex.ToString());
			}

			return incidentData;
		}

		/// <summary>
		/// On Ticket Button pressed back
		/// </summary>
		/// <param name="context"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		public virtual async Task OnTicketButton(IDialogContext context, IAwaitable<IMessageActivity> result)
		{
			IMessageActivity message = await result;
			//Load command base on pressed Button
			ListTicketCommand myCommand = getMyListTicketCommand(context, message.Text);
			context.PrivateConversationData.RemoveValue(myTexts.myCommand);
			context.PrivateConversationData.SetValue<ListTicketCommand>(myTexts.myCommand, myCommand);
			switch (myCommand.Action)
			{
				case 0:
					//Add comment to Ticket
					context.AllowNotifications(false);
					PromptDialog.Text(context, this.updateTicketComments, myTexts.letMeKnowComments, null, 3);
					
					//Return control to user to answer
					break;

				case 1:
					//Confirm is the user want to close ticket
					context.AllowNotifications(false);
					await context.PostAsync("Cancel ticket? " + myCommand.busObPublicId);
					PromptDialog.Confirm(context, AfterConfirming_CancelAsync, "Are you sure?", promptStyle: PromptStyle.Auto);
					break;

				default:
					string actionLower = message.Text.ToLower();
					if (actionLower == "start over" ||
						actionLower == "start again" ||
						actionLower == "restart")
					{
						await context.PostAsync("Ok, let's start again.");
					}
					else
					{
						// Bot don't know what user wants
						IMessageActivity reply = context.MakeMessage();
						HeroCard heroCard = new HeroCard
						{
							Text = "Sorry. I'm not sure what you mean. Here are your tickets or you can start over.",
							Buttons = new List<CardAction> { new CardAction{Type = ActionTypes.ImBack, Title = "Start Over", Value = "Start Over" },       }
						};
						reply.Attachments = new List<Attachment> { heroCard.ToAttachment() };
						await context.PostAsync(reply);

						reply = context.MakeMessage();
						reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
						reply.Attachments = GetIncidentData(context);
						await context.PostAsync(reply);
						context.Wait(this.OnTicketButton);
						return;
					}

					context.Done(result);
					break;
			}



		}
		/// <summary>
		/// Read command from private data base on Message text
		/// </summary>
		/// <param name="context"></param>
		/// <param name="data"></param>
		/// <returns></returns>
		private static ListTicketCommand getMyListTicketCommand(IDialogContext context, string data)
		{
			//"Give me a {0} seconds..."  
			ListTicketCommand myCommand = null;
			if (context.PrivateConversationData.ContainsKey(data))
			{
				myCommand = context.PrivateConversationData.Get<ListTicketCommand>(data);
			}
			else
				myCommand = new ListTicketCommand() { Action = -1 };

			return myCommand;
		}
		/// <summary>
		/// Generate internal KEY to store commnads
		/// </summary>
		/// <param name="X"></param>
		/// <returns></returns>
		private static string getCommandKey(int X)
		{
			return string.Format("Give me {0} seconds.", X);
		}



		/// <summary>
		/// withdraw action
		/// </summary>
		/// <param name="context"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		private async Task AfterConfirming_CancelAsync(IDialogContext context, IAwaitable<bool> result)
		{
			bool Confirmation = await result;
			ListTicketCommand myCommand = context.PrivateConversationData.Get<ListTicketCommand>(myTexts.myCommand);
			if (Confirmation)
			{
				//withdraw ticket
				InstanceMapping.WithDrawTicket(myCommand.busObId, myCommand.busObPublicId, myCommand.busObRecId);
				await context.PostAsync("Thanks, ticket #" + myCommand.busObPublicId + " has been withdrawn." );
			}
			else
			{
				await context.PostAsync("Ok, I did not cancel your ticket");
			}

			context.AllowNotifications();
		}

		/// <summary>
		/// Add comment to a Ticket
		/// </summary>
		/// <param name="context"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		private async Task updateTicketComments(IDialogContext context, IAwaitable<string> result)
		{
			try
			{
				string message = await result;
				ListTicketCommand myCommand = context.PrivateConversationData.Get<ListTicketCommand>(myTexts.myCommand);
				InstanceMapping.AddTicketComment(myCommand.busObId, myCommand.busObPublicId, myCommand.busObRecId, message);
				await context.PostAsync(string.Format("Comment added to Incident #{0}", myCommand.busObPublicId));
				context.AllowNotifications();
			}
			catch (Exception)
			{

				throw;
			}

		}

		/// <summary>
		/// Build actions for all of the user's Incidents. 
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		private static IList<Attachment> GetIncidentList(IDialogContext context)
		{
			List<Attachment> atList = new List<Attachment>();
			string userName = context.GetUser();
			
			JObject myData = JObject.Parse(SupportBot.Cherwell.InstanceMapping.GetIncedentList(userName));

			//Button acction ID
			int accId = 1;
			foreach (JToken businessObject in myData["businessObjects"].Take(10)) // MSTeams doesn't seem to like more than 10 cards
			{
				Attachment attachment = BusObToAttachement(businessObject, context,  ref accId);
				atList.Add(attachment);
			}
			return atList;
		}

		/// <summary>
		/// Build an action card for a specific Incident.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="incidentID">PublicID of the specific Incidnet to retrieve</param>
		/// <returns></returns>
		private static IList<Attachment> GetSpecificIncedent(IDialogContext context, string incidentID)
		{
			List<Attachment> atList = new List<Attachment>(1);
			string userName = context.GetUser();

			JObject myData = JObject.Parse(SupportBot.Cherwell.InstanceMapping.GetSpecificIncedent(userName, incidentID));

			//Button acction ID
			int accId = 1;
			JToken businessObject = myData["businessObjects"].FirstOrDefault();
			if (businessObject != null)
			{
				Attachment attachment = BusObToAttachement(businessObject, context, ref accId);
				atList.Add(attachment);
			}

			
			return atList;
		}


		/// <summary>
		/// Build an Attachment from a BusinessObject
		/// </summary>
		/// <param name="businessObject"></param>
		/// <returns></returns>
		private static Attachment BusObToAttachement(JToken businessObject, IDialogContext context, ref int counter)
		{
			
			List<CardAction> myActions = new List<CardAction>();
			//Ticket Command
			ListTicketCommand myCommand = new ListTicketCommand
			{
				Action = 0,
				busObId = businessObject["busObId"].ToString(),
				busObPublicId = businessObject["busObPublicId"].ToString(),
				busObRecId = businessObject["busObRecId"].ToString()
			};

			//First Action Add Comments               
			string myKey = getCommandKey(counter);
			context.PrivateConversationData.RemoveValue(myKey);
			context.PrivateConversationData.SetValue<ListTicketCommand>(myKey, myCommand);
			myActions.Add(new CardAction(ActionTypes.ImBack, "Add comments", null, myKey));
			counter += 1;

			//Second Actions Cancel
			myCommand.Action = 1;
			myKey = getCommandKey(counter);
			context.PrivateConversationData.SetValue<ListTicketCommand>(myKey, myCommand);
			myActions.Add(new CardAction(ActionTypes.ImBack, "Withdraw", null, myKey));
			counter += 1;

			//Third action GO to URL
			CardAction imgAction = new CardAction(ActionTypes.OpenUrl, "More details", null, string.Format(myTexts.supportPortalUrl, myCommand.busObId, myCommand.busObPublicId));
			myActions.Add(imgAction);

			//Image URL
			string imgUrl = Images.IncidentImageUrl;

			if (businessObject["fields"][3]["value"].ToString() == "Service Request")
				imgUrl = Images.ServiceRequestUrl;

			// Create Card
			ThumbnailCard heroCard = new ThumbnailCard
			{
				Title = String.Format("{0} #{1}", businessObject["fields"][3]["value"].ToString(), businessObject["fields"][0]["value"]),
				Subtitle = businessObject["fields"][1]["name"] + " " + businessObject["fields"][1]["value"],
				Text = businessObject["fields"][2]["value"].ToString(),
				Images = new List<CardImage>() { new CardImage(url: imgUrl) },
				Buttons = myActions
			};

			return heroCard.ToAttachment();
		}
	}


}