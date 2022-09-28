using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using BalSunan.Models;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using System.Security.Principal;
using Newtonsoft.Json.Linq;

namespace BalSunan {
	internal class baglantiIslemleri {
		//ArrayList[] herbisey_baglantim { get; set; } = new ArrayList[100];//= new List<mySocCon>();
		static List<room> herbisey_baglantim { get; set; } = new List<room>();
		static List<User> CCUs { get; set; } = new List<User>();

		#region start the service
		static Mutex bekleten = new Mutex();
		static List<IPAddress> ipAddresses { get; set; } = new List<IPAddress>();
		static int dinlenen_port;
		static string dinlenen_ip;
		static void getIPList() {
			var strHostName = Dns.GetHostName();
			if (strHostName != null) {
				var ipEntry = Dns.GetHostByName(strHostName);
				var addr = ipEntry.AddressList;
				foreach (IPAddress v in addr)
					if (v.AddressFamily == AddressFamily.InterNetwork)
						ipAddresses.Add(v);
			}
		}

		internal void dinle() {
			getIPList();
			if (ipAddresses.Count == 0) {
				yaz("IP adresi bulamadım");
				return;
			}
			getIPList();
			dinlenen_port = 2468;
			dinlenen_ip = ipAddresses.FirstOrDefault().ToString();
			new Thread(dinleyen_server).Start();
		}
		#endregion

		public void yaz(string yazilacak) {
			Console.WriteLine(yazilacak);
		}
		public string ProductVersion { get { return GetType().Assembly.GetName().Version.ToString(); } }
		private room findRoomByID(int roomID, Transaction veri) {
			// belki yetki burada gösterilebilinir.
			return herbisey_baglantim.FirstOrDefault(x => x.roomID == roomID);
		}
		private void veri_geldi(string gelen_veri, mySocCon eklenen) {
			/// <summary>
			/// işte herşey burada oluyor
			/// tüm iletişim buradan sağlanıyor
			/// </summary>

			if (string.IsNullOrEmpty(gelen_veri)) Thread.CurrentThread.Abort();
			var gelen_veri_ham = JsonConvert.DeserializeObject<Transaction>(gelen_veri);
			if (gelen_veri_ham == null) return;

			//kimdi bu arkadaş
			if (gelen_veri_ham.tType != TransactionTypes.login) {
				gelen_veri_ham.user = CCUs.FirstOrDefault(x => x.userToken == gelen_veri_ham.userToken);
				if (gelen_veri_ham.user == null) {
					eklenen.sendToClient(new Transaction() { tType = gelen_veri_ham.tType, rType = RType.NeedAuth, veri = "" });
				}
			}

			var metin_yazilacak = "";
			var donen_veri = new Transaction() {
				tType = TransactionTypes.login,
				rType = RType.OK
			};
			switch (gelen_veri_ham.tType) {
				case TransactionTypes.login:
					#region log-in işlemleri
					//log in işlemleri yapalım
					//arkadaşın kimliğini kontrol etmem lazım ondan sona giriş ve diğer işlemler için devam etmeliyim
					//
					//if (gelen_veri_ham.veri is JObject) {
					var userLoginRequest = (gelen_veri_ham.veri as JObject).ToObject<UserLoginRequest>();
					//}
					//	var userLoginRequest = (UserLoginRequest)gelen_veri_ham.veri;
					eklenen.uniqueID = userLoginRequest.deviceID;
					//var gelen_sifre = userLoginRequest.password;
					//gerçek bir authantication lazım 
					//jwt gibi
					if (userLoginRequest.password == "") {

						//		eklenen.kisisel_bilgilerim = "";
						//		eklenen.degisken_tut = "";
						var myToken = Guid.NewGuid().ToString();
						donen_veri.veri = new UserLoginResponse() {
							token = myToken,
							loginMessage = "Hoşgeldin " + eklenen.uniqueID + ".\nSunucu versiyon:" + ProductVersion + "\0"
						};
						eklenen.myStream.WriteLine(JsonConvert.SerializeObject(donen_veri));
						eklenen.myStream.Flush();
						metin_yazilacak = DateTime.Now.ToString("dd:MM:YY HH:mm:SS") + ": "
							+ eklenen.uniqueID + " login oldu. IP:" + IPAddress.Parse(((IPEndPoint)eklenen.mySocket.RemoteEndPoint).Address.ToString()) + ": "
							+ ((IPEndPoint)eklenen.mySocket.RemoteEndPoint).Port;
						// entity frameworkten gelen olacak
						CCUs.Add(new User() { userId = 123, userToken = myToken });
					} else {
						donen_veri.rType = RType.Fail;
						donen_veri.veri = new UserLoginResponse() {
							token = "",
							loginResult = "Fail",
							loginMessage = "Kullanıcı adı ya da şifre hatalı.\nSunucu versiyon:" + ProductVersion + "\0"
						};
						eklenen.myStream.WriteLine(JsonConvert.SerializeObject(donen_veri));
						eklenen.myStream.Flush();
						//eklenen.mySocket.Close();
					}
					break;
				#endregion
				#region mesaj olayı
				case TransactionTypes.sendMessage:
					// mesaj olayı buradan halledilecek
					// gruba mesaj atma işi
					var userMessage = (gelen_veri_ham.veri as JObject).ToObject<TransactionMessage>();

					//if (int.TryParse(gelen_veri_parcalari[1], out kontrol))
					var odam = findRoomByID(userMessage.roomID, gelen_veri_ham);
					if (odam == null) {
						eklenen.sendToClient(new Transaction() { tType = gelen_veri_ham.tType, rType = RType.Fail, veri = "" });
						/*
						 eklenen.myStream.WriteLine(JsonConvert.SerializeObject(new Transaction() {
							tType = gelen_veri_ham.tType,
							rType = RType.Fail,
							veri = ""
						}));
						 */
						break;
					}
					if (odam.myUserConnections.Contains(eklenen)) {
						foreach (mySocCon eklenenparca in odam.myUserConnections) {
							donen_veri.veri = userMessage;
							eklenenparca.myStream.WriteLine(JsonConvert.SerializeObject(donen_veri));
							eklenenparca.myStream.Flush();
						}
					} else {
						donen_veri.rType = RType.Fail;
						donen_veri.veri = "Buraya mesaj bırakamazsınız.";
						eklenen.myStream.WriteLine(JsonConvert.SerializeObject(donen_veri));
						eklenen.myStream.Flush();
					}
					metin_yazilacak = eklenen.uniqueID + ":" + userMessage.message;
					break;
				#endregion
				#region grup işlemleri

				case TransactionTypes.listRoom:
				case TransactionTypes.leaveRoom:
				case TransactionTypes.joinRoom:
				case TransactionTypes.createRoom:
					var roomData = (gelen_veri_ham.veri as JObject).ToObject<TransactionRoom>();
					donen_veri.veri = roomData;

					switch (gelen_veri_ham.tType) {
						#region gruba katılma isteği
						case TransactionTypes.listRoom:
							donen_veri.veri = herbisey_baglantim.Where(x => x.tipi == RoomType.chat).Select(x => new room() { tipi = x.tipi, roomID = x.roomID }).ToList();
							break;
						#endregion
						#region gruba katılma isteği
						case TransactionTypes.joinRoom:
							//
							// katılma şartlarını kontrol edelim: şifre evet mi 
							var joinedRoom = findRoomByID(roomData.roomID, gelen_veri_ham);
							if (joinedRoom == null) {
								eklenen.sendToClient(new Transaction() { tType = gelen_veri_ham.tType, rType = RType.Fail, veri = "" });
								goto bypass_room;
							}
							if (!string.IsNullOrEmpty(joinedRoom.pass) && joinedRoom.pass == roomData.pass) {
								eklenen.sendToClient(new Transaction() { tType = gelen_veri_ham.tType, rType = RType.Fail, veri = "" });
								goto bypass_room;
							}
							//
							// katılma şartlarını kontrol edelim:  ** sayı sınırı var mı
							//
							eklenen.myRooms.Add(joinedRoom);
							joinedRoom.myUserConnections.Add(eklenen);
							break;
						#endregion
						#region belirtiği gruptan çıkmak istiyor
						case TransactionTypes.leaveRoom:
							//
							// başkasını göndermek istiyor sa subjectTo
							//
							var leaveRoom = findRoomByID(roomData.roomID, gelen_veri_ham);
							if (leaveRoom == null) {
								eklenen.sendToClient(new Transaction() { tType = gelen_veri_ham.tType, rType = RType.Fail, veri = "" });
								goto bypass_room;
							}
							leaveRoom.myUserConnections.Remove(eklenen);
							eklenen.myRooms.Remove(leaveRoom);
							//
							break;
						#endregion
						#region grup oluşturma olayları
						case TransactionTypes.createRoom:
							if (roomData.roomID != -1) {
								eklenen.sendToClient(new Transaction() { tType = gelen_veri_ham.tType, rType = RType.Fail, veri = "" });
								goto bypass_room;
							}
							var newRoom = new room() {
								roomID = herbisey_baglantim.Max(z => z.roomID) + 1,
								pass = roomData.pass,
								tipi = RoomType.chat,
								roomName = roomData.roomName,
								roomMasterUserID = gelen_veri_ham.user.userId,
								myUserConnections = new List<mySocCon>() { eklenen }
							};
							eklenen.myRooms.Add(newRoom);
							herbisey_baglantim.Add(newRoom);
							/*
							var aranan_kisi = (gelen_veri_parcalari.Length > 3) ? gelen_veri_parcalari[3] : null;
							var aranan_kisiye_yollanacak = (gelen_veri_parcalari.Length > 5) ? gelen_veri_parcalari[5] : null;

								switch (komut) {
									case 1:
										#region bu izleyen aslında oyuna açık olanları merak ediyor
										foreach (mySocCon eklenenparca in herbisey_baglantim[kontrol + 10]) {
											grup_tam_listesi += eklenenparca.degisken_tut + "#";
										}
										eklenen.myStream.WriteLine("5#" + kontrol + "#" + komut + "#" + ((grup_tam_listesi.Length > 1) ? grup_tam_listesi.TrimEnd('#') : "") + "\0");
										eklenen.myStream.Flush();
										break;
									#endregion
									case 2:
										#region eklendiğimi tüm izleyen kullanıcılara bildirmek isterim
										var degisken_tut_icin = eklenen.degisken_tut = gelen_veri_parcalari[3] + "&" + gelen_veri_parcalari[4] + "&" + gelen_veri_parcalari[5];
										foreach (mySocCon eklenenparca in herbisey_baglantim[kontrol - 10]) {
											eklenenparca.myStream.WriteLine("5#" + kontrol + "#" + komut + "#" + degisken_tut_icin + "\0");
											eklenenparca.myStream.Flush();
										}
										break;
									#endregion
									case 3:
										#region karşılaşmamın iptalini tüm izleyen kullanıcılara bildirmek isterim
										foreach (mySocCon eklenenparca in herbisey_baglantim[kontrol]) {
											eklenenparca.myStream.WriteLine("5#" + kontrol + "#" + komut + "#" + gelen_veri_parcalari[3] + "\0");
											eklenenparca.myStream.Flush();
										}
										break;
									#endregion
									case 4:
										#region eklendiğimi tüm izleyen kullanıcılara bildirmek isterim
										// 5#1#4#kisi#1/2#veri >>izleyen grupta (başvuran)/(oluşturan) gruptaki kişiye veri gonderme oluşturulan oyuna giriş isteği red ve benzeri komutlar
										//herbisey_baglantim[3].IndexOf(eklenen.uniqueID 
										foreach (mySocCon eklenenparca in herbisey_baglantim[kontrol + ((gelen_veri_parcalari[4] == "1") ? 10 : -10)]) {
											if (eklenenparca.uniqueID == aranan_kisi) {
												eklenenparca.myStream.WriteLine("5#" + kontrol + "#" + komut + "#" + eklenen.uniqueID + "#" + ((gelen_veri_parcalari[4] == "1") ? "2" : "1") + "#" + aranan_kisiye_yollanacak + "\0");
												eklenenparca.myStream.Flush();
												break;
											}
										}
										break;
									#endregion
									case 5:
										#region oyunu_baslatma: karşılaşma onaylandı katılımcılar ve oyunu kuran izledikleri gruptan çıkarılacaklar
										//herbisey_baglantim[3].IndexOf(eklenen.uniqueID 
										//yeni bir grup oluşturduk şimdilik 45 dedim ama dinamik almalı
										//yeni gruplara ekliyoruz ... eski gruplardan atıyoruz
										// bos room kalmayınca ne yapacağız?
										var bos_oda = bos_oda_bul();
										veri_geldi("3#" + bos_oda, eklenen);
										veri_geldi("4#" + kontrol, eklenen);
										veri_geldi("4#" + (kontrol - 10), eklenen);
										foreach (mySocCon eklenenparca in herbisey_baglantim[kontrol + ((gelen_veri_parcalari[3] == "1") ? 10 : -10)]) {
											if (eklenenparca.uniqueID != aranan_kisi) continue;
											veri_geldi("3#" + bos_oda, eklenenparca);
											veri_geldi("4#" + (kontrol - 10), eklenenparca);
											eklenenparca.myStream.WriteLine("5#" + bos_oda + "#" + komut + "#" + aranan_kisiye_yollanacak + "\0");
											eklenenparca.myStream.Flush();
											eklenen.myStream.WriteLine("5#" + bos_oda + "#" + komut + "#" + aranan_kisiye_yollanacak + "\0");
											eklenen.myStream.Flush();
											break;
										}
										Thread.Sleep(1000);
										// oyunu başlat çalıştıktan sonra oyunu kuruyor ve sıra kimdeyse onu geri döndürüyor
										oyunu_baslat(bos_oda, 60);
										break;
										#endregion
								}
							*/
							break;
							#endregion
					}

					kullanicilari_yaz();
					eklenen.myStream.WriteLine(JsonConvert.SerializeObject(donen_veri));
					eklenen.myStream.Flush();
				bypass_room:
					break;


					#endregion

					//case "6":
					//	// 6#oyuncu_id#1(taş çekti)#bilmam_kac_nolu_Tas&.....&..... taşı benden gizle	
					//	// 6#oyuncu_id#2(çektiğim taş)#bilmam_kac_nolu_Tas/022(değeri)&....                   
					//	// 1/001&8/002&25/025&4/555&12/245&5/233&56/244
					//	//6#oyuncu_id#3(hamle yaptı)#bilmam_kac_nolu_Tas/022(değeri)&0/2(yeri)
					//	//oyuncu_id benim olup olmaması onemli olmayacak böylece oyun serverde oynanacak
					//	//6#oyuncu_id#4 sıra bu oyuncu_id de
					//	//arraylistin 0. elemanı elimdeki taşlar
					//	//arraylistin 1. elemanı kalan taşlar
					//	//arraylistin 2. elamanı yerdeki taşlar
					//	//arraylistin 3. elamanı puanlar
					//	//arraylistin 4. elemanı sıranın kimde olduğu
					//	//arraylistin 5. elemanı room no
					//	if (!int.TryParse(gelen_veri_parcalari[2], out komut)) break;
					//	switch (komut) {
					//		case 2:
					//			#region taş çekme
					//			var gelen_oyun_bilgisi = gelen_veri_parcalari[3].Split(new[] { '&' });
					//			//if (((string)eklenen.degisken_tut_ArrayList[0]).IndexOf(gelen_oyun_bilgisi[0]) != -1) {
					//			//herbisey_baglantim[].Count;
					//			//arraylistin 0. elemanı elimdeki taşlar
					//			//arraylistin 1. elemanı kalan taşlar
					//			//arraylistin 2. elamanı yerdeki taşlar
					//			//arraylistin 3. elamanı puanlar
					//			//arraylistin 4. elemanı sıranın kimde olduğu
					//			//arraylistin 5. elemanı room no
					//			var oda_no = (int)eklenen.degisken_tut_ArrayList[5];
					//			var istenen_tas = "";
					//			var kazanilan_puan = 0;
					//			eklenen.degisken_tut_ArrayList[8] = ((int)eklenen.degisken_tut_ArrayList[8]) + 1;
					//			if ((int)eklenen.degisken_tut_ArrayList[8] == 4) goto pas_geciyor;
					//			for (var k = 0; k < ((ArrayList)eklenen.degisken_tut_ArrayList[1]).Count; k++) {
					//				if (((string)((ArrayList)eklenen.degisken_tut_ArrayList[1])[k]).IndexOf(gelen_veri_parcalari[3] + "/") != 0) continue;
					//				istenen_tas = ((string)((ArrayList)eklenen.degisken_tut_ArrayList[1])[k]);
					//				((ArrayList)eklenen.degisken_tut_ArrayList[1]).RemoveAt(k);
					//				break;
					//			}
					//			if (istenen_tas == "") break;
					//			kazanilan_puan = -10;
					//			eklenen.degisken_tut_ArrayList[3] = (int)eklenen.degisken_tut_ArrayList[3] + kazanilan_puan;
					//			var elimdeki_taslar = ((string)eklenen.degisken_tut_ArrayList[0]) + "&" + istenen_tas;
					//			eklenen.degisken_tut_ArrayList[0] = elimdeki_taslar;
					//			eklenen.myStream.WriteLine("6#" + eklenen.uniqueID + "#2#" + istenen_tas + "\0");
					//			eklenen.myStream.Flush();
					//			foreach (mySocCon eklenenparca in herbisey_baglantim[oda_no]) {
					//				eklenenparca.myStream.WriteLine("6#" + eklenen.uniqueID + "#1#" + gelen_veri_parcalari[3] + "\0");
					//				eklenenparca.myStream.Flush();
					//			}
					//			break;
					//		#endregion
					//		case 3:
					//			#region oyuncu hamle yaptı
					//			//6#oyuncu_id#3(hamle yaptı)#bilmem_kac_nolu_Tas/022(değeri)&0/2(yeri)                                                               
					//			//oyuncu_id benim olup olmaması onemli olmayacak böylece oyun serverde oynanacak
					//			var zemin_sutun_sayisi = 19;
					//			gelen_oyun_bilgisi = gelen_veri_parcalari[3].Split(new[] { '&' });
					//			if (((string)eklenen.degisken_tut_ArrayList[0]).IndexOf(gelen_oyun_bilgisi[0]) != -1) {
					//				kazanilan_puan = 0;
					//				var sutun = int.Parse(gelen_oyun_bilgisi[1].Split(new[] { '/' })[0]);
					//				var satir = int.Parse(gelen_oyun_bilgisi[1].Split(new[] { '/' })[1]);
					//				var aci = int.Parse(gelen_oyun_bilgisi[1].Split(new[] { '/' })[2]);
					//				var kontrol_arrayi = ((zemin_sutun_sayisi * satir + sutun) % 2) == 1 ?
					//									new[,] { { satir, sutun }, { satir, sutun + 2 }, { satir + 1, sutun + 1 } }
					//									: new[,] { { satir, sutun + 1 }, { satir + 1, sutun + 2 }, { satir + 1, sutun } };

					//				//socket.send("6#"+uniqe_ID+"#3#"+this._parent._name.substr(5, 3)+"/"+this._parent.taslar_str+"&"+sutun+"/"+satir+"/"+aci);
					//				var tas_degeri = int.Parse(gelen_oyun_bilgisi[0].Split(new[] { '/' })[1]);
					//				int[] tas_degerleri;
					//				switch (aci) {
					//					case 1:
					//						tas_degerleri = new[] { tas_degeri % 10, tas_degeri / 100, tas_degeri / 10 - (tas_degeri / 100) * 10 };
					//						break;
					//					case 2:
					//						tas_degerleri = new[] { tas_degeri / 10 - (tas_degeri / 100) * 10, tas_degeri % 10, tas_degeri / 100 };
					//						break;
					//					default:
					//						tas_degerleri = new[] { tas_degeri / 100, tas_degeri / 10 - (tas_degeri / 100) * 10, tas_degeri % 10 };
					//						break;
					//				}
					//				var tutan_kose = 0;
					//				var bos_kose = 0;
					//				for (var i = 0; i < 3; i++) {
					//					if (((int[,])eklenen.degisken_tut_ArrayList[2])[kontrol_arrayi[i, 0], kontrol_arrayi[i, 1]] == tas_degerleri[i])
					//						tutan_kose++;
					//					if (((int[,])eklenen.degisken_tut_ArrayList[2])[kontrol_arrayi[i, 0], kontrol_arrayi[i, 1]] == -1)
					//						bos_kose++;
					//					kazanilan_puan += tas_degerleri[i];
					//				}
					//				//onaylandi
					//				kazanilan_puan += (tutan_kose == 3) ? 50 : 0;
					//				kazanilan_puan += (tutan_kose == 1) ? -500 : 0;
					//				//hile var olayı için
					//				if ((bos_kose == 3) && !((sutun == 9) && (satir == 3)))
					//					kazanilan_puan += -500;
					//				for (var i = 0; i < 3; i++)
					//					((int[,])eklenen.degisken_tut_ArrayList[2])[kontrol_arrayi[i, 0], kontrol_arrayi[i, 1]] = tas_degerleri[i];
					//				//string tabla = "\n";
					//				//for (int i = 0; i < 400; i++)
					//				//    tabla += ((((int[,])eklenen.degisken_tut_ArrayList[2])[i / 20, i - (i / 20) * 20] == -1) ? " " : ((int[,])eklenen.degisken_tut_ArrayList[2])[i / 20, i - (i / 20) * 20].ToString()) + (((i % 20 == 0) && (i != 0)) ? "\n" : ",");
					//				//yaz(tabla, tx_donut);
					//				eklenen.degisken_tut_ArrayList[0] = ((string)eklenen.degisken_tut_ArrayList[0]).Replace(gelen_oyun_bilgisi[0], "");
					//				//sirayi degistirelim 
					//				eklenen.degisken_tut_ArrayList[8] = 0;
					//				eklenen.degisken_tut_ArrayList[4] = ((int)eklenen.degisken_tut_ArrayList[4] + 1) % herbisey_baglantim[(int)eklenen.degisken_tut_ArrayList[5]].Count;
					//				//bir_onceki kişinin puanını duyuralım
					//				eklenen.degisken_tut_ArrayList[3] = (int)eklenen.degisken_tut_ArrayList[3] + kazanilan_puan;
					//				var uniqeID = ((mySocCon)herbisey_baglantim[(int)eklenen.degisken_tut_ArrayList[5]][(int)eklenen.degisken_tut_ArrayList[4]]).uniqueID;
					//				//oyun bitti
					//				if (((string)eklenen.degisken_tut_ArrayList[0]).TrimEnd(new[] { '&' }) == "")
					//					oyunu_bitir((int)eklenen.degisken_tut_ArrayList[5], 0, eklenen.uniqueID);
					//				var simdi = DateTime.Now;
					//				foreach (mySocCon eklenenparca in herbisey_baglantim[(int)eklenen.degisken_tut_ArrayList[5]]) {
					//					eklenenparca.myStream.WriteLine(gelen_veri + "\0" + "6#" + uniqeID + "#4#" + eklenen.uniqueID + "/" + eklenen.degisken_tut_ArrayList[3] + "\0");
					//					eklenenparca.myStream.Flush();
					//					eklenenparca.degisken_tut_ArrayList[2] = eklenen.degisken_tut_ArrayList[2];
					//					eklenenparca.degisken_tut_ArrayList[4] = (int)eklenen.degisken_tut_ArrayList[4];
					//					eklenenparca.degisken_tut_ArrayList[6] = simdi;
					//				}
					//			}
					//			#endregion
					//			break;
					//		case 4:
					//			#region oyuncu pas geçti
					//			//6#oyuncu_id#4(pas)
					//			kazanilan_puan = -25;
					//		pas_geciyor:
					//			eklenen.degisken_tut_ArrayList[8] = 0;
					//			eklenen.degisken_tut_ArrayList[4] = ((int)eklenen.degisken_tut_ArrayList[4] + 1) % herbisey_baglantim[(int)eklenen.degisken_tut_ArrayList[5]].Count;
					//			eklenen.degisken_tut_ArrayList[3] = (int)eklenen.degisken_tut_ArrayList[3] + kazanilan_puan;
					//			var uniqeID_b = ((mySocCon)(herbisey_baglantim[(int)eklenen.degisken_tut_ArrayList[5]])[(int)eklenen.degisken_tut_ArrayList[4]]).uniqueID;
					//			foreach (mySocCon eklenenparca in herbisey_baglantim[(int)eklenen.degisken_tut_ArrayList[5]]) {
					//				eklenenparca.myStream.WriteLine("6#" + uniqeID_b + "#4#" + eklenen.uniqueID + "/" + eklenen.degisken_tut_ArrayList[3] + "\0");
					//				eklenenparca.myStream.Flush();
					//				eklenenparca.degisken_tut_ArrayList[4] = (int)eklenen.degisken_tut_ArrayList[4];
					//			}
					//			break;
					//			#endregion
					//	}
					//	break;
					//case "<policy-file-request/>":
					//	var gonder = "<?xml version='1.0'?><!DOCTYPE cross-domain-policy SYSTEM '/xml/dtds/cross-domain-policy.dtd'><cross-domain-policy> <allow-access-from secure='false' domain='" + dinlenen_ip + "' to-ports='2468'/></cross-domain-policy>";
					//	eklenen.myStream.WriteLine(gonder + "\0");
					//	eklenen.myStream.Flush();
					//	yaz(">>Policy file gonderildi.... 2 --");

					//	break;
			}
			if (!string.IsNullOrEmpty(metin_yazilacak))
				yaz(metin_yazilacak);
		}
		/// <summary>
		/// Oyunu bitirme işlemi
		/// </summary>
		/// <param name="oda_no">Hangi room kapatılacak</param>
		/// <param name="bitme_sekli">oyunun bitme şekli 0-Normal bitme 1- birisi çekildi</param>
		/// <param name="ayrilan_ID">oyun normal bittiyse kazanan belli ama biri çekildiyse çekilen kimdir</param>
		#region oyun bitti
		private void oyunu_bitir(int oda_no, int bitme_sekli, string ayrilan_ID) {
			// kim kazandı felan falanı database e yazmak lazım
			// puanları almak felan lazım
			// oyunun bitiş şekli 0: birinin taşı bitti 1: biri ayrıldı 2:pas geçerek 
			// string oda_no = ((int)eklenen.degisken_tut_ArrayList[5]).ToString();
			//var veri_tabanina_yazilacak = "";
			//if (oda_no <= 20) return;
			//var gonderilecek = "";
			//var oyun_odasi = findRoomByID(oda_no, null);
			//switch (bitme_sekli) {
			//	case 0:
			//		//oyunu biri kazandı
			//		var kalan_taslar = "";
			//		foreach (mySocCon eklenenparca in oyun_odasi.myUserConnections) {
			//			kalan_taslar += (string)eklenenparca.degisken_tut_ArrayList[0] + "&";
			//		}
			//		var puan_icin = kalan_taslar.Split(new[] { '/', '&' });
			//		// diğer insanların elindeki taşların puanları
			//		var puan = 0;
			//		for (var i = 1; i < puan_icin.Length; i += 2) {
			//			for (var j = 0; j < puan_icin[i].Length; j++)
			//				puan += int.Parse(puan_icin[i][j].ToString());
			//		}
			//		foreach (mySocCon eklenenparca in oyun_odasi.myUserConnections) {
			//			gonderilecek += eklenenparca.uniqueID + "/" + (((eklenenparca.uniqueID == ayrilan_ID) ? puan : 0) + (int)eklenenparca.degisken_tut_ArrayList[3]) + "&";
			//			veri_tabanina_yazilacak += "";
			//		}
			//		gonderilecek = gonderilecek.Substring(0, gonderilecek.Length - 2);
			//		foreach (mySocCon eklenenparca in oyun_odasi.myUserConnections) {
			//			eklenenparca.myStream.WriteLine("5#" + ayrilan_ID + " kazandı.#6#" + gonderilecek + "\0");
			//			eklenenparca.myStream.Flush();
			//		}
			//		break;
			//	case 2:
			//	case 1:
			//		foreach (mySocCon eklenenparca in oyun_odasi.myUserConnections) {
			//			gonderilecek += eklenenparca.uniqueID + "/" + (((eklenenparca.uniqueID == ayrilan_ID) ? -100 : 0) + (int)eklenenparca.degisken_tut_ArrayList[3]) + "&";
			//			veri_tabanina_yazilacak += "";
			//		}
			//		gonderilecek = (gonderilecek != "") ? gonderilecek.Substring(0, gonderilecek.Length - 2) : "";
			//		foreach (mySocCon eklenenparca in oyun_odasi.myUserConnections) {
			//			eklenenparca.myStream.WriteLine("5#" + ((bitme_sekli == 1) ? ayrilan_ID + " oyundan düştü." : "Berabere!") + "#6#" + gonderilecek + "\0");
			//			eklenenparca.myStream.Flush();
			//		}
			//		break;
			//}
			//foreach (mySocCon eklenenparca in oyun_odasi.myUserConnections) {
			//	eklenenparca.degisken_tut_ArrayList.Clear();
			//	if (ayrilan_ID != eklenenparca.uniqueID)
			//		eklenenparca.myRooms.Remove(oyun_odasi);
			//}

			//herbisey_baglantim.Remove(oyun_odasi);
			//kullanicilari_yaz();
			//oyun_odasi = null;

		}
		#endregion

		#region sıra kontrolü yapan yer
		private void oyunda_zaman_doldu_mu(object oda_no) {
			//	for (int i = 21; i < herbisey_baglantim.Length; i++) {
			//		if (herbisey_baglantim[i] != null)
			//			if ((herbisey_baglantim[i].Count != 0)) {
			//				if (((mySocCon)herbisey_baglantim[i][0]).degisken_tut_ArrayList.Count != 0) {
			//					var ne_kadar_kaldi = ((DateTime)((mySocCon)(herbisey_baglantim[i])[0]).degisken_tut_ArrayList[6]).AddSeconds(((int)((mySocCon)(herbisey_baglantim[i])[0]).degisken_tut_ArrayList[7]));
			//					var simdi = DateTime.Now;
			//					if (simdi.Subtract(ne_kadar_kaldi).TotalSeconds > 0) {
			//						var kazanilan_puan = 0;
			//						//6#oyuncu_id#4(pas)
			//						//sıradaki kişiyi bulalım ve bir güzellik yapalım
			//						var oynayamayan_no = (int)((mySocCon)herbisey_baglantim[i][0]).degisken_tut_ArrayList[4];
			//						var oynayamayan = (mySocCon)herbisey_baglantim[i][oynayamayan_no];
			//						oynayamayan.degisken_tut_ArrayList[3] = (int)oynayamayan.degisken_tut_ArrayList[3] + kazanilan_puan;
			//						var oynayacak_no = (oynayamayan_no + 1) % herbisey_baglantim[i].Count;
			//						oynayamayan.degisken_tut_ArrayList[4] = oynayacak_no;
			//						var oynayacak_uniqeID = ((mySocCon)herbisey_baglantim[i][oynayacak_no]).uniqueID;
			//						foreach (mySocCon eklenenparca in herbisey_baglantim[i]) {
			//							eklenenparca.myStream.WriteLine("6#" + oynayacak_uniqeID + "#4#" + oynayamayan.uniqueID + "/" + oynayamayan.degisken_tut_ArrayList[3] + "\0");
			//							eklenenparca.myStream.Flush();
			//							eklenenparca.degisken_tut_ArrayList[4] = (int)oynayamayan.degisken_tut_ArrayList[4];
			//							eklenenparca.degisken_tut_ArrayList[6] = simdi;
			//						}
			//					}
			//				}
			//			}
			//	}
		}
		#endregion

		/// <summary>
		/// odaya kaydolanları oyuna hazırlıyor ve gerekli ilk işlemleri yapıyor
		/// </summary>
		/// <param name="oda_no">odanın indisi</param>
		/// 
		private void oyunu_baslat(int oda_no, int turn_suresi) {
			//// 6#oyuncu_id#1(taş çekti)#bilmam_kac_nolu_Tas&.....&..... taşı benden gizle	
			//// 6#oyuncu_id#2(çektiğim taş)#bilmam_kac_nolu_Tas/022(değeri)&....                   
			//// 1/001&8/002&25/025&4/555&12/245&5/233&56/244
			//// 6#oyuncu_id#4 sıra bu oyuncu_id de
			//var taslar_org = new ArrayList();
			//var taslar_array = "000, 001, 002, 003, 004, 005, 011, 012, 013, 014, 015, 021, 022, 023, 024, 025, 031, 032, 033, 034, 035, 041, 042, 043, 044, 045, 051, 052, 053, 054, 055, 111, 112, 113, 114, 115, 122, 123, 124, 125, 132, 133, 134, 135, 142, 143, 144, 145, 152, 153, 154, 155, 222, 223, 224, 225, 233, 234, 235, 243, 244, 245, 253, 254, 255, 333, 334, 335, 344, 345, 354, 355, 444, 445, 455, 555".Split(new[] { ", " }, StringSplitOptions.None);
			//for (var i = 0; i < taslar_array.Length; i++)
			//	taslar_org.Add(taslar_array[i]);
			//var a = new Random();
			//var taslar = new ArrayList();
			////karıştır
			//var sira = 0;
			//while (0 < taslar_org.Count) {
			//	var secim = a.Next(0, taslar_org.Count);
			//	taslar.Add(sira++ + "/" + taslar_org[secim]);
			//	taslar_org.RemoveAt(secim);
			//}
			//var yollanacak = new string[herbisey_baglantim[oda_no].Count];
			//var silinecek = new string[herbisey_baglantim[oda_no].Count];
			//var uniqeIDs = new string[herbisey_baglantim[oda_no].Count];
			//sira = 0;
			//foreach (mySocCon eklenenparca in herbisey_baglantim[oda_no]) {
			//	for (var i = 0; i < 9; i++) {
			//		var secim = a.Next(0, taslar.Count);
			//		yollanacak[sira] += taslar[secim] + "&";
			//		silinecek[sira] += ((string)taslar[secim]).Substring(0, ((string)taslar[secim]).IndexOf("/")) + "&";
			//		// elimdeki taşlar //
			//		taslar.RemoveAt(secim);
			//	}
			//	silinecek[sira] = silinecek[sira].Substring(0, silinecek[sira].Length - 1);
			//	yollanacak[sira] = yollanacak[sira].Substring(0, yollanacak[sira].Length - 1);
			//	uniqeIDs[sira] = eklenenparca.uniqueID;
			//	eklenenparca.myStream.WriteLine("6#" + eklenenparca.uniqueID + "#2#" + yollanacak[sira] + "\0");
			//	//arraylistin 0. elemanı elimdeki taşlar
			//	eklenenparca.degisken_tut_ArrayList.Add(yollanacak[sira]);
			//	eklenenparca.myStream.Flush();
			//	sira++;
			//}
			//sira = a.Next(silinecek.Length);
			//var kul_sayisi = silinecek.Length;
			//var ortak_yer_arrayi = new int[20, 20];
			//for (var i = 0; i < 400; i++)
			//	ortak_yer_arrayi[i / 20, i - (i / 20) * 20] = -1;
			////burada bir olay var o da taslar ve diger paylaşılan elemanlar aslında aynı şeye refer ediyor olmalı kontrol edilmeli

			//var simdi = DateTime.Now;
			//foreach (mySocCon eklenenparca in herbisey_baglantim[oda_no]) {
			//	//arraylistin 1. elemanı kalan taşlar
			//	eklenenparca.degisken_tut_ArrayList.Add(taslar);
			//	//arraylistin 2. elamanı yerdeki taşlar
			//	eklenenparca.degisken_tut_ArrayList.Add(ortak_yer_arrayi);
			//	//arraylistin 3. elamanı puanlar
			//	var puanim = 0;
			//	eklenenparca.degisken_tut_ArrayList.Add(puanim);
			//	//arraylistin 4. elemanı sıranın kimde olduğu
			//	eklenenparca.degisken_tut_ArrayList.Add(sira);
			//	//arraylistin 5. elemanı room no
			//	eklenenparca.degisken_tut_ArrayList.Add(oda_no);
			//	for (var i = 0; i < silinecek.Length; i++) {
			//		eklenenparca.myStream.WriteLine("6#" + uniqeIDs[i] + "#1#" + silinecek[i] + "\0");
			//		eklenenparca.myStream.WriteLine("6#" + ((mySocCon)herbisey_baglantim[oda_no][sira]).uniqueID + "#4#" + ((mySocCon)herbisey_baglantim[oda_no][sira]).uniqueID + "/0\0");
			//		eklenenparca.myStream.Flush();
			//	}
			//	//arraylistin 6. elemanı başladığı zaman
			//	eklenenparca.degisken_tut_ArrayList.Add(simdi);
			//	//arraylistin 7. turn over zamanı
			//	eklenenparca.degisken_tut_ArrayList.Add(turn_suresi);
			//	//arraylistin 8. taş çekme sayisi
			//	eklenenparca.degisken_tut_ArrayList.Add(0);
			//}

		}
		/// <summary>
		/// Boş room bulan ilk gördüğü booş odayı söylüyor arkadaş
		/// </summary>
		/// <returns>Odanın indisi</returns>
		//private int bos_oda_bul() {
		//	for (var i = 21; i < herbisey_baglantim.Length; i++)
		//		if (herbisey_baglantim[i].Count == 0) return i;
		//	return -1;
		//}
		private void dinleyen_server() {
			//port açlıyor ve dinleme işlemi başladııııı
			var oyunda_zaman_doldu_fn = new TimerCallback(oyunda_zaman_doldu_mu);
			//var zamani_kollayan = new System.Threading.Timer(oyunda_zaman_doldu_fn, null, 0, 1000);

			var dinle = new TcpListener(IPAddress.Parse(dinlenen_ip), dinlenen_port);
			yaz("Dinliyorum\nip: " + dinle.LocalEndpoint);
			dinle.Start();
			dinle.Server.Listen(1000);
			//for (var i = 0; i < herbisey_baglantim.Length; i++)
			//	herbisey_baglantim[i] = new room();
			while (true) {
				var baglanti = dinle.AcceptSocket();
				//int ilk_baglanacagim_oda = 0;
				var eklenen = new mySocCon {
					mySocket = baglanti,
					myRooms = new List<room>(),
					//	degisken_tut_ArrayList = new ArrayList()
				};
				//eklenen.odam.Add(ilk_baglanacagim_oda);
				//herbisey_baglantim[ilk_baglanacagim_oda].Add(eklenen);
				var baglanti_thread = new Thread(baglanti_kuran);
				baglanti_thread.Start(eklenen);
			}
			//baglanti.Close();
			//dinle.Stop();
		}
		private Char[] bos_karakter = new Char[] { '\0' };

		private void baglanti_kuran(object mySocket) {
			var eklenen = mySocket as mySocCon;
			var baglanti = eklenen.mySocket;
			var aki = new NetworkStream(baglanti);
			var aki_oku = new StreamReader(aki);
			eklenen.myStream = new StreamWriter(aki);
			if (baglanti.Connected) {
				var nicki = " - " + IPAddress.Parse(((IPEndPoint)baglanti.RemoteEndPoint).Address.ToString()) + ": " + ((IPEndPoint)baglanti.RemoteEndPoint).Port;
				//string nicki = aki_oku.ReadLine() + " - " + IPAddress.Parse(((IPEndPoint)baglanti.RemoteEndPoint).Address.ToString()).ToString() + ": " + ((IPEndPoint)baglanti.RemoteEndPoint).Port.ToString();
				yaz(nicki + ".....Bağlandı");
				try {
					while (true) {
						var gelenveri_dizi = new char[baglanti.ReceiveBufferSize];
						aki_oku.Read(gelenveri_dizi, 0, baglanti.ReceiveBufferSize);
						var gelenveri = new string(gelenveri_dizi);
						gelenveri = gelenveri.TrimEnd('\0');
						gelenveri = gelenveri.TrimEnd('\n');
						gelenveri = gelenveri.TrimEnd('\r');
						if (gelenveri.IndexOfAny(bos_karakter) > -1) {
							var gelenveri_cogul = gelenveri.Split(bos_karakter);
							for (var i = 0; i < gelenveri_cogul.Length; i++)
								veri_geldi(gelenveri_cogul[i], eklenen);
						} else {
							veri_geldi(gelenveri, eklenen);
						}
					}
				} catch {
					baglanti_koptu(eklenen, nicki, "...");
				}
			}
			aki.Close();
		}
		//private void policy_file_gonderen() {
		//	var policy_veren = new TcpListener(IPAddress.Parse(dinlenen_ip), 843);
		//	policy_veren.Start();
		//	policy_veren.Server.Listen(1000);
		//	while (true) {
		//		try {
		//			var baglanti_policy = policy_veren.AcceptSocket();
		//			var aki = new NetworkStream(baglanti_policy);
		//			var aki_oku = new StreamReader(aki);
		//			var myStream = new StreamWriter(aki);
		//			var gelenveri_dizi = new char[baglanti_policy.ReceiveBufferSize];
		//			aki_oku.Read(gelenveri_dizi, 0, baglanti_policy.ReceiveBufferSize);
		//			var gelenveri = new string(gelenveri_dizi);
		//			gelenveri = gelenveri.TrimEnd('\0');
		//			gelenveri = gelenveri.TrimEnd('\n');
		//			gelenveri = gelenveri.TrimEnd('\r');
		//			if (gelenveri == "<policy-file-request/>") {
		//				var gonder = "<?xml version='1.0'?><!DOCTYPE cross-domain-policy SYSTEM '/xml/dtds/cross-domain-policy.dtd'><cross-domain-policy> <allow-access-from secure='false' domain='" + dinlenen_ip + "' to-ports='2468'/></cross-domain-policy>";
		//				myStream.WriteLine(gonder + "\0");
		//				myStream.Flush();
		//				yaz(">>Policy file gonderildi....");
		//			}
		//		} catch {
		//			return;
		//		}
		//	}
		//}
		private void baglanti_koptu(mySocCon eklenen, string nicki, string hata) {
			bekleten.WaitOne();
			//eklenen her odadan çıkartıyorum ama aynı zamanda oynana bi olay varsa oyunu da bitirmeliyim
			foreach (var i in eklenen.myRooms)
				oyunu_bitir(i.roomID, 1, eklenen.uniqueID);
			for (int i = 0; i < eklenen.myRooms.Count; i++) {
				room? j = eklenen.myRooms[i];
				herbisey_baglantim.Remove(j);
				if (j.myUserConnections.Count == 0) {
					j = null;
					i--;
				}
			}
			yaz(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + ": " + eklenen.uniqueID
				+ " bağlantı koptu. IP:" + IPAddress.Parse(((IPEndPoint)eklenen.mySocket.RemoteEndPoint).Address.ToString()) + ": " + ((IPEndPoint)eklenen.mySocket.RemoteEndPoint).Port
				+ "." + hata);
			eklenen = null;
			bekleten.ReleaseMutex();
			kullanicilari_yaz();
			Thread.CurrentThread.Abort();
		}


		void kullanicilari_yaz() {
			//if (dg_herbisey.InvokeRequired) {
			//	var d = new kullanicilari_yaz_icin(kullanicilari_yaz);
			//	Invoke(d);
			//} else {
			var say = 0;
			//	dg_herbisey.Rows.Clear();
			//	dg_herbisey.Rows.Add(herbisey_baglantim.Length);
			string names = string.Join(", ", CCUs.Select(x => x.userEmail));
			Console.Write(CCUs.Count + " > " + names);
			//for (var i = 0; i < CCUs.Count; i++) {

			//	say++;
			//	//	dg_herbisey.Rows[say].Cells[0].Value = i;
			//	//dg_herbisey.Rows[say].Cells[1].Value = "";
			//	for (var j = 0; j < herbisey_baglantim[i].Count; j++) {
			//		//dg_herbisey.Rows[say].Cells[1].Value += ((mySocCon)herbisey_baglantim[i][j]).uniqueID + " ";
			//	}
			//}
			//}

		}
	}
}
