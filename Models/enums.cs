using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BalSunan.Models {
	internal enum RType {
		None = 0,
		NeedAuth = -1,
		NotRecognizedTerm = 0,
		OK = 1,
		Fail = 2,

	}
	internal enum RoomType {
		chat = 1,
		game = 2,
		other = 3,
		unknown = 0
	}
	internal enum TransactionTypes {
		login = 1,
		sendMessage = 2,
		gameAction = 6,


		listRoom = 10,
		joinRoom = 11,
		leaveRoom = 12,
		createRoom = 13,
		delleteRoom = 14,
	}
}
