using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace SupportBot
{
	internal static class Images
	{
		private const string incidentUrl = "~/Cherwell/images/medicalsuitcase.png";
		private const string serviceRequestUrl = "~/Cherwell/images/request2.png";
		private const string checkMarkUrl = "~/Cherwell/images/Checkmark.jpg";
		private const string knowledgeUrl = "~/Cherwell/images/knowledge.png";
		private const string problemUrl = "~/Cherwell/images/problem.png";


		public static string IncidentImageUrl
		{
			get
			{
				return incidentUrl.Replace("~", GetServerRootUrl());
			}
		}

		public static string ServiceRequestUrl
		{
			get
			{
				return serviceRequestUrl.Replace("~", GetServerRootUrl());
			}
		}

		public static string ProblemUrl
		{
			get
			{
				return problemUrl.Replace("~", GetServerRootUrl());
			}
		}

		public static string KnowledgeUrl
		{
			get
			{
				return knowledgeUrl.Replace("~", GetServerRootUrl());
			}
		}

		public static string CheckmarkUrl
		{
			get
			{
				return checkMarkUrl.Replace("~", GetServerRootUrl());
			}
		}

		public static string GetServerRootUrl()
		{
			string fullUrl = HttpContext.Current.Request.Url.ToString();
			string path = HttpContext.Current.Request.Path;
			int index = fullUrl.IndexOf(path);
			if (index >= 0)
			{
				fullUrl = fullUrl.Substring(0, index);
			}

			return fullUrl;
		}

	}
}