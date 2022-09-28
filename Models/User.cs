using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace BalSunan.Models {
	public class User {
		public int userId { get; set; }
		//[Required(ErrorMessage = "eMail is required")]
		[RegularExpression(@"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$", ErrorMessage = "Please enter a valid email")]
		[StringLength(100)]
		public string userEmail { get; set; }
		public int userGroupID { get; set; }
		public bool isAdmin { get { return userGroupID == 2; } }
		public string userPassword { get; set; }
		public string userToken { get; set; }

		public User() { }
	}
}
