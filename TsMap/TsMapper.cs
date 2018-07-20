using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TsMap.HashFiles;

namespace TsMap
{
    public class TsMapper
    {
        private readonly string _gameDir;

        public RootFileSystem Rfs;

        private List<string> _sectorFiles;
        private readonly string[] _overlayFiles;

        private readonly Dictionary<ulong, TsPrefab> _prefabLookup = new Dictionary<ulong, TsPrefab>();
        private readonly Dictionary<ulong, TsCity> _citiesLookup = new Dictionary<ulong, TsCity>();
        private readonly Dictionary<ulong, TsRoadLook> _roadLookup = new Dictionary<ulong, TsRoadLook>();
        private readonly Dictionary<ulong, TsMapOverlay> _overlayLookup = new Dictionary<ulong, TsMapOverlay>();
        private readonly List<TsFerryConnection> _ferryConnectionLookup = new List<TsFerryConnection>();

        public readonly List<TsRoadItem> Roads = new List<TsRoadItem>();
        public readonly List<TsPrefabItem> Prefabs = new List<TsPrefabItem>();
        public readonly List<TsCityItem> Cities = new List<TsCityItem>();
        public readonly List<TsMapOverlayItem> MapOverlays = new List<TsMapOverlayItem>();
        public readonly List<TsFerryItem> FerryConnections = new List<TsFerryItem>();
        public readonly List<TsCompanyItem> Companies = new List<TsCompanyItem>();

        public readonly Dictionary<ulong,TsItem> Items = new Dictionary<ulong,TsItem>();
        public readonly Dictionary<ulong, TsNode> Nodes = new Dictionary<ulong, TsNode>();

        private List<TsSector> Sectors { get; set; }

        public List<TsRoadItem> RouteRoads = new List<TsRoadItem>();
        public List<TsPrefabItem> RoutePrefabs = new List<TsPrefabItem>();
        public Dictionary<TsPrefabItem,List<TsPrefabCurve>> PrefabNav = new Dictionary<TsPrefabItem,List<TsPrefabCurve>>();

        public TsMapper(string gameDir)
        {
            _gameDir = gameDir;
            Sectors = new List<TsSector>();
            
        }

        private void ParseCityFiles()
        {
            var defDirectory = Rfs.GetDirectory("def");
            if (defDirectory == null)
            {
                Log.Msg("Could not read 'def' dir");
                return;
            }

            var cityFiles = defDirectory.GetFiles("city");
            if (cityFiles == null)
            {
                Log.Msg("Could not read city files");
                return;
            }

            foreach (var cityFile in cityFiles)
            {
                var data = cityFile.Entry.Read();
                var lines = Encoding.UTF8.GetString(data).Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("@include"))
                    {
                        var path = Helper.GetFilePath(line.Split('"')[1], "def");
                        var city = new TsCity(this, path);
                        if (city.Token != 0 && !_citiesLookup.ContainsKey(city.Token))
                        {
                            _citiesLookup.Add(city.Token, city);
                        }
                    }
                }
            }
        }

        private void ParsePrefabFiles()
        {
            var worldDirectory = Rfs.GetDirectory("def/world");
            if (worldDirectory == null)
            {
                Log.Msg("Could not read 'def/world' dir");
                return;
            }

            var prefabFiles = worldDirectory.GetFiles("prefab");
            if (prefabFiles == null)
            {
                Log.Msg("Could not read prefab files");
                return;
            }

            foreach (var prefabFile in prefabFiles)
            {
                var data = prefabFile.Entry.Read();
                var lines = Encoding.UTF8.GetString(data).Split('\n');

                var token = 0UL;
                var path = "";
                var category = "";

                foreach (var line in lines)
                {
                    if (line.Contains("prefab_model"))
                    {
                        token = ScsHash.StringToToken(line.Split('.')[1].Trim());
                    }
                    else if (line.Contains("prefab_desc"))
                    {
                        path = Helper.GetFilePath(line.Split('"')[1]);
                    }
                    else if (line.Contains("category"))
                    {
                        category = line.Split('"')[1];
                    }

                    if (line.Contains("}") && token != 0 && path != "")
                    {
                        var prefab = new TsPrefab(this, path, token, category);
                        if (prefab.Token != 0 && !_prefabLookup.ContainsKey(prefab.Token))
                        {
                            _prefabLookup.Add(prefab.Token, prefab);
                        }

                        token = 0;
                        path = "";
                        category = "";
                    }
                }
            }
        }

        private void ParseRoadLookFiles()
        {
            var worldDirectory = Rfs.GetDirectory("def/world");
            if (worldDirectory == null)
            {
                Log.Msg("Could not read 'def/world' dir");
                return;
            }

            var roadLookFiles = worldDirectory.GetFiles("road_look");
            if (roadLookFiles == null)
            {
                Log.Msg("Could not read road look files");
                return;
            }

            foreach (var roadLookFile in roadLookFiles)
            {
                var data = roadLookFile.Entry.Read();
                var lines = Encoding.UTF8.GetString(data).Split('\n');
                TsRoadLook roadLook = null;

                foreach (var line in lines)
                {
                    if (line.Contains(":") && roadLook != null)
                    {
                        var value = line.Substring(line.IndexOf(':') + 1).Trim();
                        var key = line.Substring(0, line.IndexOf(':')).Trim();
                        switch (key)
                        {
                            case "lanes_left[]":
                                roadLook.LanesLeft.Add(value);
                                roadLook.IsLocal = (value.Equals("traffic_lane.road.local") || value.Equals("traffic_lane.road.local.tram") || value.Equals("traffic_lane.road.local.no_overtake"));
                                roadLook.IsExpress = (value.Equals("traffic_lane.road.expressway") || value.Equals("traffic_lane.road.divided"));
                                roadLook.IsHighway = (value.Equals("traffic_lane.road.motorway") || value.Equals("traffic_lane.road.motorway.low_density") ||
                                                    value.Equals("traffic_lane.road.freeway") || value.Equals("traffic_lane.road.freeway.low_density") ||
                                                    value.Equals("traffic_lane.road.divided"));
                                roadLook.IsNoVehicles = (value.Equals("traffic_lane.no_vehicles"));
                                break;

                            case "lanes_right[]":
                                roadLook.LanesRight.Add(value);
                                roadLook.IsLocal = (value.Equals("traffic_lane.road.local") || value.Equals("traffic_lane.road.local.tram") || value.Equals("traffic_lane.road.local.no_overtake"));
                                roadLook.IsExpress = (value.Equals("traffic_lane.road.expressway") || value.Equals("traffic_lane.road.divided"));
                                roadLook.IsHighway = (value.Equals("traffic_lane.road.motorway") || value.Equals("traffic_lane.road.motorway.low_density") ||
                                                    value.Equals("traffic_lane.road.freeway") || value.Equals("traffic_lane.road.freeway.low_density") ||
                                                    value.Equals("traffic_lane.road.divided"));
                                roadLook.IsNoVehicles = (value.Equals("traffic_lane.no_vehicles"));
                                break;
                            case "road_offset":
                                float.TryParse(value.Replace('.', ','), out roadLook.Offset);
                                break;
                        }
                    }

                    if (line.Contains("road_look"))
                    {
                        roadLook = new TsRoadLook(ScsHash.StringToToken(line.Split('.')[1].Trim('{').Trim()));
                    }

                    if (line.Contains("}") && roadLook != null)
                    {
                        if (roadLook.Token != 0 && !_roadLookup.ContainsKey(roadLook.Token))
                        {
                            _roadLookup.Add(roadLook.Token, roadLook);
                            roadLook = null;
                        }
                    }
                }
            }
        }

        private void ParseFerryConnections()
        {
            var connectionDirectory = Rfs.GetDirectory("def/ferry/connection");
            if (connectionDirectory == null)
            {
                Log.Msg("Could not read 'def/ferry/connection' dir");
                return;
            }

            var ferryConnectionFiles = connectionDirectory.GetFiles("sii");
            if (ferryConnectionFiles == null)
            {
                Log.Msg("Could not read ferry connection files files");
                return;
            }

            foreach (var ferryConnectionFile in ferryConnectionFiles)
            {
                var data = ferryConnectionFile.Entry.Read();
                var lines = Encoding.UTF8.GetString(data).Split('\n');

                TsFerryConnection conn = null;

                foreach (var line in lines)
                {
                    if (line.Contains(":"))
                    {
                        var value = line.Split(':')[1].Trim();
                        var key = line.Split(':')[0].Trim();
                        if (conn != null)
                        {
                            if (key.Contains("connection_positions"))
                            {
                                var vector = value.Split('(')[1].Split(')')[0];
                                var values = vector.Split(',');
                                var x = float.Parse(values[0].Replace('.', ','));
                                var z = float.Parse(values[2].Replace('.', ','));
                                conn.AddConnectionPosition(x, z);
                            }
                        }

                        if (line.Contains("ferry_connection"))
                        {
                            var portIds = value.Split('.');
                            conn = new TsFerryConnection
                            {
                                StartPortToken = ScsHash.StringToToken(portIds[1]),
                                EndPortToken = ScsHash.StringToToken(portIds[2].TrimEnd('{').Trim())
                            };
                        }
                    }

                    if (!line.Contains("}") || conn == null) continue;;

                    var existingItem = _ferryConnectionLookup.FirstOrDefault(item =>
                        (item.StartPortToken == conn.StartPortToken && item.EndPortToken == conn.EndPortToken) ||
                        (item.StartPortToken == conn.EndPortToken && item.EndPortToken == conn.StartPortToken)); // Check if connection already exists
                    if (existingItem == null) _ferryConnectionLookup.Add(conn);
                    conn = null;
                }
            }
        }

        private void ParseOverlays()
        {
            var uiMapDirectory = Rfs.GetDirectory("material/ui/map");
            if (uiMapDirectory == null)
            {
                Log.Msg("Could not read 'material/ui/map' dir");
                return;
            }

            var matFiles = uiMapDirectory.GetFiles(".mat");
            if (matFiles == null)
            {
                Log.Msg("Could not read .mat files");
                return;
            }

            var uiMapRoadDirectory = Rfs.GetDirectory("material/ui/map/road");
            if (uiMapRoadDirectory != null)
            {
                var data = uiMapRoadDirectory.GetFiles(".mat");
                if (data != null) matFiles.AddRange(data);
            }
            else
            {
                Log.Msg("Could not read 'material/ui/map/road' dir");
            }

            var uiCompanyDirectory = Rfs.GetDirectory("material/ui/company/small");
            if (uiCompanyDirectory != null)
            {
                var data = uiCompanyDirectory.GetFiles(".mat");
                if (data != null) matFiles.AddRange(data);
            }
            else
            {
                Log.Msg("Could not read 'material/ui/company/small' dir");
            }

            foreach (var matFile in matFiles)
            {
                var data = matFile.Entry.Read();
                var lines = Encoding.UTF8.GetString(data).Split('\n');

                foreach (var line in lines)
                {
                    if (line.Contains("texture") && !line.Contains("_name"))
                    {
                        var tobjPath = Helper.CombinePath(matFile.GetLocalPath(), line.Split('"')[1]);

                        var tobjData = Rfs.GetFileEntry(tobjPath).Entry.Read();

                        var pathLength = BitConverter.ToInt32(tobjData, 0x28);
                        var path = Helper.GetFilePath(Encoding.UTF8.GetString(tobjData, 0x30, pathLength));

                        var name = matFile.GetFileName();
                        if (name.StartsWith("road_")) name = name.Substring(5);

                        var token = ScsHash.StringToToken(name);
                        if (!_overlayLookup.ContainsKey(token))
                        {
                            _overlayLookup.Add(token, new TsMapOverlay(this, path));
                        }

                    }
                }
            }
        }

        /// <summary>
        /// Parse all definition files
        /// </summary>
        private void ParseDefFiles()
        {
            ParseCityFiles();
            ParsePrefabFiles();
            ParseRoadLookFiles();
            ParseFerryConnections();
            ParseOverlays();
        }

        /// <summary>
        /// Parse all .base files
        /// </summary>
        private void ParseMapFiles()
        {
            var baseMapEntry = Rfs.GetDirectory("map");
            if (baseMapEntry == null)
            {
                Log.Msg("Could not read 'map' dir");
                return;
            }

            var mbd = baseMapEntry.Files.Values.FirstOrDefault(x => x.GetExtension().Equals("mbd")); // Get the map name from the mbd file
            if (mbd == null)
            {
                Log.Msg("Could not find mbd file");
                return;
            }

            var mapName = mbd.GetFileName();

            var mapFileDir = Rfs.GetDirectory($"map/{mapName}");
            if (mapFileDir == null)
            {
                Log.Msg($"Could not read 'map/{mapName}' directory");
                return;
            }

            _sectorFiles = mapFileDir.GetFiles(".base").Select(x => x.GetPath()).ToList();
        }

        /// <summary>
        /// Loads navigation inside TsItem objects
        /// </summary>
        private void LoadNavigation() {
            foreach (var prefab in Prefabs)
            {
                foreach (var nodeStr in prefab.Nodes)
                {
                    var node = GetNodeByUid(nodeStr);
                    TsItem road = null;
                    TsNode precnode = node;
                    TsItem precitem = prefab;
                    TsNode nextnode;
                    TsItem nextitem;
                    List<TsItem> roads = new List<TsItem>();
                    var totalLength = 0.0f;
                    if (node.ForwardItem != null && node.ForwardItem.Type == TsItemType.Road) {
                        road = node.ForwardItem;
                    } else if (node.BackwardItem != null && node.BackwardItem.Type == TsItemType.Road) {
                        road = node.BackwardItem;
                    }
                    if (road != null)
                    {
                        int direction = 0;
                        if (road.EndNodeUid == node.Uid) direction = 1;
                        while (road != null && road.Type != TsItemType.Prefab && !road.Hidden)
                        {
                            var length = (float)Math.Sqrt(Math.Pow(GetNodeByUid(road.StartNodeUid).X - GetNodeByUid(road.EndNodeUid).X, 2) + Math.Pow(GetNodeByUid(road.StartNodeUid).Z - GetNodeByUid(road.EndNodeUid).Z, 2));
                            TsRoadItem roadObj = (TsRoadItem) road;
                            if (roadObj.RoadLook.IsHighway)  totalLength += (length / 4) / roadObj.RoadLook.GetWidth();
                            else if (roadObj.RoadLook.IsLocal) totalLength += (length / 3) / roadObj.RoadLook.GetWidth();
                            else if (roadObj.RoadLook.IsExpress) totalLength += (length / 2) / roadObj.RoadLook.GetWidth();
                            else length += length * 2;
                            roads.Add(road);
                            if (GetNodeByUid(road.StartNodeUid) == precnode)
                            {
                                nextnode = GetNodeByUid(road.EndNodeUid);
                                precnode = GetNodeByUid(road.EndNodeUid);
                            }
                            else
                            {
                                nextnode = GetNodeByUid(road.StartNodeUid);
                                precnode = GetNodeByUid(road.StartNodeUid);
                            }
                            if (nextnode.BackwardItem == road || nextnode.BackwardItem == precitem)
                            {
                                nextitem = nextnode.ForwardItem;
                                precitem = nextnode.ForwardItem;
                            }
                            else
                            {
                                nextitem = nextnode.BackwardItem;
                                precitem = nextnode.BackwardItem;
                            }
                            road = nextitem;
                        }
                        if (road != null && !road.Hidden)
                        {
                            TsPrefabItem prevPrefab = (TsPrefabItem)prefab;
                            TsPrefabItem nextPrefab = (TsPrefabItem)road;
                            TsRoadLook look = ((TsRoadItem)roads.LastOrDefault()).RoadLook;
                            if (prevPrefab.Hidden || nextPrefab.Hidden) continue;
                            if (prevPrefab.Navigation.ContainsKey(nextPrefab) == false && (look.IsBidirectional() || direction == 0))
                            {
                                prevPrefab.Navigation.Add(nextPrefab,new Tuple<float, List<TsItem>>(totalLength , roads));
                            }
                            if (nextPrefab.Navigation.ContainsKey(prevPrefab) == false && (look.IsBidirectional() || direction == 1))
                            {
                                var reverse = new List<TsItem>(roads);
                                reverse.Reverse();
                                nextPrefab.Navigation.Add(prevPrefab,new Tuple<float, List<TsItem>>(totalLength , reverse));
                            }
                        }
                    }
                    else if (node.ForwardItem != null && node.BackwardItem != null)
                    {
                        TsPrefabItem forwardPrefab = (TsPrefabItem)node.ForwardItem;
                        TsPrefabItem backwardPrefab = (TsPrefabItem)node.BackwardItem;
                        if (forwardPrefab.Navigation.ContainsKey(backwardPrefab) == false)
                        {
                            forwardPrefab.Navigation.Add(backwardPrefab,new Tuple<float, List<TsItem>>(0, null));
                        }
                        if (backwardPrefab.Navigation.ContainsKey(forwardPrefab) == false)
                        {
                            backwardPrefab.Navigation.Add(forwardPrefab,new Tuple<float, List<TsItem>>(0, null));
                        }
                    }
                   
                }
            }

            Dictionary<ulong,TsPrefabItem> ferryToPrefab = new Dictionary<ulong,TsPrefabItem>();
            foreach (var port in FerryConnections)
            {
                float min = float.MaxValue;
                TsPrefabItem closerPrefab = null;
                foreach (var prefab in Prefabs)
                {
                    float distance = (float)Math.Sqrt(Math.Pow(port.X - prefab.X, 2) + Math.Pow(port.Z - prefab.Z, 2));
                    if (distance < min && prefab.Navigation.Count > 1 && !prefab.Hidden) {
                        min = distance;
                        closerPrefab = prefab;
                    }
                }
                ferryToPrefab[port.FerryPortId] = closerPrefab;
            }
            foreach (var port in FerryConnections)
            {
                foreach (var connection in LookupFerryConnection(port.FerryPortId))
                {
                    ferryToPrefab[connection.StartPortToken].Navigation.Add(ferryToPrefab[connection.EndPortToken],new Tuple<float,List<TsItem>>(0,new List<TsItem>()));
                    ferryToPrefab[connection.EndPortToken].Navigation.Add(ferryToPrefab[connection.StartPortToken],new Tuple<float,List<TsItem>>(0,new List<TsItem>()));
                }
            }
        }

        /// <summary>
        /// Calculate path between two prefabs with Dijkstra's shortest path algorithm (Needs improvement with A* Algorithm)
        /// </summary>
        private void CalculatePath(TsPrefabItem Start, TsPrefabItem End) {
            Dictionary<TsPrefabItem, Tuple<float, TsPrefabItem>> nodeMap = new Dictionary<TsPrefabItem, Tuple<float, TsPrefabItem>>();
            Dictionary<TsPrefabItem,bool> walkedNodes = new Dictionary<TsPrefabItem,bool>();

            foreach (var node in Prefabs)
            {
                nodeMap.Add(node, new Tuple<float, TsPrefabItem>(float.MaxValue, null));
            }
            
            if (!nodeMap.ContainsKey((TsPrefabItem)Start)) return;
            if (!nodeMap.ContainsKey((TsPrefabItem)End)) return;
            
            nodeMap[Start] = new Tuple<float, TsPrefabItem>(0, null);
           
            while (walkedNodes.Count != nodeMap.Count)
            {
                float distanceWalked = float.MaxValue;
                TsPrefabItem toWalk = null;
                foreach (var node in nodeMap)
                {
                    var dTmp = node.Value.Item1;
                    if ( distanceWalked > dTmp && !walkedNodes.ContainsKey(node.Key))
                    {
                        distanceWalked = dTmp;
                        toWalk = node.Key;
                    }
                }
                if (toWalk == null) break;

                walkedNodes[toWalk] = true;

                if (toWalk.Uid == End.Uid) break;

                var currentWeight = nodeMap[toWalk].Item1 ;//+ toWalk.Prefab.indicativeWeight / 25;

                foreach (var jump in toWalk.Navigation)
                {
                    var newWeight = jump.Value.Item1 + currentWeight;
                    TsPrefabItem newNode = jump.Key;

                    if (nodeMap[toWalk].Item2 != null) {
                        TsPrefabItem precPrefab = nodeMap[toWalk].Item2;
                        TsPrefabItem middlePrefab = toWalk;
                        List<TsItem> precRoad = null;
                        while (precRoad == null && precPrefab != null) {
                            precRoad = precPrefab.Navigation[middlePrefab].Item2;
                            middlePrefab = precPrefab;
                            precPrefab = nodeMap[precPrefab].Item2;
                        }
                        var nextRoad = toWalk.Navigation[newNode].Item2;
                        if (precRoad != null && nextRoad != null && (precRoad.Count != 0 && nextRoad.Count != 0) && !SetInternalRoutePrefab((TsRoadItem)precRoad.LastOrDefault(),(TsRoadItem)nextRoad[0])) continue;
                    }

                    if (!walkedNodes.ContainsKey(newNode) && nodeMap[newNode].Item1 > newWeight) nodeMap[newNode] = new Tuple<float, TsPrefabItem>(newWeight, toWalk);
                }
            }

            TsPrefabItem route = End;

            while (route != null)
            {
                var gotoNew = nodeMap[route].Item2;
                if (gotoNew == null) break;
                if (gotoNew.Navigation.ContainsKey(route) && gotoNew.Navigation[route].Item2 != null) {
                    for (int i=gotoNew.Navigation[route].Item2.Count-1;i>=0;i--)
                    {
                        RouteRoads.Add((TsRoadItem)gotoNew.Navigation[route].Item2[i]);
                    }
                }

                route = gotoNew;
            }

            RouteRoads.Reverse();
        }

        /// <summary>
        /// Even if prefabs roads are already been calculated these could be dirty so it recalculate them using the roads
        /// NEEDS IMPROVEMENT
        /// </summary>
        public void CalculatePrefabsPath() {
            RoutePrefabs.Clear();
            PrefabNav.Clear();
            for (int i = 0; i < RouteRoads.Count-1; i++)
            {
                SetInternalRoutePrefab(RouteRoads[i],RouteRoads[i+1]);
            }
        }

        /// <summary>
        /// Add Items in Items Dictionary
        /// </summary>
        private void SetItems() {
            foreach (var item in Roads) Items.Add(item.Uid, item);
            foreach (var item in Prefabs) Items.Add(item.Uid, item);
            foreach (var item in Cities) Items.Add(item.Uid, item);
            foreach (var item in MapOverlays) Items.Add(item.Uid, item);
            foreach (var item in FerryConnections) Items.Add(item.Uid, item);
            foreach (var item in Companies) Items.Add(item.Uid, item);
        }

        /// <summary>
        /// Set ForwardItem and BackwardItem in TsNodes
        /// </summary>
        private void SetForwardBackward() {
            foreach (var node in Nodes)
            {
                TsItem item = null;
                if (Items.TryGetValue(node.Value.ForwardItemUID,out item)) {
                    node.Value.ForwardItem = item; 
                }
                item = null;
                 if (Items.TryGetValue(node.Value.BackwardItemUID,out item)) {
                    node.Value.BackwardItem = item; 
                }
                
            }
        }

        /// <summary>
        /// Given two roads it search for a path that are inside prefabs between them using a DFS search
        /// </summary>
        private bool SetInternalRoutePrefab(TsRoadItem Start,TsRoadItem End) {
            TsNode startNode = null;
            Dictionary<TsPrefabItem,bool> visited = new Dictionary<TsPrefabItem,bool>();
            Stack<List<Tuple<TsNode,TsPrefabItem>>> prefabsToCheck = new Stack<List<Tuple<TsNode,TsPrefabItem>>>();
            List<List<Tuple<TsNode,TsPrefabItem>>> possiblePaths = new List<List<Tuple<TsNode,TsPrefabItem>>>();
            if (GetNodeByUid(Start.StartNodeUid).BackwardItem.Type == TsItemType.Prefab || GetNodeByUid(Start.StartNodeUid).ForwardItem.Type == TsItemType.Prefab) {
                startNode = GetNodeByUid(Start.StartNodeUid);
                var prefab = startNode.BackwardItem.Type == TsItemType.Prefab ? (TsPrefabItem)startNode.BackwardItem : (TsPrefabItem)startNode.ForwardItem;
                var temp = new List<Tuple<TsNode,TsPrefabItem>>();
                temp.Add(new Tuple<TsNode,TsPrefabItem>(startNode,prefab));
                prefabsToCheck.Push(temp);
            }
            if (GetNodeByUid(Start.EndNodeUid).BackwardItem.Type == TsItemType.Prefab || GetNodeByUid(Start.EndNodeUid).ForwardItem.Type == TsItemType.Prefab) {
                startNode = GetNodeByUid(Start.EndNodeUid);
                var prefab = startNode.BackwardItem.Type == TsItemType.Prefab ? (TsPrefabItem)startNode.BackwardItem : (TsPrefabItem)startNode.ForwardItem;
                var temp = new List<Tuple<TsNode,TsPrefabItem>>();
                temp.Add(new Tuple<TsNode,TsPrefabItem>(startNode,prefab));
                prefabsToCheck.Push(temp);
            }
            while (prefabsToCheck.Count != 0) {
                List<Tuple<TsNode,TsPrefabItem>> actualPath = prefabsToCheck.Pop();
                Tuple<TsNode,TsPrefabItem> actualPrefab = actualPath.LastOrDefault();

                if (visited.ContainsKey(actualPrefab.Item2)) continue;
                visited[actualPrefab.Item2] = true;
                
                var lastNode = actualPrefab.Item2.NodeIteminPrefab(this,End);
                if (lastNode != null) {
                    actualPath.Add(new Tuple<TsNode,TsPrefabItem>(lastNode,null));
                    possiblePaths.Add(actualPath);
                    continue;
                }

                foreach (var prefab in actualPrefab.Item2.NodePrefabinPrefab(this)) {
                    var newPath = new List<Tuple<TsNode,TsPrefabItem>>(actualPath);
                    newPath.Add(prefab);
                    prefabsToCheck.Push(newPath);
                }
            }
            foreach (var path in possiblePaths)
            {
                bool success = true;
                for (int i = 0; i < path.Count - 1; i++)
                {
                    if (!AddPrefabPath(path[i].Item2,path[i].Item1,path[i+1].Item1)) {
                        success = false;
                        break;
                    }
                }
                if (success && path.Count >= 1) return true;
            }
            return false;
        }

        /// <summary>
        /// Parse through all .scs files and retreive all necessary files
        /// </summary>
        public void Parse()
        {
            var startTime = DateTime.Now.Ticks;
            
            Log.Msg("Loading elements...");
            if (!Directory.Exists(_gameDir))
            {
                Log.Msg("Could not find Game directory.");
                return;
            }

            try
            {
                Rfs = new RootFileSystem(_gameDir);
            }
            catch (FileNotFoundException e)
            {
                Log.Msg(e.Message);
                return;
            }
            

            ParseDefFiles();
            ParseMapFiles();

            
            if (_sectorFiles == null) return;

            var preMapParseTime = DateTime.Now.Ticks;
            Sectors = _sectorFiles.Select(file => new TsSector(this, file)).ToList();
            Sectors.ForEach(sec => sec.Parse());
            Sectors.ForEach(sec => sec.ClearFileData());
            SetItems();
            SetForwardBackward();

            Log.Msg("Loading navigation data...");
            LoadNavigation();

            Random r = new Random();
            int firstP = r.Next(Prefabs.Count);
            int secondP = r.Next(Prefabs.Count);
            while (Prefabs[firstP].Hidden || Prefabs[firstP].Navigation.Count < 0) firstP++;
            while (Prefabs[secondP].Hidden || Prefabs[secondP].Navigation.Count < 1) secondP++;
            Log.Msg("Starting Calculating Path...");
            CalculatePath(Prefabs[firstP],Prefabs[secondP]);
            Log.Msg("Starting Calculating Path inside Prefabs...");
            CalculatePrefabsPath();

            Log.Msg("Selected Roads: " + RouteRoads.Count + " - Selected Prefabs: "+ PrefabNav.Count);
            Log.Msg("Start Location: X -> " + Prefabs[firstP].X + ";Z -> " + Prefabs[firstP].Z);
            Log.Msg("End Location: X -> " + Prefabs[secondP].X + ";Z -> " + Prefabs[secondP].Z);
            Log.Msg($"It took {(DateTime.Now.Ticks - preMapParseTime) / TimeSpan.TicksPerMillisecond} ms to parse all (*.base)" +
                    $" map files and {(DateTime.Now.Ticks - startTime) / TimeSpan.TicksPerMillisecond} ms total.");
        }


        public TsRoadLook LookupRoadLook(ulong lookId)
        {
            return _roadLookup.ContainsKey(lookId) ? _roadLookup[lookId] : null;
        }

        public TsPrefab LookupPrefab(ulong prefabId)
        {
            return _prefabLookup.ContainsKey(prefabId) ? _prefabLookup[prefabId] : null;
        }

        public TsCity LookupCity(ulong cityId)
        {
            return _citiesLookup.ContainsKey(cityId) ? _citiesLookup[cityId] : null;
        }

        public TsMapOverlay LookupOverlay(ulong overlayId)
        {
            return _overlayLookup.ContainsKey(overlayId) ? _overlayLookup[overlayId] : null;
        }

        public List<TsFerryConnection> LookupFerryConnection(ulong ferryPortId)
        {
            return _ferryConnectionLookup.Where(item => item.StartPortToken == ferryPortId).ToList();
        }
        
        public TsNode GetNodeByUid(ulong uid)
        {
            return Nodes.ContainsKey(uid) ? Nodes[uid] : null;
        }

        public void AddFerryPortLocation(ulong ferryPortId, float x, float z)
        {
            var ferry = _ferryConnectionLookup.Where(item => item.StartPortToken == ferryPortId || item.EndPortToken == ferryPortId);
            foreach (var connection in ferry)
            {
                connection.SetPortLocation(ferryPortId, x, z);
            }
        }

        public bool AddPrefabPath(TsPrefabItem prefab,TsNode startNode,TsNode endNode) {
            //Optional - some prefabs, like gas stastions will be completely selected instead of selecting a sigle road
            //if (prefab.Prefab.PrefabNodes.Count <= 2) {
            //    RoutePrefabs.Add(prefab);
            //    return true;
            //}
            var f = prefab.GetNearestNode(this,startNode,0);
            var e = prefab.GetNearestNode(this,endNode,1);
            if (f.id == -1 || e.id == -1) return false;
            if (prefab.Prefab.NavigationRoutes.ContainsKey(new Tuple<TsPrefabNode,TsPrefabNode>(f,e))) PrefabNav[prefab] = prefab.Prefab.NavigationRoutes[new Tuple<TsPrefabNode,TsPrefabNode>(f,e)].Item1;
            else return false;
            return true;
            // TODO: Add the possibility to return also the weight of the path to be used in Dijkstra's Algorithm
        }

    }
}
