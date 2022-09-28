using static System.Net.Mime.MediaTypeNames;
using System.Collections;
using System.Net.Sockets;
using System.Net;

namespace BalSunan {
	public class Program {


		//delegate void yazmak_icin(string yazilacak, RichTextBox kime);
		delegate void listeye_ekle_icin(string yazilacak);
		delegate void listeden_cikart_icin(string yazilacak);
		delegate void kullanicilari_yaz_icin();

		static void Main(string[] args) {
			var baslat = new baglantiIslemleri();
			baslat.dinle();
		}


	}
}
