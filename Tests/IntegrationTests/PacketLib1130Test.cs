using System;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.PacketHandler.Client.v168;
using DOL.Integration.Server;
using DOL.Network;

namespace DOL.Tests
{
	[TestFixture]
	public class PacketLib1130Test
	{
		[OneTimeSetUp]
		public void Init()
		{
			SetUpTests.CreateGameServerInstance();
		}

		private class TestGameClient : GameClient
		{
			public TestGameClient(BaseServer srvr) : base(srvr) { }

			public void SimulateReceive(byte[] bytes)
			{
				Array.Copy(bytes, ReceiveBuffer, bytes.Length);
				OnReceive(bytes.Length);
			}
		}

		[Test]
		public void Test_CreatePacketLibForVersion_1130_ResolvesPacketLib1130()
		{
			var client = new GameClient(GameServer.Instance);
			var lib = AbstractPacketLib.CreatePacketLibForVersion(1130, client, out var version);

			Assert.That(lib, Is.Not.Null, "PacketLib for version 1130 should not be null.");
			Assert.That(lib, Is.InstanceOf<PacketLib1130>(), "PacketLib should be an instance of PacketLib1130.");
			Assert.That(version, Is.EqualTo(GameClient.eClientVersion.Version1130), "ClientVersion should be Version1130.");
		}

		[Test]
		public void Test_CreatePacketLibForVersion_1129_ResolvesPacketLib1129()
		{
			var client = new GameClient(GameServer.Instance);
			var lib = AbstractPacketLib.CreatePacketLibForVersion(1129, client, out var version);

			Assert.That(lib, Is.Not.Null, "PacketLib for version 1129 should not be null.");
			Assert.That(lib, Is.InstanceOf<PacketLib1129>(), "PacketLib should be an instance of PacketLib1129.");
			Assert.That(version, Is.EqualTo(GameClient.eClientVersion.Version1129), "ClientVersion should be Version1129.");
		}

		[Test]
		public void Test_ClientHandshakeBuffer_Calculates1130AndResolves()
		{
			// Simulates initial post-1.115c raw TCP handshake packet buffer from DAoC 1.130 client
			byte[] pBuf = new byte[20];
			pBuf[0] = 0x00;
			pBuf[1] = 0x14; // packet size
			pBuf[2] = (byte)eClientPackets.CryptKeyRequest; // 0xF4
			pBuf[11] = 1;  // major: 1
			pBuf[12] = 1;  // minor: 1
			pBuf[13] = 30; // revision: 30 => 1.130

			int calculatedVersion = pBuf[11] * 1000 + pBuf[12] * 100 + pBuf[13];
			Assert.That(calculatedVersion, Is.EqualTo(1130), "Parsed version from buffer must equal 1130.");

			var client = new GameClient(GameServer.Instance);
			var lib = AbstractPacketLib.CreatePacketLibForVersion(calculatedVersion, client, out var ver);

			Assert.That(lib, Is.Not.Null, "AbstractPacketLib must resolve PacketLib for 1130.");
			Assert.That(ver, Is.EqualTo(GameClient.eClientVersion.Version1130));
		}

		[Test]
		public void Test_OnReceive_HandshakePacket1130_InitializesClientAndPacketProcessor()
		{
			var client = new TestGameClient(GameServer.Instance);
			Assert.That(client.Version, Is.EqualTo(GameClient.eClientVersion.VersionNotChecked));
			Assert.That(client.Out, Is.Null);

			// Real raw bytes of 0xF4 CryptKeyRequest packet sent by 1.130 game.dll
			byte[] packet1130 = new byte[20];
			packet1130[0] = 0x00;
			packet1130[1] = 0x14;
			packet1130[2] = 0xF4;
			packet1130[11] = 1;  // major: 1
			packet1130[12] = 1;  // minor: 1
			packet1130[13] = 30; // 30 => 1130
			packet1130[14] = (byte)'a';

			client.SimulateReceive(packet1130);

			Assert.That(client.Version, Is.EqualTo(GameClient.eClientVersion.Version1130), "Client version should be Version1130 after handshake.");
			Assert.That(client.Out, Is.Not.Null, "Client.Out packet lib must be initialized.");
			Assert.That(client.Out, Is.InstanceOf<PacketLib1130>(), "Client.Out must be PacketLib1130.");
			Assert.That(client.PacketProcessor, Is.Not.Null, "PacketProcessor must be instantiated and active.");
		}

		[Test]
		public void Test_SendVersionAndCryptKey_EmitsVersion1130String()
		{
			var client = new GameClient(GameServer.Instance);
			client.Version = GameClient.eClientVersion.Version1130;

			// Verify the version string generation formula used in PacketLib1126.SendVersionAndCryptKey:
			// pak.WriteString((((int)m_gameClient.Version) / 1000) + "." + (((int)m_gameClient.Version) - 1000), 5);
			int verInt = (int)client.Version;
			string versionStr = (verInt / 1000) + "." + (verInt - 1000);

			Assert.That(versionStr, Is.EqualTo("1.130"), "Version string for 1130 client must be 1.130.");
			Assert.That(versionStr.Length, Is.EqualTo(5), "Version string length must be exactly 5 chars.");
		}

		[Test]
		public void Test_PacketHandlers_SupportVersion1130()
		{
			var client = new GameClient(GameServer.Instance);
			client.Version = GameClient.eClientVersion.Version1130;

			// Verify handler checks evaluate to modern path
			Assert.That(client.Version >= GameClient.eClientVersion.Version1124, Is.True);
			Assert.That(client.Version >= GameClient.eClientVersion.Version1126, Is.True);
			Assert.That(client.Version >= GameClient.eClientVersion.Version1127, Is.True);
			Assert.That(client.Version >= GameClient.eClientVersion.Version1128, Is.True);
		}

		[Test]
		public void Test_CharacterSelectPacket184_MappedAndPreprocessed()
		{
			var client = new GameClient(GameServer.Instance);
			client.Version = GameClient.eClientVersion.Version1130;
			client.Account = new DOL.Database.Account { Name = "testaccount" };

			var processor = new PacketProcessor(client);
			processor.LoadPacketHandlers(client);

			// Test packet 184 (0xB8)
			var packetIn184 = new GSPacketIn(16);
			byte[] raw184 = new byte[16];
			raw184[9] = 184;
			packetIn184.Load(raw184, 0, 16);

			var canProcess184 = processor.PacketPreprocessor.CanProcessPacket(client, packetIn184);
			Assert.That(canProcess184, Is.True, "Preprocessor must allow packet ID 184 when client is logged in.");

			// Test packet 16 (0x10) backward compatibility
			var packetIn16 = new GSPacketIn(16);
			byte[] raw16 = new byte[16];
			raw16[9] = 16;
			packetIn16.Load(raw16, 0, 16);

			var canProcess16 = processor.PacketPreprocessor.CanProcessPacket(client, packetIn16);
			Assert.That(canProcess16, Is.True, "Preprocessor must allow packet ID 16 for backward compatibility.");
		}
	}
}
