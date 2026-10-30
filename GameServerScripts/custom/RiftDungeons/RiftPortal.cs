/*
 * DAWN OF LIGHT - Diablo 3 Style Rift Dungeons Proof of Concept
 */

using System.Reflection;
using log4net;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
	/// <summary>
	/// Interactive Rift Portal placed in Cotswold. Clicking teleports players into their private Rift Dungeon instance.
	/// </summary>
	public class RiftPortal : GameStaticItem
	{
		private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public override ushort Model => RiftConstants.COTSWOLD_PORTAL_MODEL;

		public RiftPortal() : base()
		{
			LoadedFromScript = true;
			Name = "Rift Portal";
		}

		public RiftPortal(string name) : this()
		{
			if (!string.IsNullOrWhiteSpace(name))
				Name = name;
		}

		public override bool Interact(GamePlayer player)
		{
			if (player == null)
				return false;

			if (log.IsInfoEnabled)
				log.InfoFormat("RiftPortal '{0}' OID {1} interacted by {2} (region {3}, target '{4}' OID {5})",
					Name, ObjectID, player.Name, player.CurrentRegionID, player.TargetObject?.Name ?? "null", player.TargetObject?.ObjectID ?? -1);

			if (player.CurrentRegion is RiftDungeonInstance riftInstance)
			{
				player.Out.SendMessage("Returning to Cotswold...", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				return player.MoveTo(riftInstance.ExitPosition);
			}

			if (!base.Interact(player))
				return false;

			return RiftManager.EnterOrOpenRift(player, this);
		}
	}
}
