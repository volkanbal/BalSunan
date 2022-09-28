using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BalSunan.Models {
	internal class TransactionMessage {
		public int roomID { get; set; }
		public int messageId { get; set; }
		public string message { get; set; }
	}
}
