using Newtonsoft.Json;
using System.Collections;
using System.Net.Sockets;

namespace BalSunan.Models {
	internal class mySocCon {
		// bağlandı cihazdan gelen özel ID
		public string uniqueID { get; set; }
		public Socket mySocket { get; set; }
		public List<room> myRooms { get; set; } = new List<room>();
		public StreamWriter myStream { get; set; }
		public void sendToClient(object neyi) {
			myStream.WriteLine(JsonConvert.SerializeObject(neyi));
			myStream.Flush();

		}
		//public string degisken_tut { get; set; }
		//public ArrayList degisken_tut_ArrayList { get; set; }
		//public string kisisel_bilgilerim { get; set; }
	}
}
