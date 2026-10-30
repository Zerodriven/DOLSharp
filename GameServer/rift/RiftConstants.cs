/*
 * DAWN OF LIGHT - Diablo 3 Style Rift Dungeons Proof of Concept
 */

namespace DOL.GS
{
	/// <summary>
	/// Central configuration and constants for the Diablo 3 style Rift Dungeons system.
	/// </summary>
	public static class RiftConstants
	{
		/// <summary>
		/// Whether all newly created characters across all realms should spawn on the Cotswold bridge.
		/// </summary>
		public static bool ROUTE_ALL_NEW_CHARS_TO_COTSWOLD = true;

		// --- Cotswold Bridge Spawn Location ---
		// Exact location on Cotswold bridge facing west toward town center and bindstone:
		public const ushort COTSWOLD_REGION = 1;
		public const int COTSWOLD_BRIDGE_X = 558074;
		public const int COTSWOLD_BRIDGE_Y = 511741;
		public const int COTSWOLD_BRIDGE_Z = 2336;
		public const ushort COTSWOLD_BRIDGE_HEADING = 3062;

		// --- Cotswold Rift Portal Location (Ahead of the bridge facing back) ---
		public const int COTSWOLD_PORTAL_X = 558574;
		public const int COTSWOLD_PORTAL_Y = 511741;
		public const int COTSWOLD_PORTAL_Z = 2336;
		public const ushort COTSWOLD_PORTAL_HEADING = 1024; // Facing West toward the bridge
		public const ushort COTSWOLD_PORTAL_MODEL = 2603;   // Monolithic stone portal

		// --- Rift Dungeon (Spindelhalla) ---
		public const ushort RIFT_DUNGEON_SKIN_ID = 125;
		public const int DUNGEON_ENTRANCE_X = 32152;
		public const int DUNGEON_ENTRANCE_Y = 31841;
		public const int DUNGEON_ENTRANCE_Z = 16375;
		public const ushort DUNGEON_ENTRANCE_HEADING = 90;

		// --- Rift Obelisk & Countdown (Inside Dungeon Entrance Room) ---
		// The authentic Spindelhalla entrance teleport lands at (32151, 31813, 16371).
		// Keep the obelisk a few steps in front of that point, on the same floor level,
		// so it is in plain view (the old X:32250 placement ended up outside the room).
		public const int DUNGEON_OBELISK_X = 32201;
		public const int DUNGEON_OBELISK_Y = 31813;
		public const int DUNGEON_OBELISK_Z = 16371;
		public const ushort DUNGEON_OBELISK_HEADING = 1024; // Facing west, toward the arriving player
		public const ushort DUNGEON_OBELISK_MODEL = 2603; // Monolithic obelisk stone
		public const ushort CONTAINMENT_SPELL_EFFECT = 7036; // Golden containment sphere visual
		public const ushort START_SOUND_EFFECT = 11;
		public const int COUNTDOWN_SECONDS = 10;

		// Type and name of the crowd-control spell used to hold players during the countdown.
		// It is applied through the real spell handler so the client receives the same
		// state and visuals as a normal mez/stun.
		public const int CONTAINMENT_STUN_SPELL_ID = 3629; // "Stunning Shout" (Mesmerize, 11s, instant)
		public const string CONTAINMENT_STUN_SPELL_NAME = "Stunning Shout";
		public const string CONTAINMENT_STUN_SPELL_TYPE = "Mesmerize";
		public const ushort CONTAINMENT_STUN_TOOLTIP_ID = 804; // Tooltip of spell 3629, so client delves resolve
		public const string CONTAINMENT_HOLD_PROPERTY = "RIFT_CONTAINMENT_HOLD";

		// --- Rift Dungeon Exit Portal (Inside Dungeon Entrance Room) ---
		public const int DUNGEON_EXIT_PORTAL_X = 31992;
		public const int DUNGEON_EXIT_PORTAL_Y = 31844;
		public const int DUNGEON_EXIT_PORTAL_Z = 16371;
		public const ushort DUNGEON_EXIT_PORTAL_HEADING = 90;
	}

	/// <summary>
	/// Tiered difficulty options for Diablo 3 style Rift Dungeons.
	/// </summary>
	public enum eRiftDifficulty : int
	{
		None = 0,
		Easy = 1,
		Normal = 2,
		Hard = 3,
		Torment = 4,
	}
}
