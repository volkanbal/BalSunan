using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BalSunan.Models {
	internal class Transaction {
		public TransactionTypes tType { get; set; }
		public RType rType { get; set; }
		public object veri { get; set; }
		public string userToken { get; set; } = "";
		[JsonIgnore]
		public User user { get; set; }
		public DateTime tTime { get; set; } = DateTime.Now;
	}

}
