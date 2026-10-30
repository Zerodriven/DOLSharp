/*
 * DAWN OF LIGHT - Diablo 3 Style Rift Dungeons Proof of Concept
 */

using System;
using System.Collections.Concurrent;
using System.Reflection;
using log4net;
using DOL.Events;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
	/// <summary>
	/// Central manager for the Rift Dungeon lifecycle, Cotswold portal spawning, and private instance routing.
	/// </summary>
	public static class RiftManager
	{
		private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private static readonly ConcurrentDictionary<string, RiftDungeonInstance> m_soloInstances = new ConcurrentDictionary<string, RiftDungeonInstance>();
		private static readonly ConcurrentDictionary<Group, RiftDungeonInstance> m_groupInstances = new ConcurrentDictionary<Group, RiftDungeonInstance>();
		private static RiftPortal m_cotswoldPortal;

		[GameServerStartedEvent]
		public static void OnServerStarted(DOLEvent e, object sender, EventArgs args)
		{
			SpawnCotswoldPortal();
		}

		[ScriptLoadedEvent]
		public static void OnScriptLoaded(DOLEvent e, object sender, EventArgs args)
		{
			SpawnCotswoldPortal();
		}

		[GameServerStoppedEvent]
		public static void OnServerStopped(DOLEvent e, object sender, EventArgs args)
		{
			RemoveCotswoldPortal();
		}

		/// <summary>
		/// Spawns the static Rift Portal in Cotswold in front of the bindstone and bridge.
		/// </summary>
		public static void SpawnCotswoldPortal()
		{
			try
			{
				if (m_cotswoldPortal != null && m_cotswoldPortal.ObjectState == GameObject.eObjectState.Active)
					return;

				Region cotswoldRegion = WorldMgr.GetRegion(RiftConstants.COTSWOLD_REGION);
				if (cotswoldRegion == null)
				{
					if (log.IsWarnEnabled)
						log.WarnFormat("RiftManager: Cotswold region {0} not loaded yet.", RiftConstants.COTSWOLD_REGION);
					return;
				}

				m_cotswoldPortal = new RiftPortal();
				m_cotswoldPortal.Position = Position.Create(
					regionID: RiftConstants.COTSWOLD_REGION,
					x: RiftConstants.COTSWOLD_PORTAL_X,
					y: RiftConstants.COTSWOLD_PORTAL_Y,
					z: RiftConstants.COTSWOLD_PORTAL_Z,
					heading: RiftConstants.COTSWOLD_PORTAL_HEADING);

				if (m_cotswoldPortal.AddToWorld())
				{
					if (log.IsInfoEnabled)
						log.InfoFormat("Rift Portal spawned in Cotswold at X:{0}, Y:{1}, Z:{2}, Heading:{3}",
							m_cotswoldPortal.Position.X, m_cotswoldPortal.Position.Y, m_cotswoldPortal.Position.Z, m_cotswoldPortal.Orientation.InHeading);
				}
				else
				{
					log.Error("RiftManager: Failed to add Cotswold Rift Portal to world.");
				}
			}
			catch (Exception ex)
			{
				log.Error("RiftManager: Exception spawning Cotswold Rift Portal", ex);
			}
		}

		/// <summary>
		/// Cleans up the portal upon server shutdown.
		/// </summary>
		public static void RemoveCotswoldPortal()
		{
			try
			{
				if (m_cotswoldPortal != null)
				{
					m_cotswoldPortal.RemoveFromWorld();
					m_cotswoldPortal = null;
				}
			}
			catch (Exception ex)
			{
				log.Error("RiftManager: Exception removing Cotswold Rift Portal", ex);
			}
		}

		/// <summary>
		/// Handles a player (solo or group) interacting with the Rift Portal.
		/// </summary>
		public static bool EnterOrOpenRift(GamePlayer player, RiftPortal portal)
		{
			if (player == null)
				return false;

			if (player.InCombat)
			{
				player.Out.SendMessage("You cannot enter a Rift while in combat!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
				return false;
			}

			// --- Group Handling ---
			if (player.Group != null)
			{
				Group group = player.Group;

				// 1. Check if group already has an active instance
				if (m_groupInstances.TryGetValue(group, out var activeGroupInstance) &&
				    activeGroupInstance != null &&
				    activeGroupInstance.IsActive)
				{
					player.Out.SendMessage("Entering your group's active Rift Dungeon...", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
					return player.MoveTo(activeGroupInstance.EntrancePosition);
				}

				// 2. Only group leader can open a brand new instance
				if (player != group.Leader)
				{
					player.Out.SendMessage("Only your group leader can open a new Rift Dungeon.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
					return false;
				}

				// 3. Create private instance for the group
				var newGroupInstance = (RiftDungeonInstance)WorldMgr.CreateInstance(RiftConstants.RIFT_DUNGEON_SKIN_ID, typeof(RiftDungeonInstance));
				if (newGroupInstance == null)
				{
					player.Out.SendMessage("Failed to create Rift Dungeon instance. Please try again.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
					return false;
				}

				newGroupInstance.OwnerPlayer = player;
				newGroupInstance.OwnerPlayerId = player.InternalID ?? player.Name;
				newGroupInstance.OwnerGroup = group;
				m_groupInstances[group] = newGroupInstance;

				player.Out.SendMessage("Opening private group Rift Dungeon...", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				player.MoveTo(newGroupInstance.EntrancePosition);

				// Teleport nearby group members in Cotswold
				foreach (GamePlayer member in group.GetPlayersInTheGroup())
				{
					if (member != player && member.CurrentRegionID == RiftConstants.COTSWOLD_REGION)
					{
						if (portal == null || member.IsWithinRadius(portal, 2500))
						{
							member.Out.SendMessage("Your group leader opened a Rift Dungeon! Teleporting...", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
							member.MoveTo(newGroupInstance.EntrancePosition);
						}
						else
						{
							member.Out.SendMessage("Your group leader opened a Rift Dungeon! Approach the Rift Portal in Cotswold to enter.", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
						}
					}
				}

				return true;
			}

			// --- Solo Handling ---
			string playerKey = player.InternalID ?? player.Name;

			// 1. Check if player has an active instance
			if (m_soloInstances.TryGetValue(playerKey, out var activeSoloInstance) &&
			    activeSoloInstance != null &&
			    activeSoloInstance.IsActive)
			{
				player.Out.SendMessage("Re-entering your active Rift Dungeon...", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				return player.MoveTo(activeSoloInstance.EntrancePosition);
			}

			// 2. Create private instance for solo player
			var newSoloInstance = (RiftDungeonInstance)WorldMgr.CreateInstance(RiftConstants.RIFT_DUNGEON_SKIN_ID, typeof(RiftDungeonInstance));
			if (newSoloInstance == null)
			{
				player.Out.SendMessage("Failed to create Rift Dungeon instance. Please try again.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
				return false;
			}

			newSoloInstance.OwnerPlayer = player;
			newSoloInstance.OwnerPlayerId = playerKey;
			m_soloInstances[playerKey] = newSoloInstance;

			player.Out.SendMessage("Opening private Rift Dungeon...", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
			return player.MoveTo(newSoloInstance.EntrancePosition);
		}

		/// <summary>
		/// Unregisters a dungeon instance when it collapses or cleans up.
		/// </summary>
		public static void UnregisterInstance(RiftDungeonInstance instance)
		{
			if (instance == null)
				return;

			if (instance.OwnerGroup != null)
			{
				m_groupInstances.TryRemove(instance.OwnerGroup, out _);
			}

			if (!string.IsNullOrEmpty(instance.OwnerPlayerId))
			{
				m_soloInstances.TryRemove(instance.OwnerPlayerId, out _);
			}
		}

		/// <summary>
		/// Gets the active instance for a player (group or solo), or null.
		/// </summary>
		public static RiftDungeonInstance GetActiveInstance(GamePlayer player)
		{
			if (player == null)
				return null;

			if (player.Group != null && m_groupInstances.TryGetValue(player.Group, out var grpInst))
				return grpInst;

			string key = player.InternalID ?? player.Name;
			if (m_soloInstances.TryGetValue(key, out var soloInst))
				return soloInst;

			return null;
		}
	}
}
