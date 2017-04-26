using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

namespace SupportBot.Cherwell
{
	public static class InstanceMapping
	{
		private static Dictionary<string, string> orgToKey = new Dictionary<string, string>();
		private static RestClient client = new RestClient();
		private static WebApiClient m_apiClient;

		static InstanceMapping()
		{
			// Initialize a Cherwell Web API client that can be used for API operations.
			m_apiClient = new WebApiClient(ConfigurationManager.AppSettings["WebAPI.Endpoint"],
				ConfigurationManager.AppSettings["WebAPI.ClientID"],
				ConfigurationManager.AppSettings["Cherwell.Username"],
				ConfigurationManager.AppSettings["Cherwell.Password"]);
		}

		public static Instance GetCherwellInstance(string tenantid)
		{
			Instance instance = new Instance()
			{
				TenantId = tenantid,
				ClientId = ConfigurationManager.AppSettings["WebAPI.ClientID"],
				ApiUrl = ConfigurationManager.AppSettings["WebAPI.Endpoint"],
				User = ConfigurationManager.AppSettings["Cherwell.Username"],
				Pass = ConfigurationManager.AppSettings["Cherwell.Password"],
				IncidentObjectName = "incident",
				CommunityUrl = "https://www.cherwell.com/community",
				WebApiClient = m_apiClient
			};

			return instance;
		}

		/// <summary>
		/// Execute HTTP post wih Token and json payload
		/// </summary>
		/// <param name="payload"></param>
		/// <param name="apiFullUrl"></param>
		/// <returns></returns>
		public static string GeneralJsonPost(string payload, string apiFullUrl)
		{
			return m_apiClient.PostJson(apiFullUrl, payload, true, true);
		}

		/// <summary>
		/// Get Incident list for a specif usaer
		/// </summary>
		/// <param name="userName"></param>
		/// <returns></returns>
		public static string GetIncedentList(string userName)
		{
			//Payload			
			string payLoadTemplate = JsonTemplates.userIncidentList;
			
			//updaye Payload
			Newtonsoft.Json.Linq.JObject myPayload = Newtonsoft.Json.Linq.JObject.Parse(payLoadTemplate);
			myPayload["filters"][0]["value"] = userName;

			string result = GeneralJsonPost(
				Newtonsoft.Json.JsonConvert.SerializeObject(myPayload),
				$"{System.Configuration.ConfigurationManager.AppSettings["WebAPI.Endpoint"]}api/V1/getsearchresults");

			return result;
		}

		/// <summary>
		/// Get Incident list for a specif usaer
		/// </summary>
		/// <param name="userName"></param>
		/// <param name="incidentID">Public ID of the specific incident to retrieve</param>
		/// <returns></returns>
		public static string GetSpecificIncedent(string userName, string incidentID)
		{
			//Payload			
			string payLoadTemplate = JsonTemplates.retrieveSpecificIncident;

			//updaye Payload
			Newtonsoft.Json.Linq.JObject myPayload = Newtonsoft.Json.Linq.JObject.Parse(payLoadTemplate);
			myPayload["filters"][0]["value"] = userName;
			myPayload["filters"][1]["value"] = incidentID;

			string result = GeneralJsonPost(
				Newtonsoft.Json.JsonConvert.SerializeObject(myPayload),
				$"{System.Configuration.ConfigurationManager.AppSettings["WebAPI.Endpoint"]}api/V1/getsearchresults");

			return result;
		}

		/// <summary>
		/// Add comment to Ticket
		/// </summary>
		/// <param name="busObId"></param>
		/// <param name="busObPublicId"></param>
		/// <param name="busObRecId"></param>
		/// <param name="value">Comments</param>
		/// <returns></returns>
		public static string AddTicketComment(string busObId, string busObPublicId, string busObRecId, string value)
		{
			//Payload			
			string payLoadTemplate = JsonTemplates.addCommentToTicket;

			//updaye Payload
			Newtonsoft.Json.Linq.JObject myPayload = Newtonsoft.Json.Linq.JObject.Parse(payLoadTemplate);
			myPayload["busObId"] = busObId;
			myPayload["busObPublicId"] = busObPublicId;
			myPayload["busObRecId"] = busObRecId;
			myPayload["fields"][0]["value"] = value;

			return GeneralJsonPost(
				Newtonsoft.Json.JsonConvert.SerializeObject(myPayload),
				$"{System.Configuration.ConfigurationManager.AppSettings["WebAPI.Endpoint"]}api/V1/savebusinessobject");
		}
		/// <summary>
		/// Update Event Notifications
		/// </summary>
		/// <param name="busObPublicId"></param>
		/// <param name="busObRecId"></param>
		/// <returns></returns>
		public static string UpdateEventNotification(string busObPublicId, string busObRecId)
		{
			string payLoadTemplate = JsonTemplates.savebusinessobject;
			
			//updaye Payload
			Newtonsoft.Json.Linq.JObject myPayload = Newtonsoft.Json.Linq.JObject.Parse(payLoadTemplate);
			myPayload["busObPublicId"] = busObPublicId;
			myPayload["busObRecId"] = busObRecId;

			return GeneralJsonPost(
				Newtonsoft.Json.JsonConvert.SerializeObject(myPayload),
				$"{System.Configuration.ConfigurationManager.AppSettings["WebAPI.Endpoint"]}api/V1/savebusinessobject");
		}
		/// <summary>
		/// Withdraw Ticket 
		/// </summary>
		/// <param name="busObId"></param>
		/// <param name="busObPublicId"></param>
		/// <param name="busObRecId"></param>
		/// <returns></returns>
		public static string WithDrawTicket(string busObId, string busObPublicId, string busObRecId)
		{
			//Payload			
			string payLoadTemplate = JsonTemplates.withdrawTicket;

			//updaye Payload
			Newtonsoft.Json.Linq.JObject myPayload = Newtonsoft.Json.Linq.JObject.Parse(payLoadTemplate);
			myPayload["busObId"] = busObId;
			myPayload["busObPublicId"] = busObPublicId;
			myPayload["busObRecId"] = busObRecId;

			return GeneralJsonPost(
				Newtonsoft.Json.JsonConvert.SerializeObject(myPayload),
				$"{System.Configuration.ConfigurationManager.AppSettings["WebAPI.Endpoint"]}api/V1/savebusinessobject");
		}
		/// <summary>
		/// Read Event to Notify users
		/// </summary>
		/// <returns></returns>
		public static string ReadEvents()
		{
			//Payload			
			string payLoadTemplate = JsonTemplates.getsearchresults;

			//updaye Payload
			Newtonsoft.Json.Linq.JObject myPayload = Newtonsoft.Json.Linq.JObject.Parse(payLoadTemplate);

			return GeneralJsonPost(
				Newtonsoft.Json.JsonConvert.SerializeObject(myPayload),
				$"{System.Configuration.ConfigurationManager.AppSettings["WebAPI.Endpoint"]}api/V1/getsearchresults");
		}

		public async static Task<string> CreateIncident(Instance instance, string incidentDetails, string customerDisplayName)
		{
			//Payload			
			string payLoadTemplate = JsonTemplates.createIncident;

			//updaye Payload
			JObject myPayload = JObject.Parse(payLoadTemplate);
			myPayload["fields"][0]["value"] = incidentDetails;
			myPayload["fields"][1]["value"] = customerDisplayName;

			string apiFullUrl = System.Configuration.ConfigurationManager.AppSettings["WebAPI.Endpoint"] + "api/V1/savebusinessobject";
			string serializedPayload = JsonConvert.SerializeObject(myPayload);
			string response = await instance.WebApiClient.PostJsonAsync(apiFullUrl, serializedPayload, true, true);
			
			return response;

		}
	}
}