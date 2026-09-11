using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XianXia.Data.Serialization;

namespace XianXia.Data.Content
{
    /// <summary>Deterministic, region-only W2A authoring bake. Runtime never calls this.</summary>
    public static class W2ASurfaceGeographyBaker
    {
        sealed class Feature
        {
            public string Id, Kind, RiverId, RoadId;
            public float X, Y, W, H, Stroke;
            public readonly List<float> Points = new List<float>();
        }

        public static void BakeFile(string sourcePath, string surfaceDefinitionsPath, string outputPath)
        {
            var sourceText = File.ReadAllText(sourcePath, Encoding.UTF8);
            var source = SimpleJson.Parse(sourceText);
            var surfaceRoot = SimpleJson.Parse(File.ReadAllText(surfaceDefinitionsPath, Encoding.UTF8));
            var surfaceId = source.GetString("surfaceId", string.Empty);
            if (source.GetNumber("sourceSchemaVersion", 0) != 1 || string.IsNullOrWhiteSpace(surfaceId))
                throw new InvalidDataException("W2A source schema/surfaceId invalid.");
            if (!TryReadSurfaceMetrics(surfaceRoot, surfaceId, out var surfaceOriginX, out var surfaceOriginY,
                    out var cellSize, out var chunkWidth, out var chunkHeight))
                throw new InvalidDataException("W2A source surface is missing from the Main surface definition.");
            var cellsPerChunkX = ExactCellCount(chunkWidth, cellSize);
            var cellsPerChunkY = ExactCellCount(chunkHeight, cellSize);
            var chunks = ReadChunks(source);
            ValidateCoverageExists(surfaceRoot, surfaceId, chunks);
            ValidateRectangularCoverage(chunks, out var minChunkX, out var minChunkY, out var maxChunkX, out var maxChunkY);
            var width = checked((maxChunkX - minChunkX + 1) * cellsPerChunkX);
            var height = checked((maxChunkY - minChunkY + 1) * cellsPerChunkY);
            var originX = surfaceOriginX + minChunkX * chunkWidth;
            var originY = surfaceOriginY + minChunkY * chunkHeight;
            var features = ReadFeatures(source);
            ValidateFeatures(features);
            ValidateLandmarks(source);
            var cells = Rasterize(features, originX, originY, cellSize, width, height);
            ValidateBridgeConnectivity(features, cells, originX, originY, cellSize, width, height);
            var hash = Sha256(sourceText);
            var json = WriteBake(source, surfaceId, hash, chunks, features, cells,
                originX, originY, cellSize, width, height, cellsPerChunkX, cellsPerChunkY, minChunkX, minChunkY);
            // Validate the complete temporary artifact before replacing the last known-good bake.
            var parsed = SimpleJson.Parse(json);
            if (!parsed.TryGetProperty("definitions", out var definitions) || definitions.Kind != JsonValueKind.Array || definitions.Array.Count != 1)
                throw new InvalidDataException("Generated W2A bake failed root validation.");
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temp = outputPath + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            if (File.Exists(outputPath)) File.Replace(temp, outputPath, null);
            else File.Move(temp, outputPath);
        }

        static bool TryReadSurfaceMetrics(JsonValue root, string id, out float ox, out float oy, out float cell, out float cw, out float ch)
        {
            ox = oy = cell = cw = ch = 0f;
            if (!root.TryGetProperty("definitions", out var defs) || defs.Kind != JsonValueKind.Array) return false;
            foreach (var item in defs.Array)
            {
                if (!string.Equals(item.GetString("id", string.Empty), id, StringComparison.Ordinal)) continue;
                ox = (float)item.GetNumber("originWorldX", 0); oy = (float)item.GetNumber("originWorldY", 0);
                cell = (float)item.GetNumber("cellSize", 0); cw = (float)item.GetNumber("chunkWidth", 0); ch = (float)item.GetNumber("chunkHeight", 0);
                return cell > 0f && cw > 0f && ch > 0f;
            }
            return false;
        }

        static int ExactCellCount(float extent, float cell)
        {
            var raw = extent / cell;
            var count = (int)Math.Round(raw);
            if (count <= 0 || Math.Abs(raw - count) > 0.001f) throw new InvalidDataException("Surface chunk extent must divide exactly by cellSize.");
            return count;
        }

        static List<(int x, int y)> ReadChunks(JsonValue source)
        {
            if (!source.TryGetProperty("coverageChunks", out var nodes) || nodes.Kind != JsonValueKind.Array || nodes.Array.Count == 0)
                throw new InvalidDataException("coverageChunks required.");
            var result = new List<(int, int)>(); var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes.Array)
            {
                var x = (int)node.GetNumber("x", int.MinValue); var y = (int)node.GetNumber("y", int.MinValue);
                if (!unique.Add(x + ":" + y)) throw new InvalidDataException("Duplicate coverage chunk " + x + "," + y);
                result.Add((x, y));
            }
            return result;
        }

        static void ValidateRectangularCoverage(List<(int x, int y)> chunks, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = minY = int.MaxValue; maxX = maxY = int.MinValue;
            foreach (var c in chunks) { minX = Math.Min(minX, c.x); minY = Math.Min(minY, c.y); maxX = Math.Max(maxX, c.x); maxY = Math.Max(maxY, c.y); }
            if (chunks.Count != (maxX - minX + 1) * (maxY - minY + 1)) throw new InvalidDataException("W2A V1 coverage must be one complete rectangle.");
        }

        static void ValidateCoverageExists(JsonValue root, string surfaceId, List<(int x, int y)> chunks)
        {
            JsonValue surface = null;
            if (root.TryGetProperty("definitions", out var defs) && defs.Kind == JsonValueKind.Array)
                foreach (var item in defs.Array)
                    if (string.Equals(item.GetString("id", string.Empty), surfaceId, StringComparison.Ordinal)) { surface = item; break; }
            if (surface == null || !surface.TryGetProperty("chunks", out var nodes) || nodes.Kind != JsonValueKind.Array)
                throw new InvalidDataException("Main Surface chunk registry unavailable.");
            var authored = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes.Array) authored.Add((int)node.GetNumber("x", 0) + ":" + (int)node.GetNumber("y", 0));
            foreach (var chunk in chunks)
                if (!authored.Contains(chunk.x + ":" + chunk.y))
                    throw new InvalidDataException("Coverage chunk is absent from Main Surface: " + chunk.x + "," + chunk.y);
        }

        static List<Feature> ReadFeatures(JsonValue source)
        {
            if (!source.TryGetProperty("features", out var nodes) || nodes.Kind != JsonValueKind.Array) throw new InvalidDataException("features required.");
            var result = new List<Feature>();
            foreach (var node in nodes.Array)
            {
                var f = new Feature
                {
                    Id = node.GetString("stableId", string.Empty), Kind = node.GetString("kind", string.Empty),
                    RiverId = node.GetString("riverId", string.Empty), RoadId = node.GetString("roadId", string.Empty),
                    X = (float)node.GetNumber("worldX", 0), Y = (float)node.GetNumber("worldY", 0),
                    W = (float)node.GetNumber("worldWidth", 0), H = (float)node.GetNumber("worldHeight", 0),
                    Stroke = (float)node.GetNumber("strokeWidth", 0)
                };
                if (node.TryGetProperty("points", out var points) && points.Kind == JsonValueKind.Array)
                    foreach (var p in points.Array) if (p.Kind == JsonValueKind.Number) f.Points.Add((float)p.Number);
                result.Add(f);
            }
            return result;
        }

        static void ValidateFeatures(List<Feature> features)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in features)
            {
                if (string.IsNullOrWhiteSpace(f.Id) || !ids.Add(f.Id)) throw new InvalidDataException("Feature stableId missing/duplicate.");
                if (!Finite(f.X) || !Finite(f.Y) || !Finite(f.W) || !Finite(f.H) || !Finite(f.Stroke)) throw new InvalidDataException("Feature coordinate is not finite: " + f.Id);
                if (f.Kind == "roadPolyline" && (f.Stroke <= 0f || f.Points.Count < 4 || (f.Points.Count & 1) != 0)) throw new InvalidDataException("Invalid road polyline: " + f.Id);
                if (f.Kind != "roadPolyline" && (f.W <= 0f || f.H <= 0f)) throw new InvalidDataException("Invalid feature rectangle: " + f.Id);
            }
        }

        static void ValidateLandmarks(JsonValue source)
        {
            if (!source.TryGetProperty("landmarks", out var nodes) || nodes.Kind != JsonValueKind.Array || nodes.Array.Count != 2)
                throw new InvalidDataException("W2A requires exactly two static landmarks.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in nodes.Array)
            {
                var id = node.GetString("stableId", string.Empty);
                var label = node.GetString("label", string.Empty);
                var x = (float)node.GetNumber("worldX", float.NaN);
                var y = (float)node.GetNumber("worldY", float.NaN);
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label) || !ids.Add(id) || !Finite(x) || !Finite(y))
                    throw new InvalidDataException("Invalid W2A landmark.");
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static char[] Rasterize(List<Feature> features, float ox, float oy, float cell, int width, int height)
        {
            var result = new char[width * height]; for (var i = 0; i < result.Length; i++) result[i] = '.';
            foreach (var f in features) if (f.Kind == "waterRegion") RasterRect(result, width, height, ox, oy, cell, f, '~', false);
            foreach (var f in features) if (f.Kind == "roadPolyline") RasterLine(result, width, height, ox, oy, cell, f);
            foreach (var f in features) if (f.Kind == "bridge") RasterRect(result, width, height, ox, oy, cell, f, 'B', true);
            foreach (var f in features) if (f.Kind == "solidBlocker") RasterRect(result, width, height, ox, oy, cell, f, '#', true);
            return result;
        }

        static void RasterRect(char[] cells, int width, int height, float ox, float oy, float cell, Feature f, char code, bool overwrite)
        {
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var wx = ox + (x + .5f) * cell; var wy = oy + (y + .5f) * cell;
                if (wx < f.X || wx >= f.X + f.W || wy < f.Y || wy >= f.Y + f.H) continue;
                var i = x + y * width; if (overwrite || cells[i] == '.') cells[i] = code;
            }
        }

        static void RasterLine(char[] cells, int width, int height, float ox, float oy, float cell, Feature f)
        {
            var radius2 = f.Stroke * f.Stroke * .25f;
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var wx = ox + (x + .5f) * cell; var wy = oy + (y + .5f) * cell;
                var hit = false;
                for (var p = 0; p + 3 < f.Points.Count && !hit; p += 2)
                    hit = DistanceToSegmentSquared(wx, wy, f.Points[p], f.Points[p + 1], f.Points[p + 2], f.Points[p + 3]) <= radius2;
                var i = x + y * width; if (hit && cells[i] == '.') cells[i] = '='; // Road never clears water/solid.
            }
        }

        static float DistanceToSegmentSquared(float px, float py, float ax, float ay, float bx, float by)
        {
            var dx = bx - ax; var dy = by - ay; var d = dx * dx + dy * dy;
            var t = d <= 1e-8f ? 0f : Math.Max(0f, Math.Min(1f, ((px - ax) * dx + (py - ay) * dy) / d));
            var x = ax + dx * t; var y = ay + dy * t; dx = px - x; dy = py - y; return dx * dx + dy * dy;
        }

        static void ValidateBridgeConnectivity(List<Feature> features, char[] cells, float ox, float oy, float cell, int width, int height)
        {
            var byId = new Dictionary<string, Feature>(StringComparer.Ordinal); foreach (var f in features) byId[f.Id] = f;
            foreach (var bridge in features)
            {
                if (bridge.Kind != "bridge") continue;
                if (!byId.TryGetValue(bridge.RiverId, out var river) || river.Kind != "waterRegion" ||
                    !byId.TryGetValue(bridge.RoadId, out var road) || road.Kind != "roadPolyline")
                    throw new InvalidDataException("Bridge must reference one baked river and road: " + bridge.Id);
                var overlapsRiver = bridge.X < river.X + river.W && bridge.X + bridge.W > river.X && bridge.Y < river.Y + river.H && bridge.Y + bridge.H > river.Y;
                var connectsBanks = bridge.X < river.X && bridge.X + bridge.W > river.X + river.W;
                if (!overlapsRiver || !connectsBanks) throw new InvalidDataException("Bridge does not connect both declared river banks: " + bridge.Id);
                var cy = bridge.Y + bridge.H * .5f;
                if (!AnyRoadNear(road, bridge.X, cy, bridge.X + bridge.W, cy, bridge.H))
                    throw new InvalidDataException("Bridge is not connected to its declared road: " + bridge.Id);
            }
        }

        static bool AnyRoadNear(Feature road, float ax, float ay, float bx, float by, float tolerance)
        {
            for (var p = 0; p + 3 < road.Points.Count; p += 2)
            {
                var mx = (road.Points[p] + road.Points[p + 2]) * .5f; var my = (road.Points[p + 1] + road.Points[p + 3]) * .5f;
                if (DistanceToSegmentSquared(mx, my, ax, ay, bx, by) <= tolerance * tolerance) return true;
            }
            return false;
        }

        static string WriteBake(JsonValue source, string surfaceId, string hash, List<(int x, int y)> chunks,
            List<Feature> features, char[] cells, float ox, float oy, float cell, int width, int height,
            int chunkCellsX, int chunkCellsY, int minChunkX, int minChunkY)
        {
            var sb = new StringBuilder(160000); var ci = CultureInfo.InvariantCulture;
            sb.Append("{\n  \"schemaVersion\": 1,\n  \"definitions\": [\n    {\n");
            sb.Append("      \"id\": \"base:w2a_surface_geography_v1\",\n      \"type\": \"outdoorSurfaceGeography\",\n      \"name\": \"W2A Surface Geography\",\n");
            sb.Append("      \"surfaceId\": \"").Append(Escape(surfaceId)).Append("\",\n");
            sb.Append("      \"sourceSchemaVersion\": 1,\n      \"sourceRevision\": \"").Append(Escape(source.GetString("sourceRevision", string.Empty))).Append("\",\n");
            sb.Append("      \"sourceHash\": \"").Append(hash).Append("\",\n");
            sb.Append("      \"originWorldX\": ").Append(ox.ToString("R", ci)).Append(",\n      \"originWorldY\": ").Append(oy.ToString("R", ci));
            sb.Append(",\n      \"cellSize\": ").Append(cell.ToString("R", ci)).Append(",\n      \"width\": ").Append(width).Append(",\n      \"height\": ").Append(height).Append(",\n");
            sb.Append("      \"coverageChunks\": [");
            for (var i = 0; i < chunks.Count; i++) { if (i > 0) sb.Append(','); sb.Append("{\"x\":").Append(chunks[i].x).Append(",\"y\":").Append(chunks[i].y).Append('}'); }
            sb.Append("],\n      \"rows\": [\n");
            sb.Length -= "      \"rows\": [\n".Length;
            sb.Append("      \"chunkRows\": [\n");
            for (var c = 0; c < chunks.Count; c++)
            {
                var localOriginX = (chunks[c].x - minChunkX) * chunkCellsX;
                var localOriginY = (chunks[c].y - minChunkY) * chunkCellsY;
                sb.Append("        {\"x\":").Append(chunks[c].x).Append(",\"y\":").Append(chunks[c].y).Append(",\"rows\":[");
                for (var ly = 0; ly < chunkCellsY; ly++)
                {
                    if (ly > 0) sb.Append(',');
                    sb.Append('"').Append(cells, (localOriginY + ly) * width + localOriginX, chunkCellsX).Append('"');
                }
                sb.Append("]}"); if (c + 1 < chunks.Count) sb.Append(','); sb.Append('\n');
            }
            sb.Append("      ],\n      \"rows\": [\n");
            for (var y = 0; y < height; y++) { sb.Append("        \"").Append(cells, y * width, width).Append('"'); if (y + 1 < height) sb.Append(','); sb.Append('\n'); }
            sb.Append("      ],\n      \"mapPrimitives\": [\n");
            for (var i = 0; i < features.Count; i++)
            {
                var f = features[i]; sb.Append("        {\"stableId\":\"").Append(Escape(f.Id)).Append("\",\"kind\":\"").Append(Escape(f.Kind)).Append('"');
                sb.Append(",\"worldX\":").Append(f.X.ToString("R", ci)).Append(",\"worldY\":").Append(f.Y.ToString("R", ci));
                sb.Append(",\"worldWidth\":").Append(f.W.ToString("R", ci)).Append(",\"worldHeight\":").Append(f.H.ToString("R", ci));
                sb.Append(",\"strokeWidth\":").Append(f.Stroke.ToString("R", ci)).Append(",\"points\":[");
                for (var p = 0; p < f.Points.Count; p++) { if (p > 0) sb.Append(','); sb.Append(f.Points[p].ToString("R", ci)); }
                sb.Append("]}"); if (i + 1 < features.Count) sb.Append(','); sb.Append('\n');
            }
            sb.Append("      ],\n      \"landmarks\": [");
            if (source.TryGetProperty("landmarks", out var landmarks) && landmarks.Kind == JsonValueKind.Array)
                for (var i = 0; i < landmarks.Array.Count; i++)
                {
                    var l = landmarks.Array[i]; if (i > 0) sb.Append(',');
                    sb.Append("{\"stableId\":\"").Append(Escape(l.GetString("stableId", string.Empty))).Append("\",\"label\":\"").Append(Escape(l.GetString("label", string.Empty))).Append("\",\"worldX\":");
                    sb.Append(((float)l.GetNumber("worldX", 0)).ToString("R", ci)).Append(",\"worldY\":").Append(((float)l.GetNumber("worldY", 0)).ToString("R", ci)).Append('}');
                }
            // V1 summary is intentionally derived-only and cannot mutate HexWorld.
            sb.Append("],\n      \"hexSummary\": []\n    }\n  ]\n}\n");
            return sb.ToString();
        }

        static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text)); var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2")); return sb.ToString();
            }
        }

        static string Escape(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
