/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System;
using System.IO;
using System.Text.Json;
using DOL.GS;
using NUnit.Framework;

namespace DOL.Integration.Server
{
	/// <summary>
	/// Unit tests for the Zone Class
	/// </summary>
	[TestFixture]
	public class ZoneTest : ServerTests
	{

		public ZoneTest()
		{
		}

		[Test]
		public void GetZCoordinateTest()
		{
			string daocPath = @"C:\DAoC";
			string rawPath = "terrain_162.raw";
			string roadsPath = "roads_200.json";

			int result = DOL.Tools.ZoneTerrainExtractor.Program.Main(new[] { "162", daocPath, rawPath, roadsPath });
			Assert.That(result, Is.EqualTo(0), "ZoneTerrainExtractor should succeed");
			Assert.That(File.Exists(rawPath), Is.True, "terrain_162.raw should exist");
			Assert.That(File.Exists(roadsPath), Is.True, "roads_200.json should exist");

			var fileInfo = new FileInfo(rawPath);
			Assert.That(fileInfo.Length, Is.EqualTo(256 * 256 * 2), "Raw heightmap must be exactly 131,072 bytes");

			// Verify little-endian 16-bit format
			byte[] rawBytes = File.ReadAllBytes(rawPath);
			ushort minRaw = ushort.MaxValue;
			ushort maxRaw = ushort.MinValue;
			for (int i = 0; i < rawBytes.Length; i += 2)
			{
				ushort val = (ushort)(rawBytes[i] | (rawBytes[i + 1] << 8));
				if (val < minRaw) minRaw = val;
				if (val > maxRaw) maxRaw = val;
			}

			Assert.That(minRaw, Is.EqualTo(0), "Normalized min value should be 0");
			Assert.That(maxRaw, Is.EqualTo(65535), "Normalized max value should be 65535");

			// Verify roads_200.json
			string jsonContent = File.ReadAllText(roadsPath);
			using var doc = JsonDocument.Parse(jsonContent);
			Assert.That(doc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
			Assert.That(doc.RootElement.GetArrayLength(), Is.GreaterThanOrEqualTo(1));

			foreach (var road in doc.RootElement.EnumerateArray())
			{
				Assert.That(road.TryGetProperty("roadId", out var idProp), Is.True);
				Assert.That(idProp.GetInt32(), Is.GreaterThan(0));

				Assert.That(road.TryGetProperty("width", out var widthProp), Is.True);
				Assert.That(widthProp.GetDouble(), Is.GreaterThan(0));

				Assert.That(road.TryGetProperty("points", out var pointsProp), Is.True);
				Assert.That(pointsProp.GetArrayLength(), Is.GreaterThanOrEqualTo(2));

				foreach (var pt in pointsProp.EnumerateArray())
				{
					Assert.That(pt.TryGetProperty("x", out var xProp), Is.True);
					Assert.That(pt.TryGetProperty("y", out var yProp), Is.True);
					Assert.That(pt.TryGetProperty("z", out var zProp), Is.True);

					double x = xProp.GetDouble();
					double z = zProp.GetDouble();
					Assert.That(x, Is.InRange(0.0, 1000.0), "Normalized Unity X must be within [0, 1000]");
					Assert.That(z, Is.InRange(0.0, 1000.0), "Normalized Unity Z must be within [0, 1000]");
				}
			}
		}

		[Test]
		public void GetRandomNPCTest()
		{
			Zone zone = WorldMgr.GetZone(0);
			Assert.That(zone, Is.Not.Null);

			StartWatch();
			GameNPC npc = zone.GetRandomNPC(eRealm.None, 5, 7);
			if (npc != null)
			{
				Console.WriteLine("Found NPC from Realm None in " + zone.ZoneRegion.Description + "/" + zone.Description + ":" + npc.Name + " level:" + npc.Level);
				if (npc.Level < 5 || npc.Level > 7)
					Assert.Fail("NPC Level out of defined range");

				if (npc.Realm != eRealm.None)
					Assert.Fail("NPC wrong Realm");
			}
			else
			{
				Console.WriteLine("nothing found in " + zone.ZoneRegion.Description + "/" + zone.Description);
			}
			StopWatch();

			StartWatch();
			npc = zone.GetRandomNPC(eRealm.Albion);
			if (npc != null)
			{
				Console.WriteLine("Found Albion NPC in " + zone.ZoneRegion.Description + "/" + zone.Description + ":" + npc.Name);

				if (npc.Realm != eRealm.Albion)
					Assert.Fail("NPC wrong Realm");
			}
			else
			{
				Console.WriteLine("nothing found in " + zone.ZoneRegion.Description + "/" + zone.Description);
			}
			StopWatch();
		}
	}
}
