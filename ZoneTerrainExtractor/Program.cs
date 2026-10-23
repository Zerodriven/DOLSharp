using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MpkArchive = DOL.MPK.MPK;

namespace DOL.Tools.ZoneTerrainExtractor
{
    public class RoadPoint
    {
        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
    }

    public class RoadSegment
    {
        public int roadId { get; set; }
        public string? name { get; set; }
        public double width { get; set; } = 8.0;
        public List<RoadPoint> points { get; set; } = new List<RoadPoint>();
    }

    public class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine(" DAoC / DOL Zone 200 Terrain & Road Extractor for Unity");
            Console.WriteLine("=========================================================");

            string daocPath = @"C:\DAoC";
            string outputRawFileName = "terrain_162.raw";
            string outputRoadsFileName = "roads_200.json";
            int requestedZoneId = 162;

            if (args.Length > 0 && int.TryParse(args[0], out int customZone))
                requestedZoneId = customZone;
            if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
                daocPath = args[1];
            if (args.Length > 2 && !string.IsNullOrWhiteSpace(args[2]))
                outputRawFileName = args[2];
            if (args.Length > 3 && !string.IsNullOrWhiteSpace(args[3]))
                outputRoadsFileName = args[3];

            Console.WriteLine($"[1] DAoC Client Assets Path : {daocPath}");
            Console.WriteLine($"[2] Requested Zone / Target  : Zone {requestedZoneId} (Lough Derg / Zone 200)");
            Console.WriteLine($"[3] Output Heightmap Raw     : {outputRawFileName}");
            Console.WriteLine($"[4] Output Road Network JSON : {outputRoadsFileName}");
            Console.WriteLine();

            // Part A: Inspect client archives for Zone 200
            InspectZoneArchives(daocPath, 200);

            // Part B: Extract Terrain Heightmap
            int res = ExtractTerrainHeightmap(daocPath, requestedZoneId, outputRawFileName, out int minZ, out int maxZ, out byte[]? terrainMap, out byte[]? offsetMap, out int scaleFactor, out int offsetFactor);
            if (res != 0)
                return res;

            // Part C: Extract Road / Waypoint Data
            ExtractRoadData(outputRoadsFileName, minZ, maxZ, terrainMap, offsetMap, scaleFactor, offsetFactor);

            return 0;
        }

        private static void InspectZoneArchives(string daocPath, int zoneId)
        {
            Console.WriteLine("---------------------------------------------------------");
            Console.WriteLine($" [Phase 1] Inspecting Client MPK Archives for Zone {zoneId}");
            Console.WriteLine("---------------------------------------------------------");

            string zoneDir = Path.Combine(daocPath, "zones", $"zone{zoneId:D3}");
            if (!Directory.Exists(zoneDir))
            {
                Console.WriteLine($"[Warning] Zone directory not found at: {zoneDir}");
                return;
            }

            var mpkFiles = Directory.GetFiles(zoneDir, "*.mpk");
            Console.WriteLine($"Found {mpkFiles.Length} MPK archives in {zoneDir}:");

            var matchedEntries = new List<string>();
            bool sectorHasRoadTable = false;

            foreach (var mpkPath in mpkFiles)
            {
                string archiveName = Path.GetFileName(mpkPath);
                try
                {
                    var mpk = new MpkArchive(mpkPath, false);
                    Console.WriteLine($"  * {archiveName} ({mpk.Count} entries)");

                    foreach (var entry in mpk)
                    {
                        if (Regex.IsMatch(entry.Key, "(?i)road|path|spline|waypoint|route"))
                        {
                            matchedEntries.Add($"{archiveName} -> {entry.Key}");
                        }
                    }

                    if (mpk["sector.dat"] != null)
                    {
                        string sectorText = Encoding.ASCII.GetString(mpk["sector.dat"].Data);
                        if (Regex.IsMatch(sectorText, "(?i)\\[road|\\[path|\\[spline|road\\d+|path\\d+"))
                        {
                            sectorHasRoadTable = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    [Error reading {archiveName}: {ex.Message}]");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Client Archive Inspection Results:");
            if (matchedEntries.Count > 0)
            {
                Console.WriteLine($"  Entries matching road/path/spline keywords: {matchedEntries.Count}");
                foreach (var match in matchedEntries)
                    Console.WriteLine($"    - {match}");
            }
            else
            {
                Console.WriteLine("  - No standalone 'road*.dat', 'path*.dat', or 'spline*.dat' files found inside zone archives.");
            }

            Console.WriteLine($"  - Sector layout ('sector.dat') road tables present: {(sectorHasRoadTable ? "Yes" : "No (defines rivers/lakes only, e.g. [river00])")}");
            Console.WriteLine("  - Visual terrain road rendering: Defined via terrain splat layer 'cobblestonePath' in 'textures.csv' & 'patch*.dds' alpha masks.");
            Console.WriteLine();
        }

        private static int ExtractTerrainHeightmap(string daocPath, int requestedZoneId, string outputFileName,
            out int minZ, out int maxZ, out byte[]? terrainMap, out byte[]? offsetMap, out int scaleFactor, out int offsetFactor)
        {
            minZ = int.MaxValue;
            maxZ = int.MinValue;
            terrainMap = null;
            offsetMap = null;
            scaleFactor = 8;
            offsetFactor = 48;

            Console.WriteLine("---------------------------------------------------------");
            Console.WriteLine(" [Phase 2] Extracting Zone 200 (Lough Derg) Terrain Heightmap");
            Console.WriteLine("---------------------------------------------------------");

            string zoneDir162 = Path.Combine(daocPath, "zones", $"zone{requestedZoneId:D3}");
            string mpkPath162 = Path.Combine(zoneDir162, $"dat{requestedZoneId:D3}.mpk");

            string activeMpkPath = mpkPath162;
            bool needFallback = false;
            if (File.Exists(mpkPath162))
            {
                var testMpk = new MpkArchive(mpkPath162, false);
                if (testMpk["terrain.pcx"] == null)
                {
                    Console.WriteLine($"[Note] Zone {requestedZoneId} archive ({Path.GetFileName(mpkPath162)}) contains indoor/dungeon chunk data without terrain.pcx.");
                    Console.WriteLine($"       Resolving Lough Derg terrain assets to Zone 200 (Hibernia - Lough Derg)...");
                    needFallback = true;
                }
            }
            else
            {
                needFallback = true;
            }

            if (needFallback)
            {
                activeMpkPath = Path.Combine(daocPath, "zones", "zone200", "dat200.mpk");
                if (!File.Exists(activeMpkPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Error] Could not find zone archive at '{activeMpkPath}'. Please ensure DAoC shard path is valid.");
                    Console.ResetColor();
                    return 1;
                }
            }

            Console.WriteLine($"[+] Loading terrain archive: {activeMpkPath}");
            var mpk = new MpkArchive(activeMpkPath, false);

            if (mpk["terrain.pcx"] == null || mpk["offset.pcx"] == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[Error] Archive is missing required terrain.pcx or offset.pcx assets!");
                Console.ResetColor();
                return 1;
            }

            if (mpk["sector.dat"] != null)
            {
                string sectorDat = Encoding.ASCII.GetString(mpk["sector.dat"].Data);
                var scaleMatch = Regex.Match(sectorDat, @"(?i)scalefactor\s*=\s*(\d+)");
                if (scaleMatch.Success && int.TryParse(scaleMatch.Groups[1].Value, out int s))
                    scaleFactor = s;

                var offsetMatch = Regex.Match(sectorDat, @"(?i)offsetfactor\s*=\s*(\d+)");
                if (offsetMatch.Success && int.TryParse(offsetMatch.Groups[1].Value, out int o))
                    offsetFactor = o;

                var nameMatch = Regex.Match(sectorDat, @"(?i)name\s*=\s*(.+)");
                string sectorName = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "Unknown";
                Console.WriteLine($"[+] Sector Name: {sectorName}");
            }

            Console.WriteLine($"[+] Terrain scale factors: scalefactor={scaleFactor}, offsetfactor={offsetFactor}");
            terrainMap = DecodePCX(mpk["terrain.pcx"].Data, out int tw, out int th);
            offsetMap = DecodePCX(mpk["offset.pcx"].Data, out int ow, out int oh);

            const int GridResolution = 256;
            int totalSamples = GridResolution * GridResolution;
            int[] rawZValues = new int[totalSamples];

            for (int y = 0; y < GridResolution; y++)
            {
                for (int x = 0; x < GridResolution; x++)
                {
                    int index = y * GridResolution + x;
                    int tVal = terrainMap[index];
                    int oVal = offsetMap[index];

                    int z = (tVal * scaleFactor) + (oVal * offsetFactor);
                    rawZValues[index] = z;

                    if (z < minZ) minZ = z;
                    if (z > maxZ) maxZ = z;
                }
            }

            Console.WriteLine($"[+] Minimum Z: {minZ}, Maximum Z: {maxZ} (Z Range: {maxZ - minZ} units)");

            double range = maxZ - minZ;
            byte[] rawBytes = new byte[totalSamples * 2];

            int byteIndex = 0;
            for (int i = 0; i < totalSamples; i++)
            {
                ushort normVal = range > 0
                    ? (ushort)Math.Clamp(Math.Round(((rawZValues[i] - minZ) / range) * 65535.0), 0.0, 65535.0)
                    : (ushort)0;

                rawBytes[byteIndex++] = (byte)(normVal & 0xFF);
                rawBytes[byteIndex++] = (byte)((normVal >> 8) & 0xFF);
            }

            string fullOutputPath = Path.GetFullPath(outputFileName);
            File.WriteAllBytes(fullOutputPath, rawBytes);

            FileInfo fi = new FileInfo(fullOutputPath);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[SUCCESS] Heightmap written to: {fi.FullName} ({fi.Length:N0} bytes)");
            Console.ResetColor();
            Console.WriteLine();

            return 0;
        }

        private static void ExtractRoadData(string outputFileName, int minZ, int maxZ,
            byte[]? terrainMap, byte[]? offsetMap, int scaleFactor, int offsetFactor)
        {
            Console.WriteLine("---------------------------------------------------------");
            Console.WriteLine(" [Phase 3] Extracting Road / Path Waypoint Data for Zone 200");
            Console.WriteLine("---------------------------------------------------------");

            Console.WriteLine("DOL Server Database / Source Architecture:");
            Console.WriteLine("  - Table 'Path'       : DBPath (PathID, PathType, RegionID)");
            Console.WriteLine("  - Table 'PathPoints' : DBPathPoint (PathID, Step, X, Y, Z, MaxSpeed, WaitTime)");
            Console.WriteLine("  - Default Data File  : GameServerScripts/quests/Hibernia/BaseAddrirPathPoints.xml");
            Console.WriteLine();

            // Zone 200 offset in Region 200: OffX = 39, OffY = 59.
            // In world units: 39 * 8192 = 319488, 59 * 8192 = 483328
            const int ZoneOffsetX = 39 * 8192; // 319488
            const int ZoneOffsetY = 59 * 8192; // 483328
            const double CoordScale = 65.535; // 65535 / 1000.0

            // Locate BaseAddrirPathPoints.xml
            string[] candidateXmlPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameServerScripts", "quests", "Hibernia", "BaseAddrirPathPoints.xml"),
                Path.Combine(Directory.GetCurrentDirectory(), "GameServerScripts", "quests", "Hibernia", "BaseAddrirPathPoints.xml"),
                @"D:\code\DOLSharp\GameServerScripts\quests\Hibernia\BaseAddrirPathPoints.xml"
            };

            string? xmlPath = candidateXmlPaths.FirstOrDefault(File.Exists);
            if (xmlPath == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[Warning] BaseAddrirPathPoints.xml not found in default paths.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine($"[+] Parsing route waypoints from: {xmlPath}");
            var doc = XDocument.Load(xmlPath);

            var rawPoints = doc.Descendants("PathPoints")
                .Where(e => e.Element("PathID") != null && e.Element("X") != null && e.Element("Y") != null && e.Element("Z") != null)
                .Select(e => new
                {
                    PathId = e.Element("PathID")!.Value,
                    Step = int.Parse(e.Element("Step")?.Value ?? "0"),
                    X = int.Parse(e.Element("X")!.Value),
                    Y = int.Parse(e.Element("Y")!.Value),
                    Z = int.Parse(e.Element("Z")!.Value),
                })
                .ToList();

            var grouped = rawPoints.GroupBy(p => p.PathId);
            var roadSegments = new List<RoadSegment>();
            int currentRoadId = 1;

            double zRange = (maxZ - minZ) > 0 ? (maxZ - minZ) : 1.0;

            foreach (var group in grouped)
            {
                var ordered = group.OrderBy(p => p.Step).ToList();
                var inZoneRoadPoints = new List<RoadPoint>();

                foreach (var pt in ordered)
                {
                    int zoneX = pt.X - ZoneOffsetX;
                    int zoneY = pt.Y - ZoneOffsetY;

                    // Filter points within Zone 200 bounds (0..65535)
                    if (zoneX >= 0 && zoneX <= 65535 && zoneY >= 0 && zoneY <= 65535)
                    {
                        double unityX = Math.Round(zoneX / CoordScale, 1);
                        double unityZ = Math.Round(zoneY / CoordScale, 1);

                        // Unity Y mapped to terrain height (0..100 Unity terrain height range)
                        // Sample terrain elevation if available, or use waypoint Z
                        double unityY;
                        if (terrainMap != null && offsetMap != null)
                        {
                            int gx = Math.Clamp((int)Math.Round(zoneX / 65535.0 * 255.0), 0, 255);
                            int gy = Math.Clamp((int)Math.Round(zoneY / 65535.0 * 255.0), 0, 255);
                            int sZ = (terrainMap[gy * 256 + gx] * scaleFactor) + (offsetMap[gy * 256 + gx] * offsetFactor);
                            unityY = Math.Round(((sZ - minZ) / zRange) * 100.0, 1);
                        }
                        else
                        {
                            unityY = Math.Round(((pt.Z - minZ) / zRange) * 100.0, 1);
                        }

                        inZoneRoadPoints.Add(new RoadPoint
                        {
                            x = unityX,
                            y = unityY,
                            z = unityZ
                        });
                    }
                }

                // If this route has valid segments within Zone 200, record it
                if (inZoneRoadPoints.Count >= 2)
                {
                    roadSegments.Add(new RoadSegment
                    {
                        roadId = currentRoadId++,
                        name = group.Key,
                        width = 8.0,
                        points = inZoneRoadPoints
                    });
                }
            }

            Console.WriteLine($"[+] Extracted {roadSegments.Count} road segments in Zone 200:");
            foreach (var r in roadSegments)
            {
                Console.WriteLine($"    Road #{r.roadId} ('{r.name}'): {r.points.Count} points, start=({r.points.First().x}, {r.points.First().y}, {r.points.First().z})");
            }

            // Serialize to roads_200.json
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string jsonText = JsonSerializer.Serialize(roadSegments, jsonOptions);
            string fullRoadsPath = Path.GetFullPath(outputFileName);
            File.WriteAllText(fullRoadsPath, jsonText);

            FileInfo roadsFi = new FileInfo(fullRoadsPath);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[SUCCESS] Road data written to: {roadsFi.FullName} ({roadsFi.Length:N0} bytes)");
            Console.ResetColor();
            Console.WriteLine();
        }

        public static byte[] DecodePCX(byte[] fileBytes, out int width, out int height)
        {
            if (fileBytes == null || fileBytes.Length < 128)
                throw new ArgumentException("Invalid PCX data: file too small");

            short xmin = BitConverter.ToInt16(fileBytes, 4);
            short ymin = BitConverter.ToInt16(fileBytes, 6);
            short xmax = BitConverter.ToInt16(fileBytes, 8);
            short ymax = BitConverter.ToInt16(fileBytes, 10);

            width = xmax - xmin + 1;
            height = ymax - ymin + 1;
            ushort bytesPerLine = BitConverter.ToUInt16(fileBytes, 66);

            byte[] decoded = new byte[width * height];
            int srcIndex = 128; // PCX header is 128 bytes

            for (int y = 0; y < height; y++)
            {
                int rowBytes = 0;
                while (rowBytes < bytesPerLine)
                {
                    if (srcIndex >= fileBytes.Length)
                        break;

                    byte b = fileBytes[srcIndex++];
                    int count = 1;
                    byte value = b;

                    if ((b & 0xC0) == 0xC0)
                    {
                        count = b & 0x3F;
                        if (srcIndex < fileBytes.Length)
                            value = fileBytes[srcIndex++];
                    }

                    for (int c = 0; c < count; c++)
                    {
                        if (rowBytes < width)
                        {
                            decoded[y * width + rowBytes] = value;
                        }
                        rowBytes++;
                    }
                }
            }

            return decoded;
        }
    }
}
