/*
 * DAWN OF LIGHT - Diablo 3 Style Rift Dungeons Proof of Concept
 */

using System;
using System.Reflection;
using log4net;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
	/// <summary>
	/// Interactive Rift Obelisk placed inside the instance entrance.
	/// Party leaders select difficulty, triggering group warnings, containment hold, and 10s countdown.
	/// </summary>
	/// <remarks>
	/// This intentionally derives from <see cref="GameStaticItem"/> instead of <see cref="GameNPC"/>:
	/// the 1.130 client does not reliably send an ObjectInteractRequest for an NPC that uses a
	/// static stone model, whereas static items (the same base as <see cref="RiftPortal"/>) are
	/// always right-clickable. Because a static item is not a GameLiving, whisper responses are
	/// routed through <see cref="IWhisperTarget"/>.
	/// </remarks>
	public class RiftObelisk : GameStaticItem, IWhisperTarget
	{
		private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public RiftDungeonInstance DungeonInstance { get; set; }

		public override string Name => "Rift Obelisk";

		public override ushort Model => RiftConstants.DUNGEON_OBELISK_MODEL;

		public RiftObelisk() : base()
		{
			LoadedFromScript = true;
			Level = 50;
		}

		public RiftObelisk(RiftDungeonInstance instance) : this()
		{
			DungeonInstance = instance;
		}

		public override bool Interact(GamePlayer player)
		{
			if (player == null)
				return false;

			var instance = ResolveInstance(player);
			if (instance == null || !instance.IsActive)
			{
				if (log.IsInfoEnabled)
					log.InfoFormat("RiftObelisk OID {0} interacted by {1} but no active instance was resolved (region {2})",
						ObjectID, player.Name, player.CurrentRegionID);
				return false;
			}

			if (log.IsInfoEnabled)
				log.InfoFormat("RiftObelisk OID {0} interacted by {1} in instance {2} (player at {3}, distance {4}, target '{5}' OID {6})",
					ObjectID, player.Name, instance.ID, player.Position, player.GetDistanceTo(this, 1), player.TargetObject?.Name ?? "null", player.TargetObject?.ObjectID ?? -1);

			if (!base.Interact(player))
			{
				if (log.IsInfoEnabled)
					log.InfoFormat("RiftObelisk OID {0} base.Interact rejected for {1}", ObjectID, player.Name);
				return false;
			}

			// Keep the obelisk as the player's target: clicking a popup link sends a
			// whisper to the current target, and /whisper resolves TargetObject.
			player.TargetObject = this;

			if (instance.IsRiftActive)
			{
				player.Out.SendMessage("The Rift is already active and cleared for battle!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
				return false;
			}

			if (instance.IsCountdownRunning)
			{
				player.Out.SendMessage("The Rift countdown is already running!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
				return false;
			}

			// Leadership check for groups
			if (player.Group != null && player != player.Group.Leader)
			{
				player.Out.SendMessage("Only the group leader can activate the Rift Obelisk!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
				return false;
			}

			// Required party preparation warning
			player.Out.SendMessage("Rift Obelisk: Make sure you're all buffed up before activating!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);

			SendChoiceMenu(player);

			return true;
		}

		/// <summary>
		/// Sends the difficulty menu in the NPC-style popup window, using the same
		/// bracketed-link pattern as the teleporter NPCs (for example
		/// <see cref="SimpleTeleporter"/>). Clicking a choice whispers it back to the obelisk.
		/// </summary>
		private static void SendChoiceMenu(GamePlayer player)
		{
			player.Out.SendMessage(
				"Rift Obelisk says, \"Choose your Rift difficulty, but make sure you're all buffed up first!\"",
				eChatType.CT_System, eChatLoc.CL_PopupWindow);

			player.Out.SendMessage(
				"Choose Rift Difficulty: [Easy] [Normal] [Hard] [Torment]",
				eChatType.CT_Say, eChatLoc.CL_PopupWindow);

			player.Out.SendMessage("[Exit to Cotswold]", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
		}

		private RiftDungeonInstance ResolveInstance(GamePlayer player)
		{
			return DungeonInstance ?? (player.CurrentRegion as RiftDungeonInstance);
		}

		/// <summary>
		/// Handles the clickable dialogue choices. Whisper links are delivered here through
		/// <see cref="IWhisperTarget"/> because this object is not a GameLiving.
		/// </summary>
		public bool WhisperReceive(GameLiving source, string text)
		{
			GamePlayer player = source as GamePlayer;
			if (player == null)
				return false;

			var instance = ResolveInstance(player);
			if (instance == null || !instance.IsActive)
				return false;

			string choice = text?.Trim().ToLowerInvariant();

			if (log.IsInfoEnabled)
				log.InfoFormat("RiftObelisk OID {0} received whisper '{1}' from {2} (target '{3}' OID {4})",
					ObjectID, choice, player.Name, player.TargetObject?.Name ?? "null", player.TargetObject?.ObjectID ?? -1);

			if (choice == "leave" || choice == "exit" || choice == "exit to cotswold" || choice == "cotswold")
			{
				player.Out.SendMessage("Returning to Cotswold...", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				return player.MoveTo(instance.ExitPosition);
			}

			if (player.Group != null && player != player.Group.Leader)
			{
				player.Out.SendMessage("Only the group leader can activate the Rift Obelisk!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
				return false;
			}

			if (instance.IsRiftActive || instance.IsCountdownRunning)
			{
				player.Out.SendMessage("The Rift has already been activated!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
				return false;
			}

			eRiftDifficulty diff;

			if (choice == "easy")
				diff = eRiftDifficulty.Easy;
			else if (choice == "normal")
				diff = eRiftDifficulty.Normal;
			else if (choice == "hard")
				diff = eRiftDifficulty.Hard;
			else if (choice == "torment")
				diff = eRiftDifficulty.Torment;
			else
			{
				// Not a recognised choice: show the menu again so the player can retry.
				SendChoiceMenu(player);
				return false;
			}

			if (log.IsInfoEnabled)
				log.InfoFormat("Player {0} selected Rift difficulty {1} in instance {2}", player.Name, diff, instance.ID);

			instance.StartCountdown(diff, player);
			return true;
		}
	}
}
