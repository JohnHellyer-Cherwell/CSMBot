using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SupportBot.Cherwell.Entities
{
	public class ListTicketCommand
	{
		public int Action { get; set; }
		public string busObId { get; set; }
		public string busObPublicId { get; set; }
		public string busObRecId { get; set; }
	}
}