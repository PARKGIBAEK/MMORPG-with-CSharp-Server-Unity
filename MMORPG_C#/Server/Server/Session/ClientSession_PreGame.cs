using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using Server.DB;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server
{
   public partial class ClientSession : PacketSession
   {
      public void HandleLogin(C_Login loginPacket)
      {
			//Console.WriteLine($"UniqueId({loginPacket.UniqueId})");

			//TODO : 보안체크
			if (ServerState != PlayerServerState.ServerStateLogin)
				return;

			//TODO : 문제
			// - 동시에 다른 사람이 같은 uniqueId를 보낸다면?
			// - 악의적으로 같은 패킷을 여러번 보낸다면? DB에 과부하가 발생할 수 있음
			// - 쌩뚱맞은 타이밍에 이 패킷을 보낸다면?

			using (AppDbContext db = new AppDbContext())
			{
				AccountDb findAccount = db.Accounts
					.Include(a=>a.Players)
					.Where(a => a.AccountName == loginPacket.UniqueId)
					.FirstOrDefault();

				if (findAccount != null)
				{
					S_Login loginOk = new S_Login() { LoginOk = 1 };
					Send(loginOk);
					Console.WriteLine($"기존 캐릭터 LoginOK = {loginOk.LoginOk}");
				}
				else
				{
					AccountDb newAccount = new AccountDb() { AccountName = loginPacket.UniqueId };
					db.Accounts.Add(newAccount);
					db.SaveChanges();// TODO : Exception

					S_Login loginOk = new S_Login() { LoginOk = 1 };
					Send(loginOk);
					Console.WriteLine($"신규캐릭터 LoginOK = {loginOk.LoginOk}");
				}
			}
		}
   }
}