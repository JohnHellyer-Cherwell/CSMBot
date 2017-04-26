using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SupportBot.Cherwell
{
	/// <summary>
	/// Represents a client that can be used to authenticate and make calls to the Cherwell Web API.
	/// </summary>
	public class WebApiClient
	{
		// ------------------------------------------------------------------------------------
		#region Constants

		/// <summary>
		/// The Web API relative address for ticket authentication.
		/// </summary>
		private const string RestApiAuthAddress = "token?auth_mode={0}";

		/// <summary>
		/// The Web API auth mode for "auto" selection of authentication mode.
		/// </summary>
		private const string RestApiAuthModeAuto = "Auto";

		/// <summary>
		/// The Web API auth mode for internal authentication.
		/// </summary>
		private const string RestApiAuthModeInternal = "Internal";

		#endregion

		// ------------------------------------------------------------------------------------
		#region Fields

		/// <summary>
		/// The base URL for the Web API.
		/// </summary>
		private string m_baseUrl;

		/// <summary>
		/// The client Id to use when connecting to the Web API.
		/// </summary>
		private string m_clientId;

		/// <summary>
		/// The base URL for the Web API that was used to create the current <see cref="HttpClient"/>.
		/// </summary>
		private string m_currentBaseUrl;

		/// <summary>
		/// The <see cref="HttpClient"/> that should be used for API operations.
		/// </summary>
		private HttpClient m_currentHttpClient;

		/// <summary>
		/// The password that should be used for API operations.
		/// </summary>
		private string m_password;

		/// <summary>
		/// The user Id that should be used for API operation.
		/// </summary>
		private string m_userId;

		/// <summary>
		/// The object that should be used to synchronize creation of a new HttpClient.
		/// </summary>
		private object m_clientLock = new object();

		/// <summary>
		/// The object that should be used to synchronize authentication
		/// </summary>
		private object m_authLock = new object();

		#endregion

		// ------------------------------------------------------------------------------------
		#region Construction/Finalization

		/// <summary>
		/// Initializes a new instance of the <see cref="WebApiClient"/> class.
		/// </summary>
		public WebApiClient()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WebApiClient"/> class using the supplied connection information.
		/// </summary>
		/// <param name="baseUrl">The base URL to the Cherwell Web API.</param>
		/// <param name="clientId">The client Id to use when connecting to the Web API.</param>
		/// <param name="userId">The user Id that should be used to authenticate to the Cherwell Web API.</param>
		/// <param name="password">The password that should be used to authenticate to the Cherwell Web API.</param>
		public WebApiClient(string baseUrl, string clientId, string userId, string password)
		{
			BaseUrl = baseUrl;
			ClientId = clientId;
			UserId = userId;
			Password = password;
		}

		#endregion

		// ------------------------------------------------------------------------------------
		#region Public methods

		/// <summary>
		/// Gets or sets the base URL to the Cherwell Web API.
		/// </summary>
		public string BaseUrl
		{
			get { return m_baseUrl; }
			set
			{
				// Ensure the base URL has a trailing slash, if provided.
				m_baseUrl = value;
				if (m_baseUrl != null && !m_baseUrl.EndsWith("/"))
				{
					m_baseUrl += "/";
				}
			}
		}

		/// <summary>
		/// Gets or sets the client Id that should be used when connecting to the Cherwell Web API.
		/// </summary>
		public string ClientId
		{
			get { return m_clientId; }
			set { m_clientId = value; }
		}

		/// <summary>
		/// Gets or sets the password that should be used to authenticate to the Cherwell Web API.
		/// </summary>
		public string Password
		{
			get { return m_password; }
			set { m_password = value; }
		}

		/// <summary>
		/// Gets or sets the user Id that should be used to authenticate to the Cherwell Web API.
		/// </summary>
		public string UserId
		{
			get { return m_userId; }
			set { m_userId = value; }
		}

		#endregion

		// ------------------------------------------------------------------------------------
		#region Public methods

		/// <summary>
		/// Posts a JSON payload to the Cherwell Web API and returns a JSON response.
		/// </summary>
		/// <param name="requestUrl">The URL to which the request should be sent.</param>
		/// <param name="payload">The JSON payload as a string.</param>
		/// <returns>The JSON response returned from the Web API</returns>
		public string PostJson(string requestUrl, string payload, bool handleUnauthorized = true, bool ensureSuccess = false)
		{
			HttpResponseMessage response = SubmitRequest(c =>
			{
				HttpContent content = new StringContent(payload, Encoding.UTF8, "application/json");
				return c.PostAsync(requestUrl, content).Result;
			}, handleUnauthorized, ensureSuccess);

			return response.Content.ReadAsStringAsync().Result;
		}

		/// <summary>
		/// Asynchronously posts a JSON payload to the Cherwell Web API and returns a JSON response.
		/// </summary>
		/// <param name="requestUrl">The URL to which the request should be sent.</param>
		/// <param name="payload">The JSON payload as a string.</param>
		/// <returns>The JSON response returned from the Web API</returns>
		public async Task<string> PostJsonAsync(string requestUrl, string payload, bool handleUnauthorized = true, bool ensureSuccess = false)
		{
			HttpResponseMessage response = await SubmitRequestAsync(async c =>
			{
				HttpContent content = new StringContent(payload, Encoding.UTF8, "application/json");
				return await c.PostAsync(requestUrl, content);
			}, handleUnauthorized, ensureSuccess);

			return await response.Content.ReadAsStringAsync();
		}

		/// <summary>
		/// Submits a request to the Cherwell Web API and handles well-known responses.
		/// </summary>
		/// <param name="request">The Web API request to submit.</param>
		/// <param name="handleUnauthorized">Indicates whether to handle unauthorized responses by attempting to login.</param>
		/// <param name="ensureSuccess">Indicates whether to ensure the request was successful. If not, an exception will be thrown.</param>
		/// <returns><b>true</b> if the login was successful; <b>false</b> otherwise.</returns>
		public HttpResponseMessage SubmitRequest(Func<HttpClient, HttpResponseMessage> request, 
			bool handleUnauthorized = true, bool ensureSuccess = false)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));

			HttpClient client = GetHttpClient(true);

			HttpResponseMessage response = request(client);

			if (response == null)
			{
				throw new InvalidOperationException("NoRestApiResponseReceived");
			}

			// Handle an unauthorized response by attempting to login again, if requested.
			if (handleUnauthorized && response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
			{
				if (HandleUnauthorizedResponse(client))
				{
					// If the login was successful, attempt the request again.
					return SubmitRequest(request, handleUnauthorized, ensureSuccess);
				}
			}

			// Ensure that the response indicates a successful result.
			if (ensureSuccess)
				response.EnsureSuccessStatusCode();

			return response;
		}

		/// <summary>
		/// Asynchronously Submits a request to the Cherwell Web API and handles well-known responses.
		/// </summary>
		/// <param name="request">The Web API request to submit.</param>
		/// <param name="handleUnauthorized">Indicates whether to handle unauthorized responses by attempting to login.</param>
		/// <param name="ensureSuccess">Indicates whether to ensure the request was successful. If not, an exception will be thrown.</param>
		/// <returns><b>true</b> if the login was successful; <b>false</b> otherwise.</returns>
		public async Task<HttpResponseMessage> SubmitRequestAsync(Func<HttpClient, Task<HttpResponseMessage>> request,
			bool handleUnauthorized = true, bool ensureSuccess = false)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));

			HttpClient client = GetHttpClient(true);

			HttpResponseMessage response = await request(client);

			if (response == null)
			{
				throw new InvalidOperationException("NoRestApiResponseReceived");
			}

			// Handle an unauthorized response by attempting to login again, if requested.
			if (handleUnauthorized && response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
			{
				if (HandleUnauthorizedResponse(client))
				{
					// If the login was successful, attempt the request again.
					return await SubmitRequestAsync(request, handleUnauthorized, ensureSuccess);
				}
			}

			// Ensure that the response indicates a successful result.
			if (ensureSuccess)
				response.EnsureSuccessStatusCode();

			return response;
		}

		#endregion

		// ------------------------------------------------------------------------------------
		#region Private Methods

		/// <summary>
		/// Attempts to login to the Cherwell Web API.
		/// </summary>
		/// <param name="previewState">The state information for the preview operation.</param>
		/// <returns><b>true</b> if the login was successful; <b>false</b> otherwise.</returns>
		private bool AttemptWebApiAuthentication()
		{
			// Attempt to use anonymous authentication.
			//if (AttemptWebApiAnonymousAuthentication())
			//{
			//	return true;
			//}

			// Attempt to use username/password authentication.
			if (AttemptWebApiCredentialsAuthentication())
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Attempts to login to the Cherwell Web API using anonymous authentication.
		/// </summary>
		/// <returns><b>true</b> if the login was successful; <b>false</b> otherwise.</returns>
		private bool AttemptWebApiAnonymousAuthentication()
		{
			try
			{
				// Prepare the Form content for the authentication request. Anonymous authentication is
				// requested by providing empty username and password values.
				HttpContent content = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>(WebApiConstants.PostGrantType, WebApiConstants.GrantTypePassword),
					new KeyValuePair<string, string>(WebApiConstants.PostClientId, m_clientId),
					//new KeyValuePair<string, string>(WebApiConstants.PostClientSecret, GetHash(PortalClientSecret)),
					new KeyValuePair<string, string>(WebApiConstants.PostUsername, string.Empty),
					new KeyValuePair<string, string>(WebApiConstants.PostPassword, string.Empty)
				});

				// Submit the authentication request to the Web API.
				string authUrl = string.Format(RestApiAuthAddress, RestApiAuthModeInternal);
				HttpResponseMessage response = SubmitRequest(c => c.PostAsync(authUrl, content).Result, false, false);

				return HandleTokenResponse(RestApiAuthModeInternal, response);
			}
			catch (Exception)
			{
				throw;
			}
		}

		/// <summary>
		/// Attempts to login to the Cherwell Web API using username and password authentication.
		/// </summary>
		/// <param name="previewState">The state information for the preview operation.</param>
		/// <returns><b>true</b> if the login was successful; <b>false</b> otherwise.</returns>
		private bool AttemptWebApiCredentialsAuthentication()
		{
			try
			{
				// Prepare the Form content for the authentication request.
				HttpContent content = new FormUrlEncodedContent(new[]
				{
					new KeyValuePair<string, string>(WebApiConstants.PostGrantType, WebApiConstants.GrantTypePassword),
					new KeyValuePair<string, string>(WebApiConstants.PostClientId, m_clientId),
					new KeyValuePair<string, string>(WebApiConstants.PostUsername, m_userId),
					new KeyValuePair<string, string>(WebApiConstants.PostPassword, m_password)
				});

				// Submit the authentication request to the Web API.
				string authUrl = string.Format(RestApiAuthAddress, RestApiAuthModeInternal);
				HttpResponseMessage response = SubmitRequest(c => c.PostAsync(authUrl, content).Result, false, false);

				return HandleTokenResponse(RestApiAuthModeInternal, response);
			}
			catch (Exception)
			{
				throw;
			}
		}

		/// <summary>
		/// Gets the hashed value for the specified input string.
		/// </summary>
		/// <param name="input">The string value to hash.</param>
		/// <returns>The hashed value as a base64 encoded string.</returns>
		private static string GetHash(string input)
		{
			SHA256CryptoServiceProvider algorithm = new SHA256CryptoServiceProvider();
			byte[] byteValue = Encoding.UTF8.GetBytes(input);
			byte[] byteHash = algorithm.ComputeHash(byteValue);

			return Convert.ToBase64String(byteHash);
		}

		/// <summary>
		/// Initializes an <see cref="HttpClient"/> for preview operations.
		/// </summary>
		/// <param name="reuseClient">Indicates whether to reuse an existing <see cref="HttpClient"/> if it is available.</param>
		/// <returns>An initialized <see cref="HttpClient"/> instance.</returns>
		private HttpClient GetHttpClient(bool reuseClient = true)
		{
			// Determine whether an existing HttpClient can be reused. For reuse, the HttpClient must have the same base address.
			if (m_currentHttpClient == null || !reuseClient
				|| !string.Equals(m_baseUrl, m_currentBaseUrl, StringComparison.OrdinalIgnoreCase))
			{
				lock (m_clientLock)
				{
					// After acquiring a lock, determine again whether the HttpClient has already been created for the specified base address.
					if (m_currentHttpClient == null	|| !reuseClient
						|| !string.Equals(m_baseUrl, m_currentBaseUrl, StringComparison.OrdinalIgnoreCase))
					{
						// Dispose of an existing HttpClient instance, if present.
						m_currentHttpClient?.Dispose();

						m_currentHttpClient = new HttpClient();
						m_currentHttpClient.BaseAddress = new Uri(m_baseUrl);
						m_currentHttpClient.DefaultRequestHeaders.Accept.Clear();
						m_currentHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(WebApiConstants.MediaTypeJson));

						// Preserve the base address to allow simpler comparison for subsequent client requests.
						m_currentBaseUrl = m_baseUrl;
					}
				}
			}

			return m_currentHttpClient;
		}

		/// <summary>
		/// Validates the <see cref="HttpResponseMessage"/> for a Web API authentication request.
		/// </summary>
		/// <param name="authMode">The authentication mode that was used for the authentication request.</param>
		/// <param name="response">The <see cref="HttpResponseMessage"/> to validate.</param>
		/// <returns><b>true</b> if the token response is valid; <b>false</b> otherwise.</returns>
		private bool HandleTokenResponse(string authMode, HttpResponseMessage response)
		{
			string errorMessage = null;

			try
			{
				if (response != null)
				{
					JObject jresponse = response.Content.ReadAsAsync<JObject>().Result;
					if (jresponse != null)
					{
						if (jresponse[WebApiConstants.JsonKeyError] == null)
						{
							string accessToken = jresponse[WebApiConstants.JsonKeyAccessToken].Value<string>();
							if (!string.IsNullOrWhiteSpace(accessToken))
							{
								SetBearerToken(m_currentHttpClient, accessToken);

								return true;
							}
							else
							{
								errorMessage = "No access token was received for the authentication request.";
							}
						}
						else
						{
							errorMessage = jresponse[WebApiConstants.JsonKeyError].ToString();
						}
					}
					else
					{
						errorMessage = "No JSON response was received for the authentication request.";
					}
				}
				else
				{
					errorMessage = "No HTTP response was received for the authentication request.";
				}
			}
			catch (Exception ex)
			{
				errorMessage = $"Error attempting to handle a token response from the Web API. {ex.Message}";
			}

			if (errorMessage != null)
			{
				throw new InvalidOperationException($"Cherwell Web API {authMode} authentication was not successful: {errorMessage}.");
			}

			return false;
		}

		/// <summary>
		/// Handles an unauthorized response from the Web API.
		/// </summary>
		/// <param name="client">The <see cref="HttpClient"/> for which to handle a response.</param>
		/// <returns><b>true</b> if the client was authorized; <b>false</b> otherwise.</returns>
		private bool HandleUnauthorizedResponse(HttpClient client)
		{
			// If the HttpClient already has an authorization header, it cannot be changed. In this case,
			// a new HttpClient needs to be created to faciliate re-login.
			if (client.DefaultRequestHeaders.Contains(WebApiConstants.HeaderAuthorization))
			{
				client = GetHttpClient(false);
			}

			// Ensure that two threads do not attempt to authenticate simultaneously.
			lock (m_authLock)
			{
				if (!client.DefaultRequestHeaders.Contains(WebApiConstants.HeaderAuthorization))
				{
					// Attempt to login to the Web API again.
					AttemptWebApiAuthentication();
				}
			}

			return client.DefaultRequestHeaders.Contains(WebApiConstants.HeaderAuthorization);
		}

		/// <summary>
		/// Sets the specified access token as the bearer token for the specified <see cref="HttpClient"/>.
		/// </summary>
		/// <param name="client">The <see cref="HttpClient"/> for which to set a bearer token.</param>
		/// <param name="accessToken">The access token string to set as a bearer token.</param>
		private static void SetBearerToken(HttpClient client, string accessToken)
		{
			string bearerToken = string.Format(WebApiConstants.HeaderBearerTokenFormat, accessToken);
			client.DefaultRequestHeaders.Add(WebApiConstants.HeaderAuthorization, bearerToken);
		}

		#endregion
	}
}
