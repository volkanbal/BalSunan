using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BalSunan.Models {
	internal class room {
		public int roomID { get; set; }
		public string roomName { get; set; }
		public string pass { get; set; }
		public RoomType tipi { get; set; }
		public int roomMasterUserID { get; set; }
		public List<mySocCon> myUserConnections { get; set; }
	}
}
