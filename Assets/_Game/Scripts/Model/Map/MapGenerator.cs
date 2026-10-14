using System;
using System.Collections.Generic;
using DarkestBasement.Model.Core;

namespace DarkestBasement.Model.Map
{
    public enum RoomType { Start, Combat, Treasure, Bedroom, Curio, Boss, Empty }
    public enum CorridorEventKind { None, Ambush, Curio, Trap }
    public sealed class Room
    {
        public int Id { get; }
        public int X { get; }
        public int Y { get; }
        public RoomType Type { get; internal set; }
        public bool Revealed { get; internal set; }
        public bool Visited { get; internal set; }
        public bool Cleared { get; internal set; }
        public List<int> Neighbors { get; } = new List<int>();
        public Room(int id, int x, int y) { Id = id; X = x; Y = y; }
    }
    public sealed class CorridorSegment
    {
        public int Index { get; }
        public CorridorEventKind Event { get; internal set; }
        public bool Revealed { get; internal set; }
        public bool Resolved { get; internal set; }
        public CorridorSegment(int index, CorridorEventKind kind) { Index = index; Event = kind; }
    }
    public sealed class Corridor
    {
        public int From { get; }
        public int To { get; }
        public IReadOnlyList<CorridorSegment> Segments { get; }
        public bool Cleared { get { foreach (var s in Segments) if (!s.Resolved) return false; return true; } }
        public int TravelSpeedMultiplier { get { return Cleared ? 3 : 1; } }
        public Corridor(int from, int to, CorridorSegment[] segments) { From = from; To = to; Segments = Array.AsReadOnly(segments); }
        public bool Connects(int a, int b) { return (a == From && b == To) || (a == To && b == From); }
    }
    public sealed class FloorMap
    {
        public int Floor { get; }
        public int Width { get { return 5; } }
        public int Height { get { return 4; } }
        public List<Room> Rooms { get; } = new List<Room>();
        public List<Corridor> Corridors { get; } = new List<Corridor>();
        public int StartId { get; internal set; }
        public int BossId { get; internal set; }
        public FloorMap(int floor) { Floor = floor; }
        public Room GetRoom(int id) { if (id < 0 || id >= Rooms.Count) throw new ArgumentOutOfRangeException(nameof(id)); return Rooms[id]; }
        public Corridor GetCorridor(int a, int b)
        { foreach (var corridor in Corridors) if (corridor.Connects(a, b)) return corridor; throw new InvalidOperationException("Rooms are not adjacent."); }
        public void Visit(int id)
        { Room room = GetRoom(id); room.Visited = true; room.Revealed = true; foreach (int neighbor in room.Neighbors) GetRoom(neighbor).Revealed = true; }
        public Dictionary<int, int> Distances(int origin)
        {
            var distances = new Dictionary<int, int> { { origin, 0 } }; var queue = new Queue<int>(); queue.Enqueue(origin);
            while (queue.Count > 0)
            { int id = queue.Dequeue(); foreach (int n in GetRoom(id).Neighbors) if (!distances.ContainsKey(n)) { distances[n] = distances[id] + 1; queue.Enqueue(n); } }
            return distances;
        }
    }
    public static class MapGenerator
    {
        public static FloorMap Generate(GameRng rng, int floor)
        {
            if (floor < 1 || floor > 3) throw new ArgumentOutOfRangeException(nameof(floor));
            while (true)
            {
                var map = new FloorMap(floor); int count = rng.Range(9, 12);
                map.Rooms.Add(new Room(0, rng.Range(0, 5), rng.Range(0, 4)));
                while (map.Rooms.Count < count)
                {
                    var frontier = new List<int[]>();
                    foreach (var room in map.Rooms)
                    {
                        int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };
                        for (int i = 0; i < 4; i++)
                        {
                            int x = room.X + dx[i], y = room.Y + dy[i];
                            if (x < 0 || x >= 5 || y < 0 || y >= 4) continue;
                            bool occupied = false; foreach (var other in map.Rooms) if (other.X == x && other.Y == y) occupied = true;
                            if (!occupied) frontier.Add(new[] { room.Id, x, y });
                        }
                    }
                    var next = rng.Pick(frontier); int id = map.Rooms.Count; map.Rooms.Add(new Room(id, next[1], next[2])); Link(map, next[0], id, rng);
                }
                var edges = new List<int[]>();
                foreach (var a in map.Rooms) foreach (var b in map.Rooms)
                    if (a.Id < b.Id && Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1 && !a.Neighbors.Contains(b.Id)) edges.Add(new[] { a.Id, b.Id });
                rng.Shuffle(edges); int extra = rng.Range(1, 3), added = 0;
                foreach (var edge in edges)
                {
                    int leaves = 0;
                    foreach (var room in map.Rooms) if (room.Neighbors.Count == 1 && room.Id != edge[0] && room.Id != edge[1]) leaves++;
                    // Păstrăm fundături pentru comoară și, când e posibil, boss.
                    if (leaves < 2) continue;
                    Link(map, edge[0], edge[1], rng); if (++added == extra) break;
                }
                if (added == 0) continue;
                var distances = map.Distances(0); int boss = 1;
                foreach (var room in map.Rooms)
                    if (distances[room.Id] > distances[boss] || (distances[room.Id] == distances[boss] && room.Neighbors.Count == 1 && map.Rooms[boss].Neighbors.Count != 1)) boss = room.Id;
                map.BossId = boss; map.Rooms[boss].Type = RoomType.Boss; map.Rooms[boss].Revealed = true; map.Rooms[0].Type = RoomType.Start; map.Rooms[0].Cleared = true;
                var deadEnds = new List<Room>(); foreach (var room in map.Rooms) if (room.Id != 0 && room.Id != boss && room.Neighbors.Count == 1) deadEnds.Add(room);
                if (deadEnds.Count == 0) continue;
                Room treasure = rng.Pick(deadEnds); treasure.Type = RoomType.Treasure;
                var remaining = new List<Room>(); foreach (var room in map.Rooms) if (room.Id != 0 && room.Id != boss && room != treasure) remaining.Add(room);
                rng.Shuffle(remaining); remaining[0].Type = RoomType.Bedroom; remaining[1].Type = RoomType.Curio;
                for (int i = 2; i < remaining.Count; i++) remaining[i].Type = RoomType.Combat;
                // Camera goală este rară; păstrăm aproximativ patru lupte în camere.
                if (count == 11 && rng.Chance(0.8)) remaining[2].Type = RoomType.Empty;
                map.Visit(0); return map;
            }
        }
        private static void Link(FloorMap map, int a, int b, GameRng rng)
        {
            map.Rooms[a].Neighbors.Add(b); map.Rooms[b].Neighbors.Add(a);
            var segments = new CorridorSegment[3];
            for (int i = 0; i < 3; i++)
            {
                var kind = CorridorEventKind.None;
                if (rng.Chance(0.25)) { int roll = rng.Range(0, 7); kind = roll == 0 ? CorridorEventKind.Ambush : roll <= 2 ? CorridorEventKind.Curio : CorridorEventKind.Trap; }
                segments[i] = new CorridorSegment(i, kind);
            }
            map.Corridors.Add(new Corridor(a, b, segments));
        }
    }
}
