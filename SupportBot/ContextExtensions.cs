using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SupportBot
{
	public static class ContextExtensions
	{
		public static void StoreUser(this IBotContext context, string value)
		{
			context.UserData.SetValue(ContextConstants.UserKey, value);
		}

		public static void StoreUserFriendlyName(this IBotContext context, string value)
		{
			context.UserData.SetValue(ContextConstants.UserFirstName, value);
		}

		public static string GetUser(this IBotContext context)
		{
			string user = null;
			context.UserData.TryGetValue(ContextConstants.UserKey, out user);
			return user == null ? "invalid user" : user;
		}

		public static string GetUserFriendlyName(this IBotContext context)
		{
			string user = null;
			context.UserData.TryGetValue(ContextConstants.UserFirstName, out user);
			return user == null ? "" : user;
		}

		public static void StoreTenant(this IBotContext context, string value)
		{
			context.UserData.SetValue(ContextConstants.TenantIdKey, value);
		}
		public static string GetTenant(this IBotContext context)
		{
			string tenant = null;
			context.UserData.TryGetValue(ContextConstants.TenantIdKey, out tenant);
			return tenant; //returns null if not set
		}
		public static void StoreUserWelcomed(this IBotContext context, bool value)
		{
			context.PrivateConversationData.SetValue(ContextConstants.UserWelcomedKey, value);
		}
		public static bool GetUserWelcomed(this IBotContext context)
		{
			bool userwelcomed;
			context.PrivateConversationData.TryGetValue(ContextConstants.UserWelcomedKey, out userwelcomed);
			return userwelcomed; //returns null if not set
		}
		public static void StoreUserIssue(this IBotContext context, string value)
		{
			context.PrivateConversationData.SetValue(ContextConstants.UserIssueKey, value);
		}
		public static string GetUserIssue(this IBotContext context)
		{
			string userissue = null;
			context.PrivateConversationData.TryGetValue(ContextConstants.UserIssueKey, out userissue);
			return userissue == null ? "no issue sent" : userissue; //returns null if not set
		}

		/// <summary>
		/// Sets the last activity date and time for the current conversation.
		/// </summary>
		/// <param name="context">The <see cref="IBotContext"/> for which to set last activity.</param>
		/// <param name="lastActivity">Optional. The <see cref="DateTime"/> of the last activity or <b>null</b> to use the current date and time.</param>
		public static void SetLastActivity(this IBotContext context, DateTime? lastActivity = null)
		{
			context.ConversationData.SetValue(ContextConstants.LastActivity, lastActivity ?? DateTime.UtcNow);
		}

		/// <summary>
		/// Indicates whether the conversation is at a state that allows notifications.
		/// </summary>
		/// <param name="context">The <see cref="IBotContext"/> for which to allow or disallow notifications.</param>
		/// <param name="allow">Whether to allow notifications.</param>
		public static void AllowNotifications(this IBotContext context, bool allow = true)
		{
			context.ConversationData.SetValue(ContextConstants.AllowNotifications, allow);
		}
	}
}