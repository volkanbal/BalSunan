namespace BalSunan.Models {
	public class UserLoginRequest {
		public string deviceID { get; set; }
		public string? userEmail { get; set; }
		public string password { get; set; }
		public UserLoginRequest() { }
	}
}
