using RestSharp.Serializers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using RestSharp.Deserializers;

namespace SupportBot.Cherwell.Search
{
	public interface IJsonSerializer : ISerializer, IDeserializer
	{

	}

	public class NewtonsoftJsonSerializer : IJsonSerializer
	{
		private Newtonsoft.Json.JsonSerializer serializer;

		public NewtonsoftJsonSerializer(Newtonsoft.Json.JsonSerializer serializer)
		{
			this.serializer = serializer;
		}

		public string ContentType
		{
			get { return "application/json"; } // Probably used for Serialization?
			set { }
		}

		public string DateFormat { get; set; }

		public string Namespace { get; set; }

		public string RootElement { get; set; }

		public string Serialize(object obj)
		{
			using (StringWriter stringWriter = new StringWriter())
			{
				using (JsonTextWriter jsonTextWriter = new JsonTextWriter(stringWriter))
				{
					serializer.Serialize(jsonTextWriter, obj);

					return stringWriter.ToString();
				}
			}
		}

		public T Deserialize<T>(RestSharp.IRestResponse response)
		{
			string content = response.Content;

			using (StringReader stringReader = new StringReader(content))
			{
				using (JsonTextReader jsonTextReader = new JsonTextReader(stringReader))
				{
					return serializer.Deserialize<T>(jsonTextReader);
				}
			}
		}

		public static NewtonsoftJsonSerializer Default
		{
			get
			{
				return new NewtonsoftJsonSerializer(new Newtonsoft.Json.JsonSerializer()
				{
					NullValueHandling = NullValueHandling.Ignore,
				});
			}
		}
	}

	public static class JsonExtension
	{
		/// <summary>
		/// Test whether a given string is valid JSON for the Serialized object of T
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="strInput"></param>
		/// <returns></returns>
		public static bool IsValidJson<T>(this string strInput, out T deserializedObject)
		{
			deserializedObject = default(T);

			strInput = strInput?.Trim();
			if (strInput != null &&
				((strInput.StartsWith("{") && strInput.EndsWith("}")) || //For object
				 (strInput.StartsWith("[") && strInput.EndsWith("]")))) //For array
			{
				try
				{
					deserializedObject = JsonConvert.DeserializeObject<T>(strInput);
					return true;
				}
				catch (JsonReaderException)
				{
					return false;
				}
			}
			else
			{
				return false;
			}
		}
	}
}
