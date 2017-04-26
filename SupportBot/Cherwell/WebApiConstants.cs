using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SupportBot.Cherwell
{
	/// <summary>
	/// Provides constants for interacting with the Cherwell Web API.
	/// </summary>

	public class WebApiConstants
	{
		/// <summary>
		/// The client credentials grant type name for the REST API.
		/// </summary>
		public const string GrantTypeClientCredentials = "client_credentials";

		/// <summary>
		/// The password grant type name for the REST API.
		/// </summary>
		public const string GrantTypePassword = "password";

		/// <summary>
		/// The name of the authorization header that should contain the access token.
		/// </summary>
		public const string HeaderAuthorization = "Authorization";

		/// <summary>
		/// The header bearer token format string.
		/// </summary>
		public const string HeaderBearerTokenFormat = "Bearer {0}";

		/// <summary>
		/// The JSON object key that contains the access token for a successful login attempt. 
		/// </summary>
		public const string JsonKeyAccessToken = "access_token";

		/// <summary>
		/// The JSON object key that contains error information for a request.
		/// </summary>
		public const string JsonKeyError = "error";

		/// <summary>
		/// The media type for JSON media.
		/// </summary>
		public const string MediaTypeJson = "application/json";

		/// <summary>
		/// The form variable name for the REST API client Id.
		/// </summary>
		public const string PostClientId = "client_id";

		/// <summary>
		/// The form variable name for the REST API client secret.
		/// </summary>
		public const string PostClientSecret = "client_secret";

		/// <summary>
		/// The form variable name for the REST API grant type.
		/// </summary>
		public const string PostGrantType = "grant_type";

		/// <summary>
		/// The form variable name for the REST API password.
		/// </summary>
		public const string PostPassword = "password";

		/// <summary>
		/// The parameter name for a Trebuchet authentication ticket provided to the REST API for ticket-based authentication.
		/// </summary>
		public const string PostTrebuchetTicket = "trebuchet_ticket";

		/// <summary>
		/// The form variable name for the REST API username.
		/// </summary>
		public const string PostUsername = "username";
	}
}