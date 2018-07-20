using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace TsMap
{
    public struct TsPrefabNode
    {
        public int id;
        public float X;
        public float Z;
        public float RotX;
        public float RotZ;
        public List<int> InputPoints;
        public List<int> OutputPoints;
    }

    public struct TsMapPoint
    {
        public float X;
        public float Z;
        public int LaneOffset;
        public int LaneCount;
        public bool Hidden;
        public byte PrefabColorFlags;
        public int NeighbourCount;
        public List<int> Neighbours;
    }

    public class TsSpawnPoint
    {
        public float X;
        public float Z;
        public TsSpawnPointType Type;
    }

    public class TsTriggerPoint
    {
        public uint TriggerId;
        public ulong TriggerActionUid;
        public float X;
        public float Z;
    }

    public struct TsPrefabCurve
    {
        public int id;
        public int idNode;
        public float start_X;
        public float start_Z;
        public float end_X;
        public float end_Z;
        public float lenght;
        public List<int> nextLines;
        public List<int> prevLines;
    }

    public class TsPrefab
    {
        private const int NodeBlockSize = 0x68;
        private const int MapPointBlockSize = 0x30;
        private const int SpawnPointBlockSize = 0x20;
        private const int TriggerPointBlockSize = 0x30;
        private const int PrefabCurveSize = 0x84;

        private readonly string _filePath;
        public ulong Token { get; }
        public string Category { get; }

        private byte[] _stream;

        public List<TsPrefabNode> PrefabNodes { get; private set; }
        public List<TsSpawnPoint> SpawnPoints { get; private set; }
        public List<TsMapPoint> MapPoints { get; private set; }
        public List<TsTriggerPoint> TriggerPoints { get; private set; }
        public List<TsPrefabCurve> PrefabCurves {get; private set; }
        public Dictionary<Tuple<TsPrefabNode,TsPrefabNode>, Tuple<List<TsPrefabCurve>,float>> NavigationRoutes {get; private set; }

        public float indicativeWeight {get; private set;} // this is used in Dijkstra's Algorithm but it's not accurate and NEEDS IMPORVEMENT

        public TsPrefab(TsMapper mapper, string filePath, ulong token, string category)
        {
            _filePath = filePath;
            Token = token;
            Category = category;

            var file = mapper.Rfs.GetFileEntry(_filePath);

            if (file == null) return;

            _stream = file.Entry.Read();

            Parse();
        }

        private void Parse()
        {
            PrefabNodes = new List<TsPrefabNode>();
            SpawnPoints = new List<TsSpawnPoint>();
            MapPoints = new List<TsMapPoint>();
            TriggerPoints = new List<TsTriggerPoint>();
            PrefabCurves = new List<TsPrefabCurve>();
            NavigationRoutes = new Dictionary<Tuple<TsPrefabNode,TsPrefabNode>, Tuple<List<TsPrefabCurve>,float>>();

            var fileOffset = 0x0;

            var version = BitConverter.ToInt32(_stream, fileOffset);

            var nodeCount = BitConverter.ToInt32(_stream, fileOffset += 0x04);
            var curveCount = BitConverter.ToInt32(_stream, fileOffset += 0x04);
            var spawnPointCount = BitConverter.ToInt32(_stream, fileOffset += 0x0C);
            var mapPointCount = BitConverter.ToInt32(_stream, fileOffset += 0x0C);
            var triggerPointCount = BitConverter.ToInt32(_stream, fileOffset += 0x04);

            if (version > 0x15) fileOffset += 0x04; // http://modding.scssoft.com/wiki/Games/ETS2/Modding_guides/1.30#Prefabs

            var nodeOffset = BitConverter.ToInt32(_stream, fileOffset += 0x08);
            var curveOffset = BitConverter.ToInt32(_stream, fileOffset += 0x04);
            var spawnPointOffset = BitConverter.ToInt32(_stream, fileOffset += 0x0C);
            var mapPointOffset = BitConverter.ToInt32(_stream, fileOffset += 0x10);
            var triggerPointOffset = BitConverter.ToInt32(_stream, fileOffset += 0x04);

            for (var i = 0; i < nodeCount; i++)
            {
                var nodeBaseOffset = nodeOffset + (i * NodeBlockSize);
                var listInput = new List<int>();
                var listOutput = new List<int>();
                for (var j = 0; j < 8; j++) {
                    var inVal = BitConverter.ToInt32(_stream, nodeBaseOffset + 0x28 + j * 4);
                    var outVal = BitConverter.ToInt32(_stream, nodeBaseOffset + 0x48 + j * 4);
                    if (inVal != -1) {
                        listInput.Add(inVal);
                    }
                    if (outVal != -1) {
                        listOutput.Add(outVal);
                    }
                }
                var node = new TsPrefabNode
                {
                    id = i,
                    X = BitConverter.ToSingle(_stream, nodeBaseOffset + 0x10),
                    Z = BitConverter.ToSingle(_stream, nodeBaseOffset + 0x18),
                    RotX = BitConverter.ToSingle(_stream, nodeBaseOffset + 0x1C),
                    RotZ = BitConverter.ToSingle(_stream, nodeBaseOffset + 0x24),
                    InputPoints = listInput,
                    OutputPoints = listOutput
                };
                PrefabNodes.Add(node);
            }

            for (var i = 0; i < curveCount; i++) {
                var curveBaseOffset = curveOffset + (i * PrefabCurveSize);
                var countNextLines = BitConverter.ToInt32(_stream, curveBaseOffset + 0x6C);
                var nextLinesList = new List<int>();
                for (var j = 0; j < countNextLines; j++) {
                    nextLinesList.Add(BitConverter.ToInt32(_stream, curveBaseOffset + 0x4C + j * 4));
                }
                var countPrevLines = BitConverter.ToInt32(_stream, curveBaseOffset + 0x70);
                var prevLinesList = new List<int>();
                for (var j = 0; j < countPrevLines; j++) {
                    prevLinesList.Add(BitConverter.ToInt32(_stream, curveBaseOffset + 0x5C + j * 4));
                }
                var curve = new TsPrefabCurve
                {
                    id = i,
                    idNode = BitConverter.ToInt32(_stream, curveBaseOffset + 0x0C),
                    start_X = BitConverter.ToSingle(_stream, curveBaseOffset + 0x10),
                    start_Z = BitConverter.ToSingle(_stream, curveBaseOffset + 0x18),
                    end_X = BitConverter.ToSingle(_stream, curveBaseOffset + 0x1C),
                    end_Z = BitConverter.ToSingle(_stream, curveBaseOffset + 0x24),
                    lenght = BitConverter.ToSingle(_stream,curveBaseOffset + 0x44),
                    nextLines = nextLinesList,
                    prevLines = prevLinesList
                };
                PrefabCurves.Add(curve);
            }

            for (var i = 0; i < spawnPointCount; i++)
            {
                var spawnPointBaseOffset = spawnPointOffset + (i * SpawnPointBlockSize);
                var spawnPoint = new TsSpawnPoint
                {
                    X = BitConverter.ToSingle(_stream, spawnPointBaseOffset),
                    Z = BitConverter.ToSingle(_stream, spawnPointBaseOffset + 0x08),
                    Type = (TsSpawnPointType)BitConverter.ToUInt32(_stream, spawnPointBaseOffset + 0x1C)
                };
                var pointInVicinity = SpawnPoints.FirstOrDefault(point => // check if any other spawn points with the same type are close
                    point.Type == spawnPoint.Type &&
                    ((spawnPoint.X > point.X - 4 && spawnPoint.X < point.X + 4) ||
                    (spawnPoint.Z > point.Z - 4 && spawnPoint.Z < point.Z + 4)));
                if (pointInVicinity == null) SpawnPoints.Add(spawnPoint);
                // Log.Msg($"Spawn point of type: {spawnPoint.Type} in {_filePath}");
            }

            for (var i = 0; i < mapPointCount; i++)
            {
                var mapPointBaseOffset = mapPointOffset + (i * MapPointBlockSize);
                var roadLookFlags = BitConverter.ToChar(_stream, mapPointBaseOffset + 0x01);
                var laneTypeFlags = (byte) (roadLookFlags & 0x0F);
                var laneOffsetFlags = (byte)(roadLookFlags >> 4);
                int laneOffset;
                switch (laneOffsetFlags)
                {
                    case 1: laneOffset = 1; break;
                    case 2: laneOffset = 2; break;
                    case 3: laneOffset = 5; break;
                    case 4: laneOffset = 10; break;
                    case 5: laneOffset = 15; break;
                    case 6: laneOffset = 20; break;
                    case 7: laneOffset = 25; break;
                    default: laneOffset = 1; break;

                }
                int laneCount;
                switch (laneTypeFlags) // TODO: Change these (not really used atm)
                {
                    case 1: laneCount = 2; break;
                    case 2: laneCount = 4; break;
                    case 3: laneCount = 6; break;
                    case 4: laneCount = 8; break;
                    case 5: laneCount = 5; break;
                    case 6: laneCount = 7; break;
                    case 8: laneCount = 3; break;
                    case 13: laneCount = -1; break;
                    case 14: laneCount = 0; break;
                    default:
                        laneCount = 0;
                        // Log.Msg($"Unknown LaneType: {laneTypeFlags}");
                        break;
                }

                var prefabColorFlags = (byte)BitConverter.ToChar(_stream, mapPointBaseOffset + 0x02);

                var navFlags = (byte)BitConverter.ToChar(_stream, mapPointBaseOffset + 0x05);
                var hidden = (navFlags & 0x02) != 0; // Map Point is Control Node

                var point = new TsMapPoint
                {
                    LaneCount = laneCount,
                    LaneOffset = laneOffset,
                    Hidden = hidden,
                    PrefabColorFlags = prefabColorFlags,
                    X = BitConverter.ToSingle(_stream, mapPointBaseOffset + 0x08),
                    Z = BitConverter.ToSingle(_stream, mapPointBaseOffset + 0x10),
                    Neighbours = new List<int>(),
                    NeighbourCount = BitConverter.ToInt32(_stream, mapPointBaseOffset + 0x14 + (0x04 * 6))
                };

                for (var x = 0; x < point.NeighbourCount; x++)
                {
                    point.Neighbours.Add(BitConverter.ToInt32(_stream, mapPointBaseOffset + 0x14 + (x * 0x04)));
                }

                MapPoints.Add(point);
            }

            for (var i = 0; i < triggerPointCount; i++)
            {
                var triggerPointBaseOffset = triggerPointOffset + (i * TriggerPointBlockSize);
                var triggerPoint = new TsTriggerPoint
                {
                    TriggerId = BitConverter.ToUInt32(_stream, triggerPointBaseOffset),
                    TriggerActionUid = BitConverter.ToUInt64(_stream, triggerPointBaseOffset + 0x04),
                    X = BitConverter.ToSingle(_stream, triggerPointBaseOffset + 0x1C),
                    Z = BitConverter.ToSingle(_stream, triggerPointBaseOffset + 0x24),
                };
                var pointInVicinity = TriggerPoints.FirstOrDefault(point => // check if any other trigger points with the same id are close
                    point.TriggerActionUid == triggerPoint.TriggerActionUid &&
                    ((triggerPoint.X > point.X - 20 && triggerPoint.X < point.X + 20) ||
                    (triggerPoint.Z > point.Z - 20 && triggerPoint.Z < point.Z + 20)));
                if (pointInVicinity == null) TriggerPoints.Add(triggerPoint);
            }

            int nDistances = 0;
            float totDist = 0.0f;
            foreach (var inputNode in PrefabNodes)
            {
                foreach (var outputNode in PrefabNodes)
                {
                    if (inputNode.id == outputNode.id) continue;
                    var defaultCurve = default(TsPrefabCurve);
                    defaultCurve.id = -1;
                    Dictionary<TsPrefabCurve,Tuple<int,TsPrefabCurve>> distances = new Dictionary<TsPrefabCurve,Tuple<int,TsPrefabCurve>>();
                    Dictionary<TsPrefabCurve,bool> visited = new Dictionary<TsPrefabCurve,bool>();
                    foreach (var curve in PrefabCurves)
                    {
                        distances[curve] = new Tuple<int,TsPrefabCurve>(Int32.MaxValue,defaultCurve);
                    }
                    foreach (var inputCurves in inputNode.InputPoints)
                    {
                        distances[PrefabCurves[inputCurves]] = new Tuple<int,TsPrefabCurve>(0,defaultCurve);
                    }
                    var actualCurve = defaultCurve;
                    while (!outputNode.OutputPoints.Contains(actualCurve.id))
                    {
                        var minVal = Int32.MaxValue;
                        var minCurve = defaultCurve;
                        foreach (var distance in distances)
                        {
                            if (!visited.ContainsKey(distance.Key)) {
                                if (distance.Value.Item1 < minVal) {
                                    minVal = distance.Value.Item1;
                                    minCurve = distance.Key;
                                }
                            }
                        }
                        actualCurve = minCurve;
                        if (actualCurve.id == -1) break;
                        visited[actualCurve] = true;
                        foreach (var nextCurveId in actualCurve.nextLines)
                        {
                            var nextCurve = PrefabCurves[nextCurveId];
                            if (minVal + 1 < distances[nextCurve].Item1 && !visited.ContainsKey(nextCurve)) {
                                distances[nextCurve] = new Tuple<int,TsPrefabCurve>(minVal+1,actualCurve);
                            }
                        }
                    }
                    if (actualCurve.id != -1) {
                        List<TsPrefabCurve> path = new List<TsPrefabCurve>();
                        float length = (float)distances[actualCurve].Item1;
                        float distanceLength = 0.0f;
                        while (actualCurve.id != -1)
                        {
                            distanceLength += (float)Math.Sqrt(Math.Pow(actualCurve.start_X - actualCurve.end_X,2) + Math.Pow(actualCurve.start_Z - actualCurve.end_Z,2));
                            path.Add(actualCurve);
                            actualCurve = distances[actualCurve].Item2;
                        }
                        NavigationRoutes.Add(new Tuple<TsPrefabNode,TsPrefabNode>(inputNode,outputNode), new Tuple<List<TsPrefabCurve>,float>(path,length));
                        totDist += distanceLength;
                        nDistances++;
                    }
                }
            }

            indicativeWeight = totDist / nDistances;
            
            _stream = null;

        }
    }
}
