using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SupportBot.Cherwell.Entities;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SupportBot.Cherwell.Search
{
	public static class Search
	{
		//Look for service health status with what's in message.Text
		public static async Task<string> GetServiceHealthAsync(string query, Instance instance)
		{
			string servicePath = instance.ApiUrl + "api/V1/getsearchresults/association/938a03db7c7ed3c89b7d7a4cb5b27ff77593c2d5bb"
								 + "/scope/Global"
								 + "/scopeowner/None"
								 + "/searchname/Troubled%20System%20State"
								 + "?pagenumber=1&pagesize=10&searchTerm=" + query;

			HttpResponseMessage response = await instance.WebApiClient.SubmitRequestAsync(c => c.GetAsync(servicePath));

			JObject responseData = await response.Content.ReadAsAsync<JObject>();
			string serviceHealthStatus = null;
			if (responseData["businessObjects"] != null)
			{
				serviceHealthStatus = (from businessobject in responseData["businessObjects"]
									   from field in businessobject["fields"]
									   where field["name"].ToString() == "FriendlyName"
									   select field["value"].ToString()).FirstOrDefault();
			}

			return serviceHealthStatus;
		}

		public static async Task<IList<Attachment>> GetKBaseAsync(string query, Instance instance)
		{
			List<Attachment> kbaseList = new List<Attachment>();

			string servicePath = instance.ApiUrl + "api/V1/getsearchresults/association/934c68436065e717e2d7ca4e9992f112d80031cedc"
								 + "/scope/Global"
								 + "/scopeowner/None"
								 + "/searchname/Published%20Customer%20Articles"
								 + "?pagenumber=1&pagesize=10&searchTerm=" + query;

			HttpResponseMessage response = await instance.WebApiClient.SubmitRequestAsync(c => c.GetAsync(servicePath));

			JObject responseData = await response.Content.ReadAsAsync<JObject>();

			foreach (JToken businessObject in responseData["businessObjects"])
			{
				List<CardAction> myActions = new List<CardAction>();

				//Like Command
				ListTicketCommand myCommand = new ListTicketCommand
				{
					Action = 200,
					busObId = businessObject["busObId"].ToString(),
					busObPublicId = businessObject["busObPublicId"].ToString(),
					busObRecId = businessObject["busObRecId"].ToString()
				};
				//First Action Add Comments               
				myActions.Add(new CardAction("invoke", "Like", null, JsonConvert.SerializeObject(myCommand)));

				//Diskike Command
				myCommand = new ListTicketCommand
				{
					Action = 201,
					busObId = businessObject["busObId"].ToString(),
					busObPublicId = businessObject["busObPublicId"].ToString(),
					busObRecId = businessObject["busObRecId"].ToString()
				};
				myActions.Add(new CardAction("invoke", "Dislike", null, JsonConvert.SerializeObject(myCommand)));

				// More button
				myCommand = new ListTicketCommand
				{
					Action = 202,
					busObId = businessObject["busObId"].ToString(),
					busObPublicId = businessObject["busObPublicId"].ToString(),
					busObRecId = businessObject["busObRecId"].ToString()
				};
				myActions.Add(new CardAction(ActionTypes.OpenUrl, "More details", null, string.Format(myTexts.supportPortalUrl, myCommand.busObId, myCommand.busObPublicId)));

				string imgUrl = Images.KnowledgeUrl;

				// Obtain and format the text to return, truncating to 250 characters to avoid sending too much data to the user in the form of a card.
				string cardText = businessObject["fields"][4]["value"].ToString().Replace("\n", " ").Replace("  ", " ").Replace("  ", " ");
				cardText = cardText.Substring(0, Math.Min(cardText.Length, 250));

				//Create Card
				ThumbnailCard heroCard = new ThumbnailCard
				{
					Title = businessObject["fields"][2]["value"].ToString(),
					Subtitle = businessObject["fields"][3]["value"].ToString(),
					Text = cardText,
					Images = new List<CardImage>() { new CardImage(url: imgUrl) },
					Buttons = myActions
				};

				kbaseList.Add(heroCard.ToAttachment());
			}

			return kbaseList;
		}

		public static async Task<IList<Attachment>> GetAlertsAsync(string query, Instance instance)
		{
			List<Attachment> alertsList = new List<Attachment>();

			string servicePath = instance.ApiUrl + "api/V1/getsearchresults/association/9344be92d5b7b4c290437c4110bc5b7147c9e3e98a"
								 + "/scope/Global"
								 + "/scopeowner/None"
								 + "/searchname/Problems%20Set%20as%20Top%20Issues"
								 + "?pagenumber=1&pagesize=10&searchTerm=" + query;

			HttpResponseMessage response = await instance.WebApiClient.SubmitRequestAsync(c => c.GetAsync(servicePath));

			JObject responseData = await response.Content.ReadAsAsync<JObject>();

			foreach (JToken businessObject in responseData["businessObjects"])
			{
				List<CardAction> myActions = new List<CardAction>();
				//Ticket Command
				ListTicketCommand myCommand = new ListTicketCommand
				{
					Action = 100,
					busObId = businessObject["busObId"].ToString(),
					busObPublicId = businessObject["busObPublicId"].ToString(),
					busObRecId = businessObject["busObRecId"].ToString()
				};
				string imgUrl = Images.ProblemUrl;
				//First Action Add Comments               
				myActions.Add(new CardAction("invoke", "Subscribe", null, JsonConvert.SerializeObject(myCommand)));

				//Create Card
				ThumbnailCard heroCard = new ThumbnailCard
				{
					Title = String.Format("{0} #{1}", businessObject["fields"][5]["value"].ToString(), businessObject["fields"][0]["value"]),
					Subtitle = businessObject["fields"][3]["value"].ToString(),
					Text = businessObject["fields"][2]["value"].ToString(),
					Images = new List<CardImage>() { new CardImage(url: imgUrl) },
					Buttons = myActions
				};
				alertsList.Add(heroCard.ToAttachment());
			}

			return alertsList;
		}

		public static async Task<bool> SubscribeToIssue(Instance instance, string issueid, string user)
		{
			string servicePath = instance.ApiUrl + "api/V1/savebusinessobject";

			string payLoadTemplate = JsonTemplates.subscribeToIssue;

			//update Payload
			JObject payload = JObject.Parse(payLoadTemplate);
			payload["fields"][0]["value"] = issueid;
			payload["fields"][1]["value"] = user;

			string serializedPayload = JsonConvert.SerializeObject(payload);

			string response = await instance.WebApiClient.PostJsonAsync(servicePath, serializedPayload, true, true);

			return true;
		}

		/// <summary>
		/// Log that a user was able to find some information to help themselves thus saving a support
		/// person from becoming invovled. 
		/// </summary>
		/// <param name="instance"></param>
		/// <param name="searchedProblem"></param>
		/// <param name="user"></param>
		/// <returns></returns>
		public static async Task<bool> LogCallDeflection(Instance instance, string searchedProblem, string user)
		{
			string servicePath = instance.ApiUrl + "api/V1/savebusinessobject";

			string payLoadTemplate = JsonTemplates.logCallDeflection;

			//update Payload
			JObject payload = JObject.Parse(payLoadTemplate);
			payload["fields"][0]["value"] = searchedProblem;
			payload["fields"][1]["value"] = user;

			string serializedPayload = JsonConvert.SerializeObject(payload);

			string response = await instance.WebApiClient.PostJsonAsync(servicePath, serializedPayload, true, true);

			return true;
		}


		public static async Task<bool> LikeKBase(Instance instance, string articleid, string user, bool like)
		{
			string servicePath = instance.ApiUrl + "api/V1/savebusinessobject";

			string payLoadTemplate = JsonTemplates.likeKbase;

			//update Payload
			JObject payload = JObject.Parse(payLoadTemplate);
			payload["fields"][0]["value"] = articleid;
			payload["fields"][1]["value"] = user;
			if (!like)
				payload["fields"][2]["value"] = "Dislike Article";

			string serializedPayload = JsonConvert.SerializeObject(payload);

			string response = await instance.WebApiClient.PostJsonAsync(servicePath, serializedPayload, true, true);

			return true;
		}

		public static async Task<IList<Attachment>> GetCommunityAsync(string query, Instance instance)
		{
			List<Attachment> communityList = new List<Attachment>();

			return communityList;
		}

		public static async Task<string> GetQnAAnswerAsync(string query)
		{
			string responseString = string.Empty;

			string knowledgebaseId = ConfigurationManager.AppSettings["QNAMaker.KnowledgebaseId"];
			string qnamakerSubscriptionKey = ConfigurationManager.AppSettings["QNAMaker.SubscriptionKey"];


			//Build the URI
			Uri qnamakerUriBase = new Uri("https://westus.api.cognitive.microsoft.com/qnamaker/v1.0");
			UriBuilder builder = new UriBuilder($"{qnamakerUriBase}/knowledgebases/{knowledgebaseId}/generateAnswer");

			//Add the question as part of the body
			string postBody = $"{{\"question\": \"{query}\"}}";

			//Send the POST request
			using (WebClient client = new WebClient())
			{
				//Set the encoding to UTF8
				client.Encoding = System.Text.Encoding.UTF8;

				//Add the subscription key header
				client.Headers.Add("Ocp-Apim-Subscription-Key", qnamakerSubscriptionKey);
				client.Headers.Add("Content-Type", "application/json");
				responseString = await client.UploadStringTaskAsync(builder.Uri, postBody);
			}

			try
			{
				QNAAnswer responseJson = JsonConvert.DeserializeObject<QNAAnswer>(responseString);
				if (responseJson.answer != "No good match found in the KB") //responseJson.score > Int32.Parse(ConfigurationManager.AppSettings["QNAMaker.ScoreThreshold"]))
				{
					responseString = responseJson.answer;
				}
				else { responseString = String.Empty; }

			}
			catch (Exception exc)
			{
				//TODO: Take execption to AppInsights or other
				Trace.TraceError(exc.ToString());
				responseString = "GetStaticHelpHealthAsync Exception: " + exc.Message;
			}

			return responseString;
		}
		
	}
}	