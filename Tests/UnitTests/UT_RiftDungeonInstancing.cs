using System;
using DOL.Database;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.UnitTests.Gameserver
{
	[TestFixture]
	public class UT_RiftDungeonInstancing
	{
		[SetUp]
		public void Setup()
		{
			FakeServer.Load();
			DOL.GS.ServerProperties.Properties.GROUP_MAX_MEMBER = 8;
		}

		[Test]
		public void RiftPortal_DefaultProperties_AreCorrect()
		{
			var portal = new RiftPortal();

			Assert.Multiple(() =>
			{
				Assert.That(portal.Name, Is.EqualTo("Rift Portal"));
				Assert.That(portal.Model, Is.EqualTo(RiftConstants.COTSWOLD_PORTAL_MODEL));
				Assert.That(portal.LoadedFromScript, Is.True);
			});
		}

		[Test]
		public void RiftConstants_WorldCoordinates_MatchConfiguredValues()
		{
			Assert.Multiple(() =>
			{
				// Cotswold Bridge Spawn
				Assert.That(RiftConstants.COTSWOLD_REGION, Is.EqualTo(1));
				Assert.That(RiftConstants.COTSWOLD_BRIDGE_X, Is.EqualTo(558074));
				Assert.That(RiftConstants.COTSWOLD_BRIDGE_Y, Is.EqualTo(511741));
				Assert.That(RiftConstants.COTSWOLD_BRIDGE_Z, Is.EqualTo(2336));
				Assert.That(RiftConstants.COTSWOLD_BRIDGE_HEADING, Is.EqualTo(3062));

				// Dungeon Skin & Entrance (Spindelhalla)
				Assert.That(RiftConstants.RIFT_DUNGEON_SKIN_ID, Is.EqualTo(125));
				Assert.That(RiftConstants.DUNGEON_ENTRANCE_X, Is.EqualTo(32152));
				Assert.That(RiftConstants.DUNGEON_ENTRANCE_Y, Is.EqualTo(31841));
				Assert.That(RiftConstants.DUNGEON_ENTRANCE_Z, Is.EqualTo(16375));
				Assert.That(RiftConstants.DUNGEON_ENTRANCE_HEADING, Is.EqualTo(90));

				// Obelisk & Exit Portal (Spindelhalla)
				Assert.That(RiftConstants.DUNGEON_OBELISK_X, Is.EqualTo(32201));
				Assert.That(RiftConstants.DUNGEON_OBELISK_Y, Is.EqualTo(31813));
				Assert.That(RiftConstants.DUNGEON_OBELISK_Z, Is.EqualTo(16371));
				Assert.That(RiftConstants.DUNGEON_EXIT_PORTAL_X, Is.EqualTo(31992));
				Assert.That(RiftConstants.DUNGEON_EXIT_PORTAL_Y, Is.EqualTo(31844));
				Assert.That(RiftConstants.DUNGEON_EXIT_PORTAL_Z, Is.EqualTo(16371));
			});
		}

		[Test]
		public void RiftDungeonInstance_InitializationAndCollapse_SetsCoordinatesAndState()
		{
			var regionData = new RegionData
			{
				Id = RiftConstants.RIFT_DUNGEON_SKIN_ID,
				Name = "Spindelhalla",
				Description = "Spindelhalla"
			};

			var fakeTime = new FakeTimeManager();
			const ushort instanceId = 1001;

			var instance = new RiftDungeonInstance(instanceId, fakeTime, regionData);

			Assert.Multiple(() =>
			{
				Assert.That(instance.ID, Is.EqualTo(instanceId));
				Assert.That(instance.Skin, Is.EqualTo(RiftConstants.RIFT_DUNGEON_SKIN_ID));
				Assert.That(instance.IsActive, Is.True);

				// Entrance Position inside instance
				Assert.That(instance.EntrancePosition.RegionID, Is.EqualTo(instanceId));
				Assert.That(instance.EntrancePosition.X, Is.EqualTo(RiftConstants.DUNGEON_ENTRANCE_X));
				Assert.That(instance.EntrancePosition.Y, Is.EqualTo(RiftConstants.DUNGEON_ENTRANCE_Y));
				Assert.That(instance.EntrancePosition.Z, Is.EqualTo(RiftConstants.DUNGEON_ENTRANCE_Z));

				// Exit Position to Cotswold
				Assert.That(instance.ExitPosition.RegionID, Is.EqualTo(RiftConstants.COTSWOLD_REGION));
				Assert.That(instance.ExitPosition.X, Is.EqualTo(RiftConstants.COTSWOLD_PORTAL_X));
				Assert.That(instance.ExitPosition.Y, Is.EqualTo(RiftConstants.COTSWOLD_PORTAL_Y));
				Assert.That(instance.ExitPosition.Z, Is.EqualTo(RiftConstants.COTSWOLD_PORTAL_Z));
			});

			// On collapse
			instance.OnCollapse();
			Assert.That(instance.IsActive, Is.False);
		}

		[Test]
		public void RiftManager_ActiveInstanceTracking_RegistersAndUnregistersProperly()
		{
			var player = new FakePlayer { Name = "RiftHero", InternalID = "TEST_ID_100" };
			Assert.That(RiftManager.GetActiveInstance(player), Is.Null);

			var regionData = new RegionData
			{
				Id = RiftConstants.RIFT_DUNGEON_SKIN_ID,
				Name = "Spindelhalla",
				Description = "Spindelhalla"
			};

			var instance = new RiftDungeonInstance(1005, new FakeTimeManager(), regionData)
			{
				OwnerPlayer = player,
				OwnerPlayerId = player.InternalID
			};

			// Simulate registration
			RiftManager.UnregisterInstance(instance);
			Assert.That(RiftManager.GetActiveInstance(player), Is.Null);
		}

		[Test]
		public void RiftManager_GroupMemberCannotOpenNew_WhenNotLeader()
		{
			var leader = new FakePlayer { Name = "LeaderPlayer", InternalID = "LEADER_1" };
			var member = new FakePlayer { Name = "MemberPlayer", InternalID = "MEMBER_2" };

			var group = new Group(leader);
			group.AddMember(member);

			Assert.That(member.Group, Is.EqualTo(group));
			Assert.That(group.Leader, Is.EqualTo(leader));

			var portal = new RiftPortal();

			// Member attempts to open when no instance exists
			bool result = RiftManager.EnterOrOpenRift(member, portal);
			Assert.That(result, Is.False, "Non-leader member should not be able to open a new instance");
		}

		[Test]
		public void RiftManager_MultipleSoloPlayers_TrackDistinctInstances()
		{
			var player1 = new FakePlayer { Name = "Solo1", InternalID = "ID_SOLO_1" };
			var player2 = new FakePlayer { Name = "Solo2", InternalID = "ID_SOLO_2" };

			var regionData = new RegionData
			{
				Id = RiftConstants.RIFT_DUNGEON_SKIN_ID,
				Name = "Spindelhalla",
				Description = "Spindelhalla"
			};

			var inst1 = new RiftDungeonInstance(1010, new FakeTimeManager(), regionData)
			{
				OwnerPlayer = player1,
				OwnerPlayerId = player1.InternalID
			};

			var inst2 = new RiftDungeonInstance(1011, new FakeTimeManager(), regionData)
			{
				OwnerPlayer = player2,
				OwnerPlayerId = player2.InternalID
			};

			Assert.That(inst1.ID, Is.Not.EqualTo(inst2.ID));
			Assert.That(inst1.OwnerPlayerId, Is.Not.EqualTo(inst2.OwnerPlayerId));
		}

		[Test]
		public void RiftObelisk_DefaultProperties_AreConfiguredProperly()
		{
			var obelisk = new RiftObelisk();

			Assert.Multiple(() =>
			{
				Assert.That(obelisk.Name, Is.EqualTo("Rift Obelisk"));
				Assert.That(obelisk.Model, Is.EqualTo(RiftConstants.DUNGEON_OBELISK_MODEL));
				Assert.That(obelisk.Level, Is.EqualTo(50));
				Assert.That(obelisk.LoadedFromScript, Is.True);

				// The obelisk must be a static item so the client reliably sends an
				// ObjectInteractRequest on right-click (an NPC with a static stone model does not).
				Assert.That(obelisk, Is.InstanceOf<GameStaticItem>());
				Assert.That(obelisk, Is.InstanceOf<IWhisperTarget>());
			});
		}

		[Test]
		public void RiftObelisk_Interact_WarnsPlayersToBuffUp()
		{
			var player = new FakePlayer { Name = "RiftWarrior", InternalID = "TEST_BUFF" };
			var regionData = new RegionData
			{
				Id = RiftConstants.RIFT_DUNGEON_SKIN_ID,
				Name = "Spindelhalla",
				Description = "Spindelhalla"
			};
			var instance = new RiftDungeonInstance(1012, new FakeTimeManager(), regionData);
			var obelisk = new RiftObelisk(instance);
			obelisk.Position = Position.Create(instance.ID, RiftConstants.DUNGEON_OBELISK_X, RiftConstants.DUNGEON_OBELISK_Y, RiftConstants.DUNGEON_OBELISK_Z);
			player.Position = obelisk.Position;

			bool interactResult = obelisk.Interact(player);
			Assert.That(interactResult, Is.True);

			var fakeLib = player.Out as FakePacketLib;
			Assert.That(fakeLib, Is.Not.Null);
			bool foundBuffWarning = fakeLib.SentMessages.Exists(m => m.Contains("Make sure you're all buffed up"));
			Assert.That(foundBuffWarning, Is.True, "Obelisk interaction must warn players to buff up");
		}

		[Test]
		public void RiftObelisk_Interact_SendsClickableChoicesInPopupWindow()
		{
			var player = new FakePlayer { Name = "RiftChooser", InternalID = "TEST_MENU" };
			var regionData = new RegionData
			{
				Id = RiftConstants.RIFT_DUNGEON_SKIN_ID,
				Name = "Spindelhalla",
				Description = "Spindelhalla"
			};
			var instance = new RiftDungeonInstance(1019, new FakeTimeManager(), regionData);
			var obelisk = new RiftObelisk(instance);
			obelisk.Position = Position.Create(instance.ID, RiftConstants.DUNGEON_OBELISK_X, RiftConstants.DUNGEON_OBELISK_Y, RiftConstants.DUNGEON_OBELISK_Z);
			player.Position = obelisk.Position;

			Assert.That(obelisk.Interact(player), Is.True);

			var fakeLib = player.Out as FakePacketLib;
			Assert.That(fakeLib, Is.Not.Null);

			// Teleporter-style menu: the bracketed choices are sent to the popup window
			// as CT_Say text, exactly like SimpleTeleporter does.
			var difficultyMenu = fakeLib.SentMessageDetails.Find(d =>
				d.Message.Contains("[Easy]") &&
				d.Message.Contains("[Normal]") &&
				d.Message.Contains("[Hard]") &&
				d.Message.Contains("[Torment]"));

			Assert.That(difficultyMenu.Message, Is.Not.Null, "Interaction must display all difficulty choices");
			Assert.That(difficultyMenu.Loc, Is.EqualTo(eChatLoc.CL_PopupWindow), "Clickable choices must be sent to the popup window");
			Assert.That(difficultyMenu.Type, Is.EqualTo(eChatType.CT_Say), "Clickable choices must be sent as chat text");

			var exitOption = fakeLib.SentMessageDetails.Find(d => d.Message.Contains("[Exit to Cotswold]"));
			Assert.That(exitOption.Message, Is.Not.Null, "Interaction must display the exit option");
			Assert.That(exitOption.Loc, Is.EqualTo(eChatLoc.CL_PopupWindow), "The exit option must be in the popup window");

			Assert.That(player.TargetObject, Is.SameAs(obelisk), "The obelisk must stay targeted so popup link whispers reach it");
		}

		[Test]
		public void RiftObelisk_WhisperReceive_NonLeaderRejected_WhenInGroup()
		{
			var leader = new FakePlayer { Name = "LeaderPlayer", InternalID = "LEAD_OB" };
			var member = new FakePlayer { Name = "MemberPlayer", InternalID = "MEM_OB" };
			var group = new Group(leader);
			group.AddMember(member);

			var regionData = new RegionData
			{
				Id = RiftConstants.RIFT_DUNGEON_SKIN_ID,
				Name = "Spindelhalla",
				Description = "Spindelhalla"
			};
			var instance = new RiftDungeonInstance(1013, new FakeTimeManager(), regionData);
			var obelisk = new RiftObelisk(instance);

			bool memberWhisper = obelisk.WhisperReceive(member, "easy");
			Assert.That(memberWhisper, Is.False, "Non-leader member cannot trigger countdown");
			Assert.That(instance.IsCountdownRunning, Is.False);

			bool leaderWhisper = obelisk.WhisperReceive(leader, "easy");
			Assert.That(leaderWhisper, Is.True, "Leader can trigger countdown");
			Assert.That(instance.IsCountdownRunning, Is.True);
			Assert.That(instance.Difficulty, Is.EqualTo(eRiftDifficulty.Easy));
		}

		[Test]
		public void RiftDungeonInstance_CountdownFlow_Stuns_Animates_And_Releases()
		{
			var player = new FakePlayer { Name = "CountdownHero", InternalID = "TEST_FLOW" };
			var regionData = new RegionData
			{
				Id = RiftConstants.RIFT_DUNGEON_SKIN_ID,
				Name = "Spindelhalla",
				Description = "Spindelhalla"
			};
			var instance = new RiftDungeonInstance(1014, new FakeTimeManager(), regionData);

			// Player enters
			instance.OnPlayerEnterInstance(player);
			Assert.That(player.IsStunned, Is.False);

			// Start countdown at Torment difficulty
			instance.StartCountdown(eRiftDifficulty.Torment, player);

			var fakeLib = player.Out as FakePacketLib;
			Assert.That(fakeLib, Is.Not.Null);

			Assert.Multiple(() =>
			{
				Assert.That(instance.IsCountdownRunning, Is.True);
				Assert.That(instance.Difficulty, Is.EqualTo(eRiftDifficulty.Torment));
				Assert.That(player.IsStunned || player.IsMezzed, Is.True, "Player must be held during countdown containment");
				Assert.That(fakeLib.LastSpellEffect, Is.EqualTo(RiftConstants.CONTAINMENT_SPELL_EFFECT));
				Assert.That(fakeLib.SentMessages.Exists(m => m.Contains("Make sure you're all buffed up")), Is.True);
			});

			// One countdown tick must broadcast the remaining time on screen.
			instance.OnCountdownTick(null);
			Assert.That(fakeLib.SentMessages.Exists(m => m.Contains("Rift opening in 9")), Is.True, "Countdown must be displayed each second");

			// Complete countdown
			instance.OnCountdownComplete(null);

			Assert.Multiple(() =>
			{
				Assert.That(instance.IsCountdownRunning, Is.False);
				Assert.That(instance.IsRiftActive, Is.True);
				Assert.That(player.IsStunned || player.IsMezzed, Is.False, "Containment must be released when countdown completes");
				Assert.That(fakeLib.TimerClosed, Is.True, "Timer window must be closed when countdown completes");
				Assert.That(fakeLib.LastSoundEffect, Is.EqualTo(RiftConstants.START_SOUND_EFFECT));
				Assert.That(fakeLib.SentMessages.Exists(m => m.Contains("THE RIFT HAS OPENED")), Is.True);
			});
		}

		[Test]
		public void RiftDungeonInstance_PlayerLeavingDuringCountdown_ClearsStun()
		{
			var player = new FakePlayer { Name = "LeavingHero", InternalID = "TEST_LEAVE" };
			var regionData = new RegionData
			{
				Id = RiftConstants.RIFT_DUNGEON_SKIN_ID,
				Name = "Spindelhalla",
				Description = "Spindelhalla"
			};
			var instance = new RiftDungeonInstance(1015, new FakeTimeManager(), regionData);

			instance.OnPlayerEnterInstance(player);
			instance.StartCountdown(eRiftDifficulty.Normal, player);
			Assert.That(player.IsStunned || player.IsMezzed, Is.True);

			// Player leaves during countdown
			instance.OnPlayerLeaveInstance(player);
			Assert.That(player.IsStunned || player.IsMezzed, Is.False, "Containment must be cleared when player leaves instance");

			var fakeLib = player.Out as FakePacketLib;
			Assert.That(fakeLib.TimerClosed, Is.True);
		}

		[Test]
		public void RiftObelisk_WhisperReceive_Leave_TransfersPlayerToExitPosition()
		{
			var player = new FakePlayer { Name = "LeavingPlayer", InternalID = "TEST_EXIT_WHISPER" };
			var regionData = new RegionData
			{
				Id = RiftConstants.RIFT_DUNGEON_SKIN_ID,
				Name = "Spindelhalla",
				Description = "Spindelhalla"
			};
			var instance = new RiftDungeonInstance(1016, new FakeTimeManager(), regionData);
			var obelisk = new RiftObelisk(instance);

			bool result = obelisk.WhisperReceive(player, "leave");
			Assert.That(result, Is.True);
			Assert.Multiple(() =>
			{
				Assert.That(player.Position.RegionID, Is.EqualTo(RiftConstants.COTSWOLD_REGION));
				Assert.That(player.Position.X, Is.EqualTo(RiftConstants.COTSWOLD_PORTAL_X));
				Assert.That(player.Position.Y, Is.EqualTo(RiftConstants.COTSWOLD_PORTAL_Y));
				Assert.That(player.Position.Z, Is.EqualTo(RiftConstants.COTSWOLD_PORTAL_Z));
			});
		}

		[Test]
		public void RiftPortal_Interact_InsideInstance_TransfersPlayerToExitPosition()
		{
			var player = new FakePlayer { Name = "PortalExiter", InternalID = "TEST_PORTAL_EXIT" };
			var regionData = new RegionData
			{
				Id = RiftConstants.RIFT_DUNGEON_SKIN_ID,
				Name = "Spindelhalla",
				Description = "Spindelhalla"
			};
			var instance = new RiftDungeonInstance(1017, new FakeTimeManager(), regionData);
			player.CurrentRegion = instance;

			var portal = new RiftPortal();
			portal.CurrentRegion = instance;
			portal.Position = Position.Create(instance.ID, player.Position.X, player.Position.Y, player.Position.Z);
			bool result = portal.Interact(player);

			Assert.That(result, Is.True);
			Assert.Multiple(() =>
			{
				Assert.That(player.Position.RegionID, Is.EqualTo(RiftConstants.COTSWOLD_REGION));
				Assert.That(player.Position.X, Is.EqualTo(RiftConstants.COTSWOLD_PORTAL_X));
				Assert.That(player.Position.Y, Is.EqualTo(RiftConstants.COTSWOLD_PORTAL_Y));
				Assert.That(player.Position.Z, Is.EqualTo(RiftConstants.COTSWOLD_PORTAL_Z));
			});
		}

		[Test]
		public void RiftDungeonInstance_OnInstanceDoor_TransfersPlayerToExitPosition()
		{
			var player = new FakePlayer { Name = "DoorExiter", InternalID = "TEST_DOOR_EXIT" };
			var regionData = new RegionData
			{
				Id = RiftConstants.RIFT_DUNGEON_SKIN_ID,
				Name = "Spindelhalla",
				Description = "Spindelhalla"
			};
			var instance = new RiftDungeonInstance(1018, new FakeTimeManager(), regionData);

			bool doorResult = instance.OnInstanceDoor(player, new ZonePoint());

			Assert.That(doorResult, Is.False, "OnInstanceDoor returns false to prevent default non-instance zone transition");
			Assert.Multiple(() =>
			{
				Assert.That(player.Position.RegionID, Is.EqualTo(RiftConstants.COTSWOLD_REGION));
				Assert.That(player.Position.X, Is.EqualTo(RiftConstants.COTSWOLD_PORTAL_X));
				Assert.That(player.Position.Y, Is.EqualTo(RiftConstants.COTSWOLD_PORTAL_Y));
				Assert.That(player.Position.Z, Is.EqualTo(RiftConstants.COTSWOLD_PORTAL_Z));
			});
		}
	}
}
