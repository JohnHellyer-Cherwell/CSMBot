using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using Microsoft.Bot.Connector;
using System.Diagnostics;
using Newtonsoft.Json;
using SupportBot.Cherwell;
using System.Globalization;
using System.Threading;
using System.Text.RegularExpressions;

namespace SupportBot.Dialogs
{
	[Serializable]
	public class RootDialog : IDialog<object>
	{
		private static readonly Regex _specificIncidentRegEx = new Regex(@"(?:Status of ticket #)(?<PublicID>\d+)", RegexOptions.IgnoreCase);

		public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
		{
			IMessageActivity message = await result;

			if (!context.GetUserWelcomed())
			{
				await context.PostAsync(string.Format("Hi, I'm the Cherwell Support bot."));
				context.StoreUserWelcomed(true);
			}

			await ShowStartingOptions(context, message);
		}

		private async Task ShowStartingOptions(IDialogContext context, IMessageActivity message)
		{
			if (message != null)
				PopulatePrivateData(context, message);

			try
			{
				//pull
				Cherwell.LongRunningPull.AddNotificationList(context);
			}
			catch (Exception X)
			{

				Trace.TraceError("Cherwell.LongRunningPull.AddNotificationList " + X.Message);
			}

			
			if (ValidCherwellInstance(context, context.GetTenant()))
			{
				string welcome;
				if (message != null) //means it's the first time for the user here
				{
					string friendlyName = context.GetUserFriendlyName();
					if (!string.IsNullOrEmpty(friendlyName))
						friendlyName = " " + friendlyName;

					welcome = $"Hi{friendlyName}, how I can help you?";
				}
				else
				{
					welcome = "Let's start again! What would you like to do next?";
				}
				
				IMessageActivity initialActivity = context.MakeMessage();
				initialActivity.AttachmentLayout = AttachmentLayoutTypes.List;

				HeroCard heroCard = GetOptionsForDialog(welcome);
				
				initialActivity.Attachments = new List<Attachment> { heroCard.ToAttachment() };
				await context.PostAsync(initialActivity);
				context.Wait(OnOptionSelected);
			}
			else
			{
				await context.PostAsync("Oops, looks like your admin hasn't configured me yet. Let him know to configure (LINK TO HELP FILE) and come back to me!");
				context.Wait(this.MessageReceivedAsync);
			}
		}

		private async Task OnOptionSelected(IDialogContext context, IAwaitable<IMessageActivity> result)
		{
			try
			{
				IMessageActivity message = await result;

				string optionSelected = message.Text;
				string optionSelectedLower = optionSelected?.ToLower();

				if (optionSelected == "I have a problem!" ||
					optionSelectedLower == "problem" ||
					optionSelectedLower == "issue" ||
					optionSelectedLower == "help")
				{
					context.Call(new SearchDialog(), this.ResumeAfterOptionDialog);
					return;
				}
				else if (optionSelected == "Status of open tickets?" ||
					optionSelectedLower == "status" ||
					optionSelectedLower == "state")
				{
					context.Call(new ManageIncidentsDialog(), this.ResumeAfterOptionDialog);
					return;
				}
				else if (_specificIncidentRegEx.IsMatch(optionSelected))
				{
					Match match = _specificIncidentRegEx.Match(optionSelected);
					string publicId = match.Groups["PublicID"].Value;
					context.Call(new ManageIncidentsDialog(publicId), this.ResumeAfterOptionDialog);
					return;
				}
				else
				{
					IMessageActivity reply = context.MakeMessage();

					string helpMessage = "I don't understand. Here's what I can help you with.";
					HeroCard heroCard = GetOptionsForDialog(helpMessage);

					reply.Attachments = new List<Attachment> { heroCard.ToAttachment() };
					await context.PostAsync(reply);
					context.Wait(OnOptionSelected);
				}

			}
			catch (TooManyAttemptsException ex)
			{
				await context.PostAsync($"Ooops! Too many attemps :(. But don't worry, I'm handling that exception and you can try again!");

				context.Wait(this.MessageReceivedAsync);
			}
		}


		public async Task StartAsync(IDialogContext context)
		{
			context.Wait(this.MessageReceivedAsync);
		}

		private async Task ResumeAfterOptionDialog(IDialogContext context, IAwaitable<object> result)
		{
			await ShowStartingOptions(context, context.MakeMessage());
		}

		private void PopulatePrivateData(IDialogContext context, IMessageActivity message)
		{
			// TODO - Quick and dirty way to retrieve the user from the message context. 
			//        and if running in the emulator use the Windows username so there's something useful.
			if (context.GetUser() == "invalid user")
			{
				string fullName = message.From.Name;

				if (fullName.IndexOf('.') > 0)
				{
					fullName = fullName.Replace(".", " ");
				}

				context.StoreUser(Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(fullName));
				context.StoreUserFriendlyName("");
			}
			else if (context.GetUser() == "User" && message.ChannelId == "emulator")
			{
				string username = Environment.UserName;
				int indexof = username.IndexOf("\\");
				if (indexof >= 0)
					username = username.Substring(0, indexof);
				
				indexof = username.IndexOf("@");
				if (indexof >=0)
					username = username.Substring(0, (username.Length - ++indexof));
				

				if (username.IndexOf('.') > 0)
				{
					username = username.Replace(".", " ");
				}

				context.StoreUser(Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(username));
				context.StoreUserFriendlyName("");
			}

			if (string.IsNullOrEmpty(context.GetUserFriendlyName()))
			{
				string fullName = context.GetUser();
				if (fullName.IndexOf('.') > 0)
				{
					fullName = fullName.Replace(".", " ");
				}
				
				string firstName = fullName;
				int spaceIndex = firstName.IndexOf(" ");
				if (spaceIndex > 0)
					firstName = firstName.Substring(0, spaceIndex);
								
				context.StoreUser(fullName);
				context.StoreUserFriendlyName(firstName);
			}

			if (string.IsNullOrEmpty(context.GetTenant()))
			{
				string tenantidfromMsg = "";
				try
				{
					tenantidfromMsg = message.ChannelData.tenant.id;
				}
				catch
				{
					tenantidfromMsg = "notset";
				}
				context.StoreTenant(tenantidfromMsg);
			}

			context.StoreUserWelcomed(false);
		}

		private static HeroCard GetOptionsForDialog(string messageText)
		{
			//Using 'invoke' string instead of ActionTypes.ImBack so MS Teams doesn't post the selection back to user
			//see https://msdn.microsoft.com/en-us/microsoft-teams/botsmessages#action---invoke-new
			HeroCard heroCard = new HeroCard
			{
				Text = messageText,
				Buttons = new List<CardAction> {
							new CardAction{Type = ActionTypes.ImBack, Title = "I have a problem!", Value = "I have a problem!" },
							new CardAction{Type = ActionTypes.ImBack, Title = "Status of open tickets?", Value = "Status of open tickets?" },       }
			};

			return heroCard;
		}

		private bool ValidCherwellInstance(IDialogContext context, string tenantid)
		{
			Instance instance = InstanceMapping.GetCherwellInstance(tenantid);
			return instance != null;
		}

		
	}
}