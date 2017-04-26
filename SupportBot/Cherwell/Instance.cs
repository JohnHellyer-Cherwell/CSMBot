using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SupportBot.Cherwell
{
	public class Instance
	{
		public string TenantId { get; set; }
		public string ClientId { get; set; }
		public string ApiUrl { get; set; }
		public string User { get; set; }
		public string Pass { get; set; }
		public string CommunityUrl { get; set; }
		public string IncidentObjectName { get; set; }
		public WebApiClient WebApiClient { get; set; }
	}
}