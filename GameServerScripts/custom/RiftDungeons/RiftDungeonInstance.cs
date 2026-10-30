/*
 * DAWN OF LIGHT - Diablo 3 Style Rift Dungeons Proof of Concept
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using DOL.Database;
using DOL.GS.Effects;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;

namespace DOL.GS
{
	/// <summary>
	/// Represents an isolated, private dungeon instance (Spindelhalla) for Diablo 3 style Rift adventures.
	/// </summary>
	public class RiftDungeonInstance : RegionInstance
	{
		private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly object m_playerLock = new object();
		private readonly List<GamePlayer> m_playersInInstance = new List<GamePlayer>();
		private readonly Dictionary<GamePlayer, GameSpellEffect> m_containmentEffects = new Dictionary<GamePlayer, GameSpellEffect>();
		private readonly Dictionary<GamePlayer, Position> m_containmentAnchors = new Dictionary<GamePlayer, Position>();
		private readonly List<GameNPC> m_hiddenDungeonMobs = new List<GameNPC>();
		private RegionTimer m_countdownTimer;
		private int m_countdownRemaining;

		/// <summary>
		/// The player who initiated this rift dungeon.
		/// </summary>
		public GamePlayer OwnerPlayer { get; set; }

		/// <summary>
		/// Stable identifier for the owner (Account or InternalID).
		/// </summary>
		public string OwnerPlayerId { get; set; }

		/// <summary>
		/// The group bound to this rift dungeon, if any.
		/// </summary>
		public Group OwnerGroup { get; set; }

		/// <summary>
		/// Entrance coordinate inside the dungeon instance.
		/// </summary>
		public Position EntrancePosition { get; set; }

		/// <summary>
		/// Exit coordinate in Cotswold.
		/// </summary>
		public Position ExitPosition { get; set; }

		/// <summary>
		/// The exit portal spawned inside the dungeon entrance chamber.
		/// </summary>
		public RiftPortal ExitPortal { get; private set; }

		/// <summary>
		/// Whether this instance is active and accessible.
		/// </summary>
		public bool IsActive { get; private set; } = true;

		/// <summary>
		/// Whether the dungeon run has been completed.
		/// </summary>
		public bool IsCompleted { get; set; }

		/// <summary>
		/// Selected difficulty tier for this run.
		/// </summary>
		public eRiftDifficulty Difficulty { get; private set; } = eRiftDifficulty.None;

		/// <summary>
		/// Whether the 10-second opening countdown is currently active.
		/// </summary>
		public bool IsCountdownRunning { get; private set; } = false;

		/// <summary>
		/// Whether the countdown finished and the dungeon combat is fully active.
		/// </summary>
		public bool IsRiftActive { get; private set; } = false;

		/// <summary>
		/// The interactive difficulty selector obelisk placed in the entrance room.
		/// </summary>
		public RiftObelisk Obelisk { get; private set; }

		/// <summary>
		/// Instance constructor required by WorldMgr.CreateInstance.
		/// </summary>
		public RiftDungeonInstance(ushort ID, GameTimer.TimeManager time, RegionData data)
			: base(ID, time, data)
		{
			EntrancePosition = Position.Create(
				regionID: ID,
				x: RiftConstants.DUNGEON_ENTRANCE_X,
				y: RiftConstants.DUNGEON_ENTRANCE_Y,
				z: RiftConstants.DUNGEON_ENTRANCE_Z,
				heading: RiftConstants.DUNGEON_ENTRANCE_HEADING);

			ExitPosition = Position.Create(
				regionID: RiftConstants.COTSWOLD_REGION,
				x: RiftConstants.COTSWOLD_PORTAL_X,
				y: RiftConstants.COTSWOLD_PORTAL_Y,
				z: RiftConstants.COTSWOLD_PORTAL_Z,
				heading: RiftConstants.COTSWOLD_BRIDGE_HEADING);

			if (log.IsInfoEnabled)
				log.InfoFormat("RiftDungeonInstance created: RegionID {0}, SkinID {1} (Spindelhalla)", ID, Skin);
		}

		public override void Start()
		{
			base.Start();
			SpawnObelisk();
			SpawnExitPortal();
			LoadDungeonMobs();

			// Keep the dungeon empty until the rift is actually opened.
			HideDungeonMobs();
		}

		/// <summary>
		/// Removes all cloned dungeon mobs from the world so nothing is visible until
		/// the countdown completes. The instances are kept and re-added on reveal.
		/// </summary>
		private void HideDungeonMobs()
		{
			m_hiddenDungeonMobs.Clear();

			lock (ObjectsSyncLock)
			{
				foreach (GameObject obj in Objects)
				{
					if (obj is GameNPC npc && npc.ObjectState == GameObject.eObjectState.Active)
						m_hiddenDungeonMobs.Add(npc);
				}
			}

			foreach (GameNPC npc in m_hiddenDungeonMobs)
				npc.RemoveFromWorld();

			if (log.IsInfoEnabled)
				log.InfoFormat("RiftDungeonInstance {0}: {1} mobs hidden until the rift opens", ID, m_hiddenDungeonMobs.Count);
		}

		/// <summary>
		/// Adds the hidden dungeon mobs back to the world when the rift opens. Only mobs
		/// near a player generate create packets, so this is the same cost as the initial
		/// load, just deferred.
		/// </summary>
		private void RevealDungeonMobs()
		{
			if (m_hiddenDungeonMobs.Count == 0)
				return;

			int revealed = 0;

			foreach (GameNPC npc in m_hiddenDungeonMobs)
			{
				if (npc == null)
					continue;

				if (npc.AddToWorld())
					revealed++;
			}

			if (log.IsInfoEnabled)
				log.InfoFormat("RiftDungeonInstance {0}: {1} mobs revealed as the rift opened", ID, revealed);

			m_hiddenDungeonMobs.Clear();
		}

		/// <summary>
		/// Spawns the exit portal near the dungeon entrance to return to Cotswold.
		/// </summary>
		public void SpawnExitPortal()
		{
			if (ExitPortal != null)
				return;

			try
			{
				ExitPortal = new RiftPortal("Rift Exit Portal")
				{
					Position = Position.Create(
						regionID: ID,
						x: RiftConstants.DUNGEON_EXIT_PORTAL_X,
						y: RiftConstants.DUNGEON_EXIT_PORTAL_Y,
						z: RiftConstants.DUNGEON_EXIT_PORTAL_Z,
						heading: RiftConstants.DUNGEON_EXIT_PORTAL_HEADING)
				};

				if (ExitPortal.AddToWorld())
				{
					if (log.IsInfoEnabled)
						log.InfoFormat("Exit Portal '{0}' spawned in instance {1} at X:{2}, Y:{3}, Z:{4} OID:{5}",
							ExitPortal.Name, ID, ExitPortal.Position.X, ExitPortal.Position.Y, ExitPortal.Position.Z, ExitPortal.ObjectID);
				}
				else
				{
					log.ErrorFormat("Failed to add Exit Portal to instance {0}", ID);
				}
			}
			catch (Exception ex)
			{
				log.Error("Exception spawning Exit Portal in instance " + ID, ex);
			}
		}

		/// <summary>
		/// Clones and loads all authentic dungeon mobs from the source region (Spindelhalla).
		/// </summary>
		public void LoadDungeonMobs()
		{
			try
			{
				if (RegionData?.Mobs != null && RegionData.Mobs.Length > 0)
				{
					long mobCount = 0;
					long merchantCount = 0;
					long itemCount = 0;
					long bindCount = 0;

					LoadFromDatabase(RegionData.Mobs, ref mobCount, ref merchantCount, ref itemCount, ref bindCount);

					if (log.IsInfoEnabled)
						log.InfoFormat("RiftDungeonInstance {0} loaded {1} mobs and {2} items from Spindelhalla (Region {3}).",
							ID, mobCount, itemCount, Skin);
				}
			}
			catch (Exception ex)
			{
				log.Error("Exception loading dungeon mobs in Rift instance " + ID, ex);
			}
		}

		/// <summary>
		/// Spawns the interactive Rift Obelisk in the entrance chamber.
		/// </summary>
		public void SpawnObelisk()
		{
			if (Obelisk != null)
				return;

			try
			{
				Obelisk = new RiftObelisk(this)
				{
					Position = Position.Create(
						regionID: ID,
						x: RiftConstants.DUNGEON_OBELISK_X,
						y: RiftConstants.DUNGEON_OBELISK_Y,
						z: RiftConstants.DUNGEON_OBELISK_Z,
						heading: RiftConstants.DUNGEON_OBELISK_HEADING)
				};

				if (Obelisk.AddToWorld())
				{
					if (log.IsInfoEnabled)
						log.InfoFormat("Rift Obelisk spawned in instance {0} at X:{1}, Y:{2}, Z:{3} OID:{4}",
							ID, Obelisk.Position.X, Obelisk.Position.Y, Obelisk.Position.Z, Obelisk.ObjectID);
				}
				else
				{
					log.ErrorFormat("Failed to add Rift Obelisk to instance {0}", ID);
				}
			}
			catch (Exception ex)
			{
				log.Error("Exception spawning Rift Obelisk in instance " + ID, ex);
			}
		}

		/// <summary>
		/// Returns a thread-safe snapshot of all players currently inside this instance.
		/// </summary>
		public List<GamePlayer> GetPlayersInInstance()
		{
			lock (m_playerLock)
			{
				return new List<GamePlayer>(m_playersInInstance);
			}
		}

		/// <summary>
		/// Starts the 10-second opening countdown with containment hold, visual orb, and group warning.
		/// The native timer window is sent as well, but the 1.130 client does not render it reliably,
		/// so the countdown is also broadcast to the screen center every second.
		/// </summary>
		public void StartCountdown(eRiftDifficulty difficulty, GamePlayer initiator)
		{
			if (IsCountdownRunning || IsRiftActive)
				return;

			IsCountdownRunning = true;
			Difficulty = difficulty;
			m_countdownRemaining = RiftConstants.COUNTDOWN_SECONDS;

			List<GamePlayer> players = GetPlayersInInstance();

			// Fallback: If player list is empty (e.g. mock test without region registration), include initiator
			if (players.Count == 0 && initiator != null)
				players.Add(initiator);

			foreach (GamePlayer player in players)
			{
				if (player == null)
					continue;

				// Prominent group warning (single screen-center message)
				player.Out.SendMessage("WARNING: Make sure you're all buffed up! Rift opening in 10 seconds!", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
				player.Out.SendMessage(string.Format("The Rift has been set to [{0}] difficulty! Make sure you're all buffed up!", difficulty), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				player.Out.SendMessage("Rift opening in 10...", eChatType.CT_System, eChatLoc.CL_SystemWindow);

				// Apply containment hold through the game's stun mechanic so the client
				// receives the stun state and refuses to move.
				ApplyContainmentHold(player, RiftConstants.COUNTDOWN_SECONDS);

				// Golden containment orb visual animation
				player.Out.SendSpellEffectAnimation(player, player, RiftConstants.CONTAINMENT_SPELL_EFFECT, 0, false, 1);
			}

			if (log.IsInfoEnabled)
				log.InfoFormat("RiftDungeonInstance {0}: countdown started at {1} difficulty ({2}s)", ID, difficulty, m_countdownRemaining);

			// One-second ticker drives both the visible countdown and the completion.
			if (TimeManager != null)
			{
				m_countdownTimer = new RegionTimer(TimeManager)
				{
					Callback = new RegionTimerCallback(OnCountdownTick)
				};
				m_countdownTimer.Start(1000);
			}
			else
			{
				OnCountdownComplete(null);
			}
		}

		/// <summary>
		/// One-second countdown tick. Broadcasts the remaining seconds to the screen center
		/// and completes the countdown when it reaches zero.
		/// </summary>
		public int OnCountdownTick(RegionTimer timer)
		{
			if (!IsCountdownRunning)
				return 0;

			m_countdownRemaining--;

			if (m_countdownRemaining <= 0)
			{
				OnCountdownComplete(timer);
				return 0;
			}

			foreach (GamePlayer player in GetPlayersInInstance())
			{
				if (player == null)
					continue;

				// Backstop: if the client still moved, pull the player back to the
				// containment anchor before sending the next countdown message.
				Position anchor;
				bool hasAnchor;

				lock (m_containmentEffects)
				{
					hasAnchor = m_containmentAnchors.TryGetValue(player, out anchor);
				}

				// Re-assert speed 0 and the anchor position every tick. The 1.130 client
				// does not always lock movement from the one-shot CC packets.
				if (!player.IsStunned && !player.IsMezzed)
					player.IsStunned = true;

				player.StopAttack();
				player.Out.SendUpdateMaxSpeed();
				player.Out.SendPlayerPositionAndObjectID();

				if (hasAnchor && player.Position.RegionID == anchor.RegionID &&
					player.Coordinate.DistanceTo(anchor.Coordinate) > 8)
				{
					player.MoveTo(anchor);
				}

				// Chat/system window, not screen-center: screen-center messages fade/queue
				// on the client, so a ten-message screen-center sequence visibly runs
				// longer than the actual ten-second mez.
				player.Out.SendMessage(string.Format("Rift opening in {0}...", m_countdownRemaining), eChatType.CT_System, eChatLoc.CL_SystemWindow);

				if (log.IsInfoEnabled)
					log.InfoFormat("RiftDungeonInstance {0}: countdown {1}, {2} stunned={3} mezzed={4} held={5} maxSpeed={6} pos={7}",
						ID, m_countdownRemaining, player.Name, player.IsStunned, player.IsMezzed,
						player.TempProperties.getProperty<bool>(RiftConstants.CONTAINMENT_HOLD_PROPERTY, false),
						player.MaxSpeed, player.Position);
			}

			if (log.IsDebugEnabled)
				log.DebugFormat("RiftDungeonInstance {0}: countdown {1}", ID, m_countdownRemaining);

			return 1000; // Keep ticking
		}

		/// <summary>
		/// Callback executed when the 10-second countdown expires.
		/// </summary>
		public int OnCountdownComplete(RegionTimer timer)
		{
			IsCountdownRunning = false;
			IsRiftActive = true;
			m_countdownRemaining = 0;

			List<GamePlayer> players = GetPlayersInInstance();
			foreach (GamePlayer player in players)
			{
				if (player == null)
					continue;

				// Release containment stun
				ReleaseContainmentHold(player);

				// Close native timer window
				player.Out.SendCloseTimerWindow();

				// Play combat horn / start sound
				player.Out.SendSoundEffect(RiftConstants.START_SOUND_EFFECT, Position.Zero, 0);

				// Start fanfare notifications
				player.Out.SendMessage("THE RIFT HAS OPENED! FIGHT!", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
				player.Out.SendMessage("Containment fields collapsed. The Rift Dungeon is now active!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
			}

			if (m_countdownTimer != null)
			{
				m_countdownTimer.Stop();
				m_countdownTimer = null;
			}

			RevealDungeonMobs();

			if (log.IsInfoEnabled)
				log.InfoFormat("RiftDungeonInstance {0}: countdown complete, rift active", ID);

			return 0; // One-shot timer
		}

		/// <summary>
		/// Applies the game's crowd-control mez to a player for the containment hold.
		/// Uses the existing "Stunning Shout" mesmerize spell (3629) so the client receives
		/// the same state and visuals as a real mez, then starts the effect directly so
		/// immunity abilities cannot bypass the scripted hold.
		/// </summary>
		private void ApplyContainmentHold(GamePlayer player, int seconds)
		{
			if (player == null || seconds <= 0)
				return;

			// Enforce the hold independently of the client's CC display: the position
			// handler checks this flag, and the countdown tick pulls the player back to
			// the anchor if the client still manages to move.
			player.TempProperties.setProperty(RiftConstants.CONTAINMENT_HOLD_PROPERTY, true);

			lock (m_containmentEffects)
			{
				m_containmentAnchors[player] = player.Position;
			}

			try
			{
				Spell spell = SkillBase.GetSpellByID(RiftConstants.CONTAINMENT_STUN_SPELL_ID);
				SpellLine line;

				if (spell != null)
				{
					line = SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells)
						?? new SpellLine("Rift Containment", "Rift Containment", "Rift Containment", true);
				}
				else
				{
					DBSpell dbSpell = new DBSpell
					{
						Name = RiftConstants.CONTAINMENT_STUN_SPELL_NAME,
						Icon = 3629, // "Stunning Shout" mez effect
						ClientEffect = 3629,
						Damage = 0,
						DamageType = (int)eDamageType.Energy,
						Target = "Self",
						Radius = 0,
						Type = RiftConstants.CONTAINMENT_STUN_SPELL_TYPE,
						Value = 0,
						Duration = seconds,
						Pulse = 0,
						PulsePower = 0,
						Power = 0,
						CastTime = 0,
						Range = 0,
						Uninterruptible = true,
						TooltipId = RiftConstants.CONTAINMENT_STUN_TOOLTIP_ID
					};

					spell = new Spell(dbSpell, 1);
					line = new SpellLine("Rift Containment", "Rift Containment", "Rift Containment", true);
				}

				if (ScriptMgr.CreateSpellHandler(player, spell, line) is SpellHandler handler)
				{
					GameSpellEffect effect = new GameSpellEffect(handler, seconds * 1000, 0, 1.0);
					effect.Start(player);

					// Force the movement lock as well; some client versions ignore the
					// speed update bundled with the effect.
					if (!player.IsStunned && !player.IsMezzed)
						player.IsStunned = true;

					player.StopAttack();
					player.Out.SendUpdateMaxSpeed();

					lock (m_containmentEffects)
					{
						m_containmentEffects[player] = effect;
					}

					// Play the same visual as the real mez spell.
					player.Out.SendSpellEffectAnimation(player, player, (ushort)spell.ClientEffect, 0, false, 1);

					if (log.IsInfoEnabled)
						log.InfoFormat("RiftDungeonInstance {0}: containment mez applied to {1} for {2}s using spell {3} '{4}' (stunned={5}, mezzed={6}, maxSpeed={7})",
							ID, player.Name, seconds, spell.ID, spell.Name, player.IsStunned, player.IsMezzed, player.MaxSpeed);

					return;
				}
			}
			catch (Exception ex)
			{
				log.Error("Failed to apply Rift containment hold to " + player.Name, ex);
			}

			// Fallback: at least stop the player from moving server-side.
			player.IsStunned = true;
			player.Out.SendUpdateMaxSpeed();
		}

		/// <summary>
		/// Releases the containment hold immediately, cancelling the stun effect if it is
		/// still active (for example when the countdown is interrupted or the player leaves).
		/// </summary>
		private void ReleaseContainmentHold(GamePlayer player)
		{
			if (player == null)
				return;

			try
			{
				GameSpellEffect effect = null;

				lock (m_containmentEffects)
				{
					if (m_containmentEffects.TryGetValue(player, out effect))
						m_containmentEffects.Remove(player);

					m_containmentAnchors.Remove(player);
				}

				if (effect != null)
				{
					effect.Cancel(false);

					// The mez handler returns a 60s immunity duration on expiry. Do not
					// leave that lingering immunity effect on the player after the hold.
					player.EffectList.Remove(effect);
				}
			}
			catch (Exception ex)
			{
				log.Error("Failed to release Rift containment hold for " + player.Name, ex);
			}

			player.TempProperties.removeProperty(RiftConstants.CONTAINMENT_HOLD_PROPERTY);
			player.IsStunned = false;
			player.IsMezzed = false;
			player.Out.SendUpdateMaxSpeed();

			if (log.IsInfoEnabled)
				log.InfoFormat("RiftDungeonInstance {0}: containment mez released for {1}", ID, player.Name);
		}

		public override void OnPlayerEnterInstance(GamePlayer player)
		{
			base.OnPlayerEnterInstance(player);

			if (player != null)
			{
				lock (m_playerLock)
				{
					if (!m_playersInInstance.Contains(player))
						m_playersInInstance.Add(player);
				}

				player.Out.SendMessage(string.Format("You enter the Rift Dungeon [{0}].", Description), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				player.Out.SendMessage("A private dungeon instance has been opened for your expedition.", eChatType.CT_System, eChatLoc.CL_SystemWindow);

				// If entering while countdown is actively ticking, apply containment
				if (IsCountdownRunning)
				{
					int remaining = Math.Max(1, m_countdownRemaining);
					ApplyContainmentHold(player, remaining);
					player.Out.SendSpellEffectAnimation(player, player, RiftConstants.CONTAINMENT_SPELL_EFFECT, 0, false, 1);
					player.Out.SendMessage(string.Format("WARNING: Make sure you're all buffed up! Rift opening in {0} seconds!", remaining), eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
				}
			}
		}

		public override void OnPlayerLeaveInstance(GamePlayer player)
		{
			base.OnPlayerLeaveInstance(player);

			if (player != null)
			{
				lock (m_playerLock)
				{
					m_playersInInstance.Remove(player);
				}

				// Clear containment stun and timer if leaving during countdown
				ReleaseContainmentHold(player);

				player.Out.SendCloseTimerWindow();

				if (log.IsInfoEnabled)
					log.InfoFormat("Player {0} left RiftDungeonInstance {1}", player.Name, ID);
			}
		}

		/// <summary>
		/// Intercepts native dungeon exit door/zonepoints to return players back to Cotswold.
		/// </summary>
		public override bool OnInstanceDoor(GamePlayer player, ZonePoint zonePoint)
		{
			if (player != null && ExitPosition.RegionID != 0)
			{
				if (log.IsInfoEnabled)
					log.InfoFormat("RiftDungeonInstance {0}: player {1} used instance door/zonepoint {2}, sending to Cotswold",
						ID, player.Name, zonePoint?.Id ?? 0);

				player.Out.SendMessage("Returning to Cotswold...", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
				player.MoveTo(ExitPosition);
				return false;
			}

			return base.OnInstanceDoor(player, zonePoint);
		}

		public override void OnCollapse()
		{
			IsActive = false;

			m_hiddenDungeonMobs.Clear();

			if (m_countdownTimer != null)
			{
				m_countdownTimer.Stop();
				m_countdownTimer = null;
			}

			// Clear stuns for anyone remaining
			foreach (GamePlayer player in GetPlayersInInstance())
			{
				ReleaseContainmentHold(player);
				player.Out.SendCloseTimerWindow();
			}

			if (Obelisk != null)
			{
				Obelisk.RemoveFromWorld();
				Obelisk = null;
			}

			if (ExitPortal != null)
			{
				ExitPortal.RemoveFromWorld();
				ExitPortal = null;
			}

			if (log.IsInfoEnabled)
				log.InfoFormat("RiftDungeonInstance {0} collapsed.", ID);

			RiftManager.UnregisterInstance(this);
			base.OnCollapse();
		}
	}
}
