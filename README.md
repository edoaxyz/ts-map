## ts-map with navigation mode

<b>This is the first time I ever programmed in C#.</b>

This is a fork of the original ts-map project that includes a navigation mode, giving the possibility to calculate the best path between two prefab items and selecting not only roads but also roads inside prefabs.

### How does it calculate paths?
Using Dijkstra's Algorithm. Every single prefab item has a dictionary that contains information of near and reachable prefabs from that specific prefab.

### Why is it so slow?
Because Dijkstra's Algorithm explore all the nodes(prefabs) and since ETS2 currently has nearly 20000 nodes it requires some time to find the best path. There's another algorithm called A* that explore nodes that are more near to the destination calculating an heuristic distance which I think can immediately find a valid path in some milliseconds. I hope to implement it soon.

### How to choose start and end prefabs?

Currently these are choosed randomly since I didn't want to deal with C# UI. The idea was to put three button: the first and the second for enabling a mode where the user can select respectively a start prefab and an end prefab in the visualized area; the third starts the calculation.
