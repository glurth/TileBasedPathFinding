using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public partial class Topology
{
    [System.Serializable]
    public partial class Node
    {
        public string name;
        // Ordered sequence of in and outbound edge indices (integer index into Topo.edges)
        // nodeA in all referenced outbound edges, should be THIS node.
        // nodeB in all referenced inbound edges, should be THIS node.
        // The order in the sequnce refers to where on the node's path these edges are, relative to the other edges.
        //Some nodes, like "start" or "end" may have only 1 edge.
        public int[] edgeSequence;
        [System.NonSerialized]
        Topology map;
        public readonly int mapIndex;

        Dictionary<int, int> mapToSeqCache=null;
        public int MapEdgeIndexToSeqIndex(int mapIndex)
        {
            if (mapToSeqCache == null)
            {
                mapToSeqCache = new Dictionary<int, int>();
                for (int i = 0; i < edgeSequence.Length; i++)
                {
                    mapToSeqCache.Add(edgeSequence[i], i);
                }
            }
            if (mapToSeqCache.TryGetValue(mapIndex, out int retVal))
            {
                return retVal;
            }
            throw new System.Exception("Invalid MapEdgeIndexToSeqIndex lookup: edge doe not exist in node."); 
            //return -1;
        }

        public Edge EdgeInSequence(int index)
        {
            return map.edges[edgeSequence[index]];
        }

        public Node(Topology map, int mapNodeIndex, string name, int[] edgeSequence)
        {
            this.edgeSequence = edgeSequence;
            this.map = map;
            this.name= name;
            mapIndex = mapNodeIndex;
        }
    }

    /// <summary>
    /// Edges are unidirection: a sequential (in node) reversed PAIR of edges is required for a two-way edge.
    /// </summary>
    [System.Serializable]
    public partial class Edge
    {

        public readonly int nodeAidx;
        public readonly int nodeBidx;
        public readonly bool isTeleport; //alleviates topological requirements (can cross other edges)
        [System.NonSerialized]
        Topology map;
        public readonly int mapIndex;
        
        public Node NodeA => map.nodes[nodeAidx];
        public Node NodeB => map.nodes[nodeBidx];

        public Edge(Topology map, int mapEdgeIndex, int nodeAidx, int nodeBidx, bool isTeleport=false)
        {
            this.nodeAidx = nodeAidx;
            this.nodeBidx = nodeBidx;
            this.map = map;
            this.isTeleport = isTeleport;
            mapIndex = mapEdgeIndex;
        }

        public bool isPartOfTwoWayEdge(out Edge otherEdge)
        {
            int seqA = NodeA.MapEdgeIndexToSeqIndex(mapIndex);
            int seqB = NodeB.MapEdgeIndexToSeqIndex(mapIndex);
            Edge checkEdge = null;
            int testPassCount = 0;

            if (CheckSeqIdx(seqA - 1, NodeA)) testPassCount++;
            if (CheckSeqIdx(seqA + 1, NodeA)) testPassCount++;

            if (CheckSeqIdx(seqB - 1, NodeB)) testPassCount++;
            if (CheckSeqIdx(seqB + 1, NodeB)) testPassCount++;
            
            if (testPassCount > 1)
            {
                otherEdge = checkEdge;
                return true;
            }
            otherEdge = null;
            return false;

            bool CheckSeqIdx(int checkSeqIdx, Node inNode)
            {
                if (checkSeqIdx >= 0 && checkSeqIdx < inNode.edgeSequence.Length)//valid in range
                {
                    checkEdge = inNode.EdgeInSequence(checkSeqIdx);//get edge
                    if (checkEdge.nodeBidx == nodeAidx && checkEdge.nodeAidx == nodeBidx)// both nodes refs are equal but opposite
                        return true;
                    
                }
                return false;
            }
        }
    }

    [SerializeField]
    public Node[] nodes;
    [SerializeField]
    public Edge[] edges;

    public bool EdgesShareBothNodesAndAreSequentialInThem(Edge edgeA, Edge edgeB)
    {
        if (edgeA.nodeAidx == edgeB.nodeAidx || edgeA.nodeAidx == edgeB.nodeBidx)// is edgeA, nodeA, shared?
        {
            if (edgeA.nodeBidx == edgeB.nodeAidx || edgeA.nodeBidx == edgeB.nodeBidx)// is edgeA, nodeB, shared?
            {
                Node node1 = edgeA.NodeA;

                Node node2 = edgeA.NodeB;
                int seqIndexA1 = node1.MapEdgeIndexToSeqIndex(edgeA.mapIndex);
                int seqIndexB1 = node1.MapEdgeIndexToSeqIndex(edgeB.mapIndex);
                if (Mathf.Abs(seqIndexA1 - seqIndexB1) < 2)
                {
                    int seqIndexA2 = node2.MapEdgeIndexToSeqIndex(edgeA.mapIndex);
                    int seqIndexB2 = node2.MapEdgeIndexToSeqIndex(edgeB.mapIndex);
                    return Mathf.Abs(seqIndexA2 - seqIndexB2) < 2;
                }
            }
        }
        return false;
    }
}

//geometric
public partial class Topology
{
    int[] fixedNodes;//forces applied to these nodes will not move them.
    public void SetFixedNodes(int[] fix)
    {
        fixedNodes = fix;
    }
    HashSet<int> fixedNodesCache=null;
    public bool isNodeFixed(int mapIndex)
    {
        if (fixedNodesCache == null)
        {
            fixedNodesCache = new HashSet<int>();
            foreach (int i in fixedNodes)
                fixedNodesCache.Add(i);
        }
        return fixedNodesCache.Contains(mapIndex);
    }

    public void SetupTopo(Node[] nodes, Edge[] edges, int[] fixedNodes)
    {
        this.nodes = nodes;
        this.edges = edges;
        this.fixedNodes = fixedNodes;
       // if (!IsAnalyticallyPlanar())
         //   Debug.LogError("Topology fails planar test");
        //throw new System.Exception("Topology fails planar test");
    }

    //geometric layout information
    public partial class Node
    {

        public Vector2 uvPosition;
        public float rotationAngleRad;
        static public float defaultDistanceBetweenEdges = 0.01f;
        public float[] edgeSpacing;
        public float drawnLength
        {
            get
            {
                if (edgeSequence.Length <= 1)
                    return defaultDistanceBetweenEdges;

                return totalLength;
            }
        }

        float totalLength
        {
            get
            {
                if (edgeSpacing == null || edgeSpacing.Length == 0)
                    return defaultDistanceBetweenEdges * (edgeSequence.Length - 1);

                float sum = 0f;
                for (int i = 0; i < edgeSpacing.Length; i++)
                    sum += edgeSpacing[i];

                return sum;
            }
        }

        float GetEdgeConnectionOffsetSignedDist(int edgeSeqIndex)
        {
            if (edgeSequence.Length <= 1)
                return 0f;

            float offsetFromStart = 0f;

            if (edgeSpacing == null || edgeSpacing.Length == 0)
            {
                offsetFromStart = defaultDistanceBetweenEdges * edgeSeqIndex;
            }
            else
            {
                for (int i = 0; i < edgeSeqIndex; i++)
                    offsetFromStart += edgeSpacing[i];
            }

            return offsetFromStart - totalLength * 0.5f;
        }
        public Vector2 GetEdgeConnectionOffset(int edgeSeqIndex)
        {
            Vector2 offset = new Vector2(Mathf.Cos(rotationAngleRad), Mathf.Sin(rotationAngleRad));
            return offset * GetEdgeConnectionOffsetSignedDist(edgeSeqIndex);
        }
        public Vector2 GetEdgeConnectionPosition(int edgeSeqIndex)
        {
            return uvPosition + GetEdgeConnectionOffset(edgeSeqIndex);
        }
    }
    //layout iteration state information
    public partial class Node
    {
        //applies forces will modify these values
        Vector2 velocity;
        float angularVelocity;
        // called at the start(or end if after Update) of each iteration "frame"
        public void Reset()
        {
            velocity = Vector2.zero;
            angularVelocity = 0;
        }
        // called at the end of each iteration "frame"
        public void UpdatePositionAndRotation(bool thenReset = true)
        {
            if (map.isNodeFixed(mapIndex)) return;
            uvPosition += velocity;
            rotationAngleRad += angularVelocity;
            if (thenReset)
                Reset();
        }
        // called by layout iterator
        public void ApplyForce(Vector2 force)
        {
            //we will use mass of 1, and time unit of 1
            velocity += force;
        }
        // called by layout iterator
        public void ApplyForceToEdgeConnection(int seqIndex,Vector2 force)
        {
            //we will use mass of 1, and time unit of 1
            velocity += force;
            //we will assume moment of inertia 1
            angularVelocity += Cross(GetEdgeConnectionOffset(seqIndex), force);
            float Cross(Vector2 a, Vector2 b)
            {
                return a.x * b.y - a.y * b.x;
            }
        }
        public void ApplyForceAtPosition(Vector2 position, Vector2 force)
        {
            //we will use mass of 1, and time unit of 1
            velocity += force;
            //we will assume moment of inertia 1
            angularVelocity += 100f * Cross(position-uvPosition, force);
            float Cross(Vector2 a, Vector2 b)
            {
                return a.x * b.y - a.y * b.x;
            }
        }
        // called by layout iterator
        public void ApplyTorqueToNode(float torque)
        {
            angularVelocity += 100f * torque;
        }
        public void ApplyDamping(float amount)
        {
            velocity *= (1f - amount);
            angularVelocity *= (1f - amount);
        }
    }
    //geometric layout information
    public partial class Edge
    {
        public Vector2 GetNodeAConnectionPosition()
        {
            Node node = NodeA;
            int nodeSeqIndex = node.MapEdgeIndexToSeqIndex(mapIndex);
            return node.GetEdgeConnectionPosition(nodeSeqIndex);
        }
        public Vector2 GetNodeBConnectionPosition()
        {
            Node node = NodeB;
            int nodeSeqIndex = node.MapEdgeIndexToSeqIndex(mapIndex);
            return node.GetEdgeConnectionPosition(nodeSeqIndex);
        }
        public Vector2 GetDirectionFromNode(Node node)
        {
            if (node == NodeA)
            {
                Vector2 d = NodeB.uvPosition - NodeA.uvPosition;
                float len = d.magnitude;
                if (len < 0.0001f) return Vector2.zero;
                return d / len;
            }
            else if (node == NodeB)
            {
                Vector2 d = NodeA.uvPosition - NodeB.uvPosition;
                float len = d.magnitude;
                if (len < 0.0001f) return Vector2.zero;
                return d / len;
            }

            // invalid usage — node not part of this edge
            return Vector2.zero;
        }
    }

    /// <summary>
    /// Parses a textual topology description into a Topology.
    ///
    /// FORMAT
    /// ======
    ///
    /// One node definition per line:
    ///
    ///     NodeName : Connection [, Connection ...]
    ///
    /// Whitespace is ignored.
    ///
    ///
    /// BIDIRECTIONAL CONNECTIONS
    /// =========================
    ///
    /// A bidirectional connection is specified by listing the same
    /// connection on both endpoint nodes.
    ///
    ///     MainBranch:SecondaryBranch
    ///     SecondaryBranch:MainBranch
    ///
    ///
    /// Multiple physical connections between the same two nodes are
    /// distinguished with an edge identifier:
    ///
    ///     MainBranch:
    ///         SecondaryBranch[left],
    ///         DeadEnd1,
    ///         SecondaryBranch[right]
    ///
    ///     SecondaryBranch:
    ///         MainBranch[left],
    ///         DeadEnd2,
    ///         MainBranch[right]
    ///
    ///
    /// ONE-WAY CONNECTIONS
    /// ===================
    ///
    /// A one-way connection is specified by matching a '>' declaration
    /// (edge source) with a '<' declaration (edge destination).
    ///
    ///     MainBranch:
    ///         MainBranch[return]<
    ///
    ///     SecondaryBranch:
    ///         MainBranch[return]>
    ///
    /// This creates:
    ///
    ///     SecondaryBranch -> MainBranch
    ///
    ///
    /// EDGE IDENTIFIERS
    /// ================
    ///
    /// The identifier inside [...]
    /// distinguishes multiple physical connections between the same pair
    /// of nodes.
    ///
    /// Identifiers are only required when multiple connections exist
    /// between the same node pair or for one-way edges.
    ///
    ///
    /// ORDERING
    /// ========
    ///
    /// The order connections appear on each node defines that node's
    /// edgeSequence.
    ///
    /// Every generated Edge is inserted into the edgeSequence of both
    /// endpoint nodes at the declared position.
    ///
    ///
    /// EXAMPLE 1
    /// =========
    ///
    /// Input:
    ///
    ///     Start:MainBranch
    ///
    ///     MainBranch:
    ///         Start,
    ///         DeadEnd1,
    ///         SecondaryBranch[left],
    ///         End,
    ///         SecondaryBranch[right]
    ///
    ///     SecondaryBranch:
    ///         MainBranch[left],
    ///         DeadEnd2,
    ///         MainBranch[right],
    ///         MainBranch[return]>
    ///
    ///     DeadEnd1:MainBranch
    ///     DeadEnd2:SecondaryBranch
    ///     End:MainBranch
    ///
    ///     MainBranch:
    ///         ...
    ///         MainBranch[return]<
    ///
    /// Creates:
    ///
    ///     Start <-> MainBranch
    ///     MainBranch <-> DeadEnd1
    ///     MainBranch <-> SecondaryBranch (left)
    ///     MainBranch <-> SecondaryBranch (right)
    ///     MainBranch <-> End
    ///     SecondaryBranch -> MainBranch (return)
    ///
    /// 
    /// 
    /// Same output for:
    /// Start:MainBranch
    /// MainBranch:
    ///     Start,
    ///     DeadEnd1,
    ///     SecondaryBranch[left],
    ///     End,
    ///     SecondaryBranch[right]
    /// 
    /// SecondaryBranch:
    ///     MainBranch[left],
    ///     DeadEnd2,
    ///     MainBranch[right],
    ///
    /// EXAMPLE 2
    /// =========
    ///
    /// Input:
    ///
    ///     A:B[left],B[right]
    ///     B:A[left],A[right]
    ///
    /// Creates two independent bidirectional edge pairs between A and B,
    /// with ordering preserved independently on both nodes.
    /// </summary>
    public static Topology Parse(string text)
    {
        string[] lines = text.Split(new[] { '\r', '\n' },
            System.StringSplitOptions.RemoveEmptyEntries);

        Dictionary<string, int> nodeLookup = new Dictionary<string, int>();
        List<string> nodeNames = new List<string>();

        int GetNode(string name)
        {
            name = name.Trim();

            if (!nodeLookup.TryGetValue(name, out int idx))
            {
                idx = nodeNames.Count;
                nodeNames.Add(name);
                nodeLookup[name] = idx;
            }

            return idx;
        }

        // key -> (a, b, hasA, hasB)
        Dictionary<string, (int a, int b, bool hasA, bool hasB)> edges =
            new Dictionary<string, (int, int, bool, bool)>();

        string MakeKey(int a, int b, string label)
        {
            if (string.IsNullOrEmpty(label))
                return (a < b) ? $"{a}:{b}" : $"{b}:{a}";

            int min = Mathf.Min(a, b);
            int max = Mathf.Max(a, b);
            return $"{min}:{max}:{label}";
        }

        List<List<int>> sequences = new List<List<int>>();

        Topology topo = new Topology();

        // -------- PARSE --------
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;

            string[] parts = line.Split(':');
            if (parts.Length != 2)
                throw new System.Exception("Bad line: " + line);

            int from = GetNode(parts[0].Trim());

            while (sequences.Count <= from)
                sequences.Add(new List<int>());

            string[] tokens = parts[1].Split(',');

            foreach (string tRaw in tokens)
            {
                string t = tRaw.Trim();
                if (t.Length == 0) continue;

                bool isOut = false;
                bool isIn = false;

                if (t.EndsWith(">"))
                {
                    isOut = true;
                    t = t.Substring(0, t.Length - 1).Trim();
                }
                else if (t.EndsWith("<"))
                {
                    isIn = true;
                    t = t.Substring(0, t.Length - 1).Trim();
                }

                string label = null;
                int lb = t.IndexOf('[');
                int rb = t.IndexOf(']');

                string nodeName = t;

                if (lb >= 0 && rb > lb)
                {
                    nodeName = t.Substring(0, lb).Trim();
                    label = t.Substring(lb + 1, rb - lb - 1).Trim();
                }

                int to = GetNode(nodeName);

                while (sequences.Count <= to)
                    sequences.Add(new List<int>());

                string key = MakeKey(from, to, label);

                if (!edges.TryGetValue(key, out var e))
                {
                    e = (from, to, false, false);
                }

                if (!isOut && !isIn)
                {
                    e = (e.Item1, e.Item2, true, true);
                }
                else if (isOut)
                {
                    e = (e.Item1, e.Item2, true, e.Item4);
                }
                else if (isIn)
                {
                    e = (e.Item1, e.Item2, e.Item3, true);
                }

                edges[key] = e;

                edges[key] = e;
            }
        }

        // -------- BUILD --------
        List<Topology.Edge> edgeList = new List<Topology.Edge>();

        foreach (var kv in edges)
        {
            var e = kv.Value;

            if (!e.hasA || !e.hasB)
                throw new System.Exception("Incomplete edge: " + kv.Key);

            int index = edgeList.Count;

            edgeList.Add(new Topology.Edge(topo, index, e.a, e.b));

            sequences[e.a].Add(index);
            sequences[e.b].Add(index);
        }

        Topology.Node[] nodes = new Topology.Node[nodeNames.Count];

        for (int i = 0; i < nodeNames.Count; i++)
        {
            nodes[i] = new Topology.Node(
                topo,
                i,
                nodeNames[i],
                sequences[i].ToArray());
        }

        topo.SetupTopo(nodes, edgeList.ToArray(), new int[0]);

        return topo;
    }

    public void GenerateInitialLayout()
    {
        bool normalizeNodeWidth = false;
        Dictionary<int, List<int>> indexedSequences = BuildSimplifiedSequences();

        List<List<int>> simplifiedSequences = new List<List<int>>();

        string s = "\n";

        foreach (KeyValuePair<int, List<int>> kvp in indexedSequences)
        {
            simplifiedSequences.Add(kvp.Value);
            s += "\n node[" + kvp.Key + "]: " + string.Join(",", kvp.Value);
        }

        Debug.Log("starting FindFirstValidLayout with input-> simplifiedSequences:" + s);

        bool result = SimpleTopo.FindFirstValidLayout(
            simplifiedSequences,
            out List<int> nodeOrder,
            out List<int> connectionOrder,
            out List<SimpleTopo.NodeConnection> allConnectionsList);

        if (!result)
        {
            Debug.LogWarning("Failed to Generate valid Layout");
            return;
        }

        Debug.Log(SimpleTopo.DrawLayoutText(
            nodeOrder,
            connectionOrder,
            allConnectionsList,
            nodes));


        s = "Positioning Log\n";

        s += "-Given connectionOrder array: " + string.Join(",", connectionOrder);
        int[] connectionIndexToColumn = new int[connectionOrder.Count];
        for (int col = 0; col < connectionOrder.Count; col++)
            connectionIndexToColumn[connectionOrder[col]] = col * 4;

        //asign position of nodes
        for (int row = 0; row < nodeOrder.Count; row++)
        {
            int nodeIndex = nodeOrder[row];
            Node node = nodes[nodeIndex];
            s += "   Node row: "+row+"  node index: " + nodeIndex + "\n";
                
            float y = row;
            if (node.edgeSequence.Length == 0) throw new System.Exception("uNEXPECETED NODE WITH NO EDGES. NOT SURE WHERE TO PLACE IT");
            int startEdgeIndex = node.edgeSequence[0];
            int startEdgeColumn = connectionIndexToColumn[startEdgeIndex];
            float x = startEdgeColumn;
            s += "       Starting Connection-  connetcionIndex: " + startEdgeIndex + "  connectionColumn:" + startEdgeColumn + "\n";
            if (node.edgeSequence.Length > 1)
            {
                int endEdgeIndex = node.edgeSequence[node.edgeSequence.Length - 1];
                int endEdgeColumn = connectionIndexToColumn[endEdgeIndex];
                int width = endEdgeColumn - startEdgeColumn;
                x += width / 2f;
            }

            node.uvPosition = new Vector2(x * Node.defaultDistanceBetweenEdges, y * 0.1f);
            node.rotationAngleRad = Mathf.Deg2Rad * 0f;

            // we CAN do the above AFTER computing edgeSpacing, and use the values from in there.- but not worth moving it yet
            if (node.edgeSequence.Length <= 1)
            {
                node.edgeSpacing = System.Array.Empty<float>();
                continue;
            }

            node.edgeSpacing = new float[node.edgeSequence.Length - 1];
            int prevColumn=0;
            for (int i = 0; i < node.edgeSequence.Length; i++)
            {
                int edgeIndex = node.edgeSequence[i];
                int edgeColumn= connectionIndexToColumn[edgeIndex]; //connectionOrder[edgeIndex];
                s += "       Connection-  nodeSeq:" + i + "  connetcionIndex: " + edgeIndex + "  connectionColumn:" + edgeColumn + "\n";
                if (i > 0)
                {
                    node.edgeSpacing[i - 1] = (edgeColumn - prevColumn) * Node.defaultDistanceBetweenEdges;
                }
                prevColumn = edgeColumn;
            }// end connections in current node loop

        }// end row/nodes loop
        Debug.Log(s);
        // ------------------------------------------------------------
        //  normalization + rotation-- wait
        // ------------------------------------------------------------
        NormalizeToUnitSquare();
       // GenerateInitialRotations();

        Dictionary<int, List<int>> BuildSimplifiedSequences()
        {
            Dictionary<int, List<int>> result = new Dictionary<int, List<int>>();

            for (int n = 0; n < nodes.Length; n++)
            {
                Node node = nodes[n];

                List<int> physicalNeighbors = new List<int>();
                int lastNeighbor = -1;

                for (int i = 0; i < node.edgeSequence.Length; i++)
                {
                    Edge edge = edges[node.edgeSequence[i]];

                    if (edge.isTeleport)
                        continue;

                    int neighbor =
                        edge.nodeAidx == node.mapIndex ?
                        edge.nodeBidx :
                        edge.nodeAidx;

                    if (neighbor != lastNeighbor)
                    {
                        physicalNeighbors.Add(neighbor);
                        lastNeighbor = neighbor;
                    }
                }

                if (physicalNeighbors.Count > 1)
                {
                    if (physicalNeighbors[0] ==
                        physicalNeighbors[physicalNeighbors.Count - 1])
                    {
                        physicalNeighbors.RemoveAt(physicalNeighbors.Count - 1);
                    }
                }

                result.Add(node.mapIndex, physicalNeighbors);
            }

            return result;

        }//end buildseq
        void GenerateInitialRotations()
        {
            for (int n = 0; n < nodes.Length; n++)
            {
                List<int> neighbors = simplifiedSequences[n];

                if (neighbors.Count == 0)
                {
                    nodes[n].rotationAngleRad = 0f;
                    continue;
                }

                Vector2 avg = Vector2.zero;

                for (int i = 0; i < neighbors.Count; i++)
                    avg += nodes[neighbors[i]].uvPosition - nodes[n].uvPosition;

                if (avg.sqrMagnitude < 0.000001f)
                {
                    nodes[n].rotationAngleRad = 0f;
                    continue;
                }

                nodes[n].rotationAngleRad = Mathf.Atan2(avg.y, avg.x);
            }
        }
        void NormalizeToUnitSquare()
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < nodes.Length; i++)
            {
                Vector2 p = nodes[i].uvPosition;
                if (p.x < min.x) min.x = p.x;
                if (p.y < min.y) min.y = p.y;
                if (p.x > max.x) max.x = p.x;
                if (p.y > max.y) max.y = p.y;
            }

            Vector2 size = max - min;

            for (int i = 0; i < nodes.Length; i++)
            {
                Vector2 p = nodes[i].uvPosition;
                p = (p - min);

                if (size.x > 0) p.x /= size.x;
                if (size.y > 0) p.y /= size.y;

                nodes[i].uvPosition = p;
                float[] spacingArray = nodes[i].edgeSpacing;
                if (normalizeNodeWidth)
                {
                    float oneOverSizeX = 1f / size.x;
                    for (int s = 0; s < spacingArray.Length; s++)
                    {
                        spacingArray[s] *= oneOverSizeX;
                    }
                }
            }
        }
    }
}

public class TopoViz : MonoBehaviour
{
    [Header("Simulation Settings")]
    public bool runSimulation = true;
 public float simulationSpeed = 1f;
 public float damping = 0.1f;

    public Topology topo;
    private Coroutine layoutCoroutine;

    private void OnEnable()
    {
        //topo = BuildSimpleTopology();
        // topo = BuildMediumTopology();
        //topo = BuildComplexTopology();
        string s = "Start:MainBranch\n";
        s += "MainBranch:Start,DeadEnd1,SecondaryBranch[left],DeadEnd3,SecondaryBranch[right],End\n";
        s += "SecondaryBranch: MainBranch[left],DeadEnd2,MainBranch[right]";
        topo = Topology.Parse(s);

        topo.GenerateInitialLayout();
        topo.SetFixedNodes(new int[2] { 0, 5 });

        layoutCoroutine = StartCoroutine(LayoutRoutine());
    }

    [Header("Force Strengths")]
    public float springStrength = 5f;
    public float nodeRepulsion = 0.5f;
    
    public float nodeEdgeRepulsion = 0.1f;
    public float edgeRepulsion = 2f;
    public float edgeUncrossingStength = 2f;
    public float alignmentTorque = 2f;
    public float perpendicularStrength = 0.01f;
    public float idealEdgeLength = 0.2f;

    private IEnumerator LayoutRoutine()
    {
        const int stepsPerFrame = 6;

        while (true)
        {
            yield return new WaitForEndOfFrame();

            if (!runSimulation || topo == null)
                continue;

            for (int step = 0; step < stepsPerFrame; step++)
            {
                foreach (Topology.Edge edge in topo.edges)
                {
                    HandleEndpoint(edge, true);
                    HandleEndpoint(edge, false);
                }

                foreach (Topology.Node node in topo.nodes)
                {
                    node.UpdatePositionAndRotation(false);
                    node.ApplyDamping(damping);
                }
            }
        }

        void HandleEndpoint(Topology.Edge edge, bool useNodeA)
        {
            
            Topology.Node endpointNode = useNodeA ? edge.NodeA : edge.NodeB;
            Topology.Node otherNode = !useNodeA ? edge.NodeA : edge.NodeB;
            int seqIndex = endpointNode.MapEdgeIndexToSeqIndex(edge.mapIndex);

            Vector2 endpointPos = useNodeA ? edge.GetNodeAConnectionPosition()
                                           : edge.GetNodeBConnectionPosition();

            Vector2 otherEndpointPos = useNodeA ? edge.GetNodeBConnectionPosition()
                                                : edge.GetNodeAConnectionPosition();

            Vector2 nearPoint = endpointPos +
                                (otherEndpointPos - endpointPos)*0.1f;//.normalized *
                                //(idealEdgeLength * 0.1f);

            //
            // Endpoint -> every other edge
            //
            foreach (Topology.Edge other in topo.edges)
            {
                if (other == edge)
                    continue;

                if (other.NodeA == endpointNode || other.NodeB == endpointNode)
                    continue;

                Vector2 nearest = ClosestPointOnLine(
                    other.GetNodeAConnectionPosition(),
                    other.GetNodeBConnectionPosition(),
                    endpointPos,
                    out float t);

                ApplySpring(endpointNode,
                            seqIndex,
                            endpointPos,
                            other,
                            t,
                            nearest);
            }

            //
            // Endpoint -> every other node
            //
            foreach (Topology.Node other in topo.nodes)
            {
                if (other == endpointNode)
                    continue;

                Vector2 nearest = GetClosestPointOnNode(other, endpointPos, out _);

                ApplySpringToNode(other,
                                  nearest,
                                  endpointPos,
                                  endpointNode);
            }
            //
            // Endpoint -> own node
            //
            {
                Vector2 nearest = GetClosestPointOnNode(endpointNode, nearPoint, out _);

                ApplyEndpointToOwnNode(endpointNode, otherNode, nearPoint, nearest, otherEndpointPos);
            }
        }

        void ApplyEndpointToOwnNode(Topology.Node node, Topology.Node otherNode, Vector2 nearPoint, Vector2 nearestPoint, Vector2 otherNodeConnectionPosition)
        //void ApplyEndpointToOwnNode(Topology.Edge edge, bool useNodeA, Vector2 endpointPos, Vector2 nearPoint, Vector2 nearestPoint)
        {
            //Topology.Node node = useNodeA ? edge.NodeA : edge.NodeB;
            //Topology.Node otherNode = useNodeA ? edge.NodeB : edge.NodeA;

            //int seqIndex = node.MapEdgeIndexToSeqIndex(edge.mapIndex);
            //int otherSeqIndex = otherNode.MapEdgeIndexToSeqIndex(edge.mapIndex);

            Vector2 delta = nearestPoint - nearPoint;

            float dist = delta.magnitude;
            if (dist < 0.0001f)
                dist = 0.0001f;

            float barrierRadius = 100;// idealEdgeLength * 0.3f;

            if (dist >= barrierRadius)
                  return;

            float strength = 0.01f*springStrength * (1f / dist - 1f / barrierRadius);

            Vector2 force = -delta.normalized * strength * simulationSpeed;

            bool nodeFixed = topo.isNodeFixed(node.mapIndex);
            bool otherNodeFixed = topo.isNodeFixed(otherNode.mapIndex);

            if (nodeFixed || otherNodeFixed)
                force *= 2f;

            if (!nodeFixed)
            {
             //   node.ApplyForceToEdgeConnection(seqIndex, force);
                node.ApplyForceAtPosition(nearestPoint, force);
            }

            if (!otherNodeFixed)
            {
                otherNode.ApplyForceAtPosition(otherNodeConnectionPosition, -force);//  .ApplyForceToEdgeConnection(otherSeqIndex, -force);
            }
        }

        void ApplySpring(Topology.Node node, int seqIndex, Vector2 endpointPos, Topology.Edge otherEdge, float t, Vector2 nearestPoint)
        {
            Vector2 delta = nearestPoint - endpointPos;

            float dist = delta.magnitude;
            if (dist < 0.0001f)
                return;

            float x = dist / idealEdgeLength;
            float signedMagnitude = springStrength * (x - 1f / Mathf.Max(x, 0.0001f));
            Vector2 force = delta.normalized * signedMagnitude * simulationSpeed;

            bool nodeAFixed = topo.isNodeFixed(otherEdge.NodeA.mapIndex);
            bool nodeBFixed = topo.isNodeFixed(otherEdge.NodeB.mapIndex);

            if (nodeAFixed && nodeBFixed)
                force *= 2f;

            if (!topo.isNodeFixed(node.mapIndex))
                node.ApplyForceToEdgeConnection(seqIndex, force);
            else
                force *= 2f;

            if (!nodeAFixed)
                otherEdge.NodeA.ApplyForceToEdgeConnection(
                    otherEdge.NodeA.MapEdgeIndexToSeqIndex(otherEdge.mapIndex),
                    -force * (1f - t));

            if (!nodeBFixed)
                otherEdge.NodeB.ApplyForceToEdgeConnection(
                    otherEdge.NodeB.MapEdgeIndexToSeqIndex(otherEdge.mapIndex),
                    -force * t);
        }

        void ApplySpringToNode(Topology.Node node, Vector2 forcePos, Vector2 endpointPos, Topology.Node endPointsNode)
        {
            Vector2 delta = forcePos - endpointPos;

            float dist = delta.magnitude;
            if (dist < 0.0001f)
                return;

            float x = dist / idealEdgeLength;
            float signedMagnitude = springStrength * (x - 1f / Mathf.Max(x, 0.0001f));
            Vector2 force = delta.normalized * signedMagnitude * simulationSpeed;

            bool endPointNodeFixed = topo.isNodeFixed(endPointsNode.mapIndex);
            bool nodeFixed = topo.isNodeFixed(node.mapIndex);

            if (nodeFixed || endPointNodeFixed)
                force *= 2f;

            if (!endPointNodeFixed)
                endPointsNode.ApplyForceAtPosition(endpointPos, force);

            if (!nodeFixed)
                node.ApplyForceAtPosition(forcePos, -force);
        }

        static Vector2 ClosestPointOnLine(Vector2 a, Vector2 b, Vector2 p, out float t)
        {
            Vector2 ab = b - a;

            float lenSq = Vector2.Dot(ab, ab);
            if (lenSq < 0.000001f)
            {
                t = 0;
                return a;
            }

            t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);

            return a + ab * t;
        }

        static Vector2 GetClosestPointOnNode(Topology.Node node, Vector2 p, out float t)
        {
            Vector2 endpointA = node.GetEdgeConnectionPosition(0);
            Vector2 endpointB = node.GetEdgeConnectionPosition(node.edgeSequence.Length - 1);

            return ClosestPointOnLine(endpointA, endpointB, p, out t);
        }
    }

    IEnumerator outlineLayoutRoutine()
    {
        const int stepsPerFrame = 6;
        while (true)
        {
            yield return new WaitForEndOfFrame();
            if (!runSimulation) continue;
            //  nodes will have a (for now single-shared constant global) moment of inertia and mass pair of values, which will determine
            //  (when using the applied force's offset from center of mass) how much of the force applied becomes angular torque, and how much a linear force.
            for (int step = 0; step < stepsPerFrame; step++)
            {

                //for each connection endpoint
                //    loop all other connections
                //      if node at current endpoint shared with other connection: skip
                //      get distance from endpoint to nearest point on other connection line
                //      push/pull effect- spring like- constant ideal length for all (for now). force/impulse applied to:
                //          a) the node, at the current endpoint position
                //          b) the other connection's nearest point.  This force/impulse will be transfered to both the nodes at the other connection's endpoints.
                //    
                //    loop all nodes
                //      skip the two this connection touches
                //      get distance from endpoint to nearest point on other node lines
                //      spring effect like above
                //      push/pull effect- spring like- constant ideal length for all (for now). force/impulse applied to:
                //          a) the node, at the current endpoint position
                //          b) the other node, at the nearest point on the line (may be between endpoints).
            }
        }
    }

    IEnumerator crapLayoutRoutine()
    {
        const int stepsPerFrame = 6;

        const float dMin = 0.1f;
        const float dMax = 0.1f;

        const float repulsionStrength = 2f;
        const float attractionStrength = 2f;

        while (true)
        {
            yield return new WaitForEndOfFrame();
            if (!runSimulation) continue;
            for (int step = 0; step < stepsPerFrame; step++)
            {
                for (int i = 0; i < topo.nodes.Length; i++)
                    topo.nodes[i].Reset();

                for (int i = 0; i < topo.edges.Length; i++)
                {
                    Topology.Edge e1 = topo.edges[i];

                    Vector2 a1 = e1.GetNodeAConnectionPosition();
                    Vector2 a2 = e1.GetNodeBConnectionPosition();

                    for (int j = i + 1; j < topo.edges.Length; j++)
                    {
                        Topology.Edge e2 = topo.edges[j];

                        if (SharesNode(e1, e2))
                            continue;

                        Vector2 b1 = e2.GetNodeAConnectionPosition();
                        Vector2 b2 = e2.GetNodeBConnectionPosition();

                        float d = SegmentDistance(a1, a2, b1, b2, out Vector2 dir);

                        Vector2 force = Vector2.zero;

                        if (d < dMin)
                        {
                            // strong repulsion (prevents crossing)
                            float t = (dMin - d) / (d + 1e-4f);
                            force = dir * (repulsionStrength * t);
                        }
                        else if (d > dMax)
                        {
                            // weak attraction (keeps graph compact)
                            float t = (d - dMax);
                            force = -dir * (attractionStrength * t);
                        }
                        else
                        {
                            continue;
                        }
                        force *= simulationSpeed;
                        ApplyEdgeForce(e1, force);
                        ApplyEdgeForce(e2, -force);
                    }
                }

                for (int n = 0; n < topo.nodes.Length; n++)
                {
                    topo.nodes[n].ApplyDamping(damping);
                    topo.nodes[n].UpdatePositionAndRotation(true);
                }
            }

            
        }

        float SegmentDistance(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out Vector2 dir)
        {
            Vector2 c1, c2;

            float d = Mathf.Sqrt(SegmentToSegmentSquared(a1, a2, b1, b2, out c1, out c2));

            dir = c2 - c1;
            float len = dir.magnitude;
            if (len < 1e-6f)
            {
                dir = Vector2.zero;
                return d;
            }

            dir /= len;
            return d;
        }
        float SegmentToSegmentSquared(
    Vector2 p1, Vector2 p2,
    Vector2 q1, Vector2 q2,
    out Vector2 c1, out Vector2 c2)
        {
            // standard closest points between segments
            Vector2 d1 = p2 - p1;
            Vector2 d2 = q2 - q1;
            Vector2 r = p1 - q1;

            float a = Vector2.Dot(d1, d1);
            float e = Vector2.Dot(d2, d2);
            float f = Vector2.Dot(d2, r);

            float s, t;

            if (a <= 1e-8f && e <= 1e-8f)
            {
                c1 = p1;
                c2 = q1;
                return (c1 - c2).sqrMagnitude;
            }

            if (a <= 1e-8f)
            {
                s = 0;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector2.Dot(d1, r);

                if (e <= 1e-8f)
                {
                    t = 0;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector2.Dot(d1, d2);
                    float denom = a * e - b * b;

                    if (denom != 0)
                        s = Mathf.Clamp01((b * f - c * e) / denom);
                    else
                        s = 0;

                    t = (b * s + f) / e;

                    if (t < 0)
                    {
                        t = 0;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1)
                    {
                        t = 1;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }

            c1 = p1 + d1 * s;
            c2 = q1 + d2 * t;

            return (c1 - c2).sqrMagnitude;
        }

        void ApplyEdgeForce(Topology.Edge e, Vector2 force)
        {
            ApplyAtNodeConnection(e.NodeA, e, force, true);
            ApplyAtNodeConnection(e.NodeB, e, -force, false);
            void ApplyAtNodeConnection(Topology.Node node, Topology.Edge e, Vector2 force, bool isNodeA)
            {
                int seqIndex = node.MapEdgeIndexToSeqIndex(e.mapIndex);

                // linear response
             //   node.ApplyForce(force);

                // rotational response (prevents edge “shearing through” node)
                node.ApplyForceToEdgeConnection(seqIndex, force);
            }
        }
        bool SharesNode(Topology.Edge a, Topology.Edge b)
        {
            return a.nodeAidx == b.nodeAidx ||
                   a.nodeAidx == b.nodeBidx ||
                   a.nodeBidx == b.nodeAidx ||
                   a.nodeBidx == b.nodeBidx;
        }
    }

    private IEnumerator OLDLayoutRoutine()
    {
        //memory reuse
        Vector2 p1;
        Vector2 p2;
        Vector2 p3;
        Vector2 p4;

        while (true)
        {
            if (runSimulation && topo != null)
            {
                ApplyForces();
                UpdateTopology();
            }
            yield return null;
        }


        Vector2 SpringForceOnA(Vector2 posA, Vector2 posB)
        {
            Vector2 offset = posB - posA;
            if (offset == Vector2.zero)
            {
                Debug.LogWarning("Overlapping nodes at pos: "+ posA );
                return Vector2.one;
            }
            float dist = offset.magnitude;
            Vector2 dir = offset / dist;
            float diffFromIdeal = dist - idealEdgeLength;
            //diffFromIdeal = Mathf.Max(diffFromIdeal, 0.001f);
           // diffFromIdeal *= diffFromIdeal;
            //negative means too close- push A away from B
            return dir * diffFromIdeal;
        }
        void ApplyForces()
        {
            // 1. Node-Node Repulsion (Keep nodes away from each other)
            for (int i = 0; i < topo.nodes.Length; i++)
            {
                Topology.Node nodeA = topo.nodes[i];
                for (int j = i + 1; j < topo.nodes.Length; j++)
                {
                    Topology.Node nodeB = topo.nodes[j];
                    Vector2 forceOnA = SpringForceOnA(nodeA.uvPosition, nodeB.uvPosition) * springStrength * Time.fixedDeltaTime * simulationSpeed;
                    nodeA.ApplyForce(forceOnA );
                    nodeB.ApplyForce(-forceOnA);
                }
            }

            foreach (var edge in topo.edges)
            {
                Vector2 posA = edge.GetNodeAConnectionPosition();
                Vector2 posB = edge.GetNodeBConnectionPosition();
                Vector2 diff = posB - posA;
                float dist = diff.magnitude;

                Vector2 forceDir = diff.normalized;
                Vector2 springForce = forceDir * (dist - idealEdgeLength) * springStrength;

                // Find the local indices in the node's edgeSequence
                int seqIdxA = edge.NodeA.MapEdgeIndexToSeqIndex(edge.mapIndex);
                int seqIdxB = edge.NodeB.MapEdgeIndexToSeqIndex(edge.mapIndex);

                edge.NodeA.ApplyForceToEdgeConnection(seqIdxA, springForce * Time.fixedDeltaTime * simulationSpeed);
                edge.NodeB.ApplyForceToEdgeConnection(seqIdxB, -springForce * Time.fixedDeltaTime * simulationSpeed);

                // Alignment Torque: Encourage node to rotate toward the edge neighbor
                ApplyAlignmentTorque(edge.NodeA, seqIdxA, posB);
                ApplyAlignmentTorque(edge.NodeB, seqIdxB, posA);
            }
        }

        void ApplyForcesFULL()
        {
            // 1. Node-Node Repulsion (Keep nodes away from each other)
            for (int i = 0; i < topo.nodes.Length; i++)
            {
                Topology.Node nodeA = topo.nodes[i];
                for (int j = i + 1; j < topo.nodes.Length; j++)
                {
                    Vector2 dir = nodeA.uvPosition - topo.nodes[j].uvPosition;
                    float dist = Mathf.Max(dir.magnitude, 0.01f);
                    Vector2 force = (dir.normalized * nodeRepulsion) / (dist * dist);

                    nodeA.ApplyForce(force * Time.fixedDeltaTime);
                    topo.nodes[j].ApplyForce(-force * Time.fixedDeltaTime);
                    
                }
                
                foreach (var edge in topo.edges)// we ALSO want to repel node from the center of edges, so they dont touch them.
                {
                    if (edge.NodeA == nodeA || edge.NodeB == nodeA) continue;
                    Vector2 posA = edge.GetNodeAConnectionPosition();
                    Vector2 posB = edge.GetNodeBConnectionPosition();
                    Vector2 center = (posB - posA) * 0.5f;
                    Vector2 dir = nodeA.uvPosition - center;
                    float dist = Mathf.Max(dir.magnitude, 0.001f);
                    if (dist < 0.1f)
                    {
                        Vector2 force = (dir.normalized * nodeEdgeRepulsion) / (dist * dist);

                        nodeA.ApplyForce(force * Time.fixedDeltaTime);
                    }
                }
                ApplyPerpendicularBias(nodeA);
            }
            void ApplyPerpendicularBias(Topology.Node node)
            {
                int count = node.edgeSequence.Length;
                if (count == 0) return;

                for (int i = 0; i < count; i++)
                {
                    Vector2 offset = node.GetEdgeConnectionOffset(i);
                    float len = offset.magnitude;
                    if (len < 0.0001f) continue;

                    Vector2 radial = offset / len;

                    // get edge direction at this connection
                    Topology.Edge edge = node.EdgeInSequence(i);

                    Vector2 edgeDir = edge.GetDirectionFromNode(node); // should be normalized

                    // perpendicular condition: dot = 0
                    float d = Vector2.Dot(radial, edgeDir);

                    // tangential direction around node
                    Vector2 tangent = new Vector2(-radial.y, radial.x);

                    // drive toward dot = 0
                    Vector2 force = -tangent * d * perpendicularStrength * Time.fixedDeltaTime;

                    node.ApplyForceToEdgeConnection(i, force);
                }
            }
            // 2. Edge Springs & Alignment Torque
            foreach (var edge in topo.edges)
            {
                Vector2 posA = edge.GetNodeAConnectionPosition();
                Vector2 posB = edge.GetNodeBConnectionPosition();
                Vector2 diff = posB - posA;
                float dist = diff.magnitude;

                Vector2 forceDir = diff.normalized;
                Vector2 springForce = forceDir * (dist - idealEdgeLength) * springStrength;

                // Find the local indices in the node's edgeSequence
                int seqIdxA = edge.NodeA.MapEdgeIndexToSeqIndex(edge.mapIndex);
                int seqIdxB = edge.NodeB.MapEdgeIndexToSeqIndex(edge.mapIndex);

                edge.NodeA.ApplyForceToEdgeConnection(seqIdxA, springForce * Time.fixedDeltaTime);
                edge.NodeB.ApplyForceToEdgeConnection(seqIdxB, -springForce * Time.fixedDeltaTime);

                // Alignment Torque: Encourage node to rotate toward the edge neighbor
                ApplyAlignmentTorque(edge.NodeA, seqIdxA, posB);
                ApplyAlignmentTorque(edge.NodeB, seqIdxB, posA);
            }

            // 3. Edge-Edge Repulsion (The "Untangler")
            // We compare edges; if they are crossing or near-crossing, push them away.
            if (!Mathf.Approximately(edgeRepulsion, 0))
            {
                for (int i = 0; i < topo.edges.Length; i++)
                {
                    for (int j = i + 1; j < topo.edges.Length; j++)
                    {
                        Topology.Edge e1 = topo.edges[i];
                        Topology.Edge e2 = topo.edges[j];
                        if (e1.isTeleport || e2.isTeleport) continue;
                        if (topo.EdgesShareBothNodesAndAreSequentialInThem(e1, e2)) continue;
                        HandleEdgeRepulsion(e1, e2);
                        HandleCrossingEdges(e1, e2);
                    }
                }
            }
        }

        void ApplyAlignmentTorque(Topology.Node node, int seqIdx, Vector2 targetPos)
        {
            Vector2 toTarget = (targetPos - node.uvPosition).normalized;
            Vector2 connectionDir = new Vector2(Mathf.Cos(node.rotationAngleRad), Mathf.Sin(node.rotationAngleRad)); //(node.GetEdgeConnectionPosition(seqIdx) - node.uvPosition).normalized;

            // 2D Cross product gives us the "direction" of the rotation needed
            float angleDiff = Vector2.SignedAngle(connectionDir, toTarget);
            node.ApplyTorqueToNode(angleDiff * alignmentTorque * 0.01f * Time.fixedDeltaTime);
        }


        void HandleEdgeRepulsion(Topology.Edge e1, Topology.Edge e2)
        {
            // 1. Get current segment positions
            p1 = e1.GetNodeAConnectionPosition();
            p2 = e1.GetNodeBConnectionPosition();
            p3 = e2.GetNodeAConnectionPosition();
            p4 = e2.GetNodeBConnectionPosition();
            //  if (!SegmentsIntersect(p1, p2, p3, p4)) return;
            // 2. Find closest points between segments (p1-p2) and (p3-p4)
            // t and s are normalized values (0 to 1) along the length of the edges
            float t, s;
            float distSq = ClosestPointsOnSegments(p1, p2, p3, p4, out t, out s);
            float dist = Mathf.Sqrt(distSq);

            // 3. Apply force if they are too close (or crossing)
            // We use a threshold to provide a "buffer" zone
            float threshold = idealEdgeLength * 0.5f;
            if (dist < threshold)
            {
                Vector2 posOnE1 = Vector2.Lerp(p1, p2, t);
                Vector2 posOnE2 = Vector2.Lerp(p3, p4, s);

                // Calculate direction to push: from e2 point toward e1 point
                Vector2 pushDir;
                if (dist > 0.0001f)
                    pushDir = (posOnE1 - posOnE2) / dist;
                else
                    // If perfectly overlapping, push in an arbitrary perpendicular direction
                    pushDir = Vector2.Perpendicular(p2 - p1).normalized;

                // Force magnitude increases as they get closer (inverse square or linear)
                float forceMag = (threshold - dist) * edgeRepulsion;
                Vector2 finalForce = pushDir * forceMag * Time.fixedDeltaTime;

                // 4. Distribute forces to nodes based on proximity to the contact point
                // If the contact is at NodeA (t=0), NodeA gets all the force. 
                // If in the middle (t=0.5), both get half.
                e1.NodeA.ApplyForce(finalForce * (1f - t));
                e1.NodeB.ApplyForce(finalForce * t);

                e2.NodeA.ApplyForce(-finalForce * (1f - s));
                e2.NodeB.ApplyForce(-finalForce * s);
            }


            // Math helper: Closest distance squared between two 2D segments
            float ClosestPointsOnSegments(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out float t, out float s)
            {
                Vector2 d1 = p2 - p1;
                Vector2 d2 = p4 - p3;
                Vector2 r = p1 - p3;
                float a = Vector2.Dot(d1, d1);
                float e = Vector2.Dot(d2, d2);
                float f = Vector2.Dot(d2, r);

                float epsilon = 0.00001f;

                if (a <= epsilon && e <= epsilon)
                {
                    t = s = 0f;
                    return Vector2.SqrMagnitude(p1 - p3);
                }
                if (a <= epsilon)
                {
                    t = 0f;
                    s = Mathf.Clamp(f / e, 0f, 1f);
                }
                else
                {
                    float c = Vector2.Dot(d1, r);
                    if (e <= epsilon)
                    {
                        s = 0f;
                        t = Mathf.Clamp(-c / a, 0f, 1f);
                    }
                    else
                    {
                        float b = Vector2.Dot(d1, d2);
                        float denom = a * e - b * b;
                        if (denom != 0)
                        {
                            t = Mathf.Clamp((b * f - c * e) / denom, 0f, 1f);
                        }
                        else
                        {
                            t = 0f; // Parallel
                        }
                        s = Mathf.Clamp((b * t + f) / e, 0f, 1f);
                    }
                }
                return Vector2.SqrMagnitude((p1 + d1 * t) - (p3 + d2 * s));
            }
        }
        void HandleCrossingEdges(Topology.Edge e1, Topology.Edge e2)
        {
            if (!SegmentsIntersect(p1, p2, p3, p4)) return;

            Vector2 d1 = p2 - p1;
            Vector2 d2 = p4 - p3;

            float len1 = d1.magnitude;
            float len2 = d2.magnitude;
            if (len1 < 0.0001f || len2 < 0.0001f) return;

            d1 /= len1;
            d2 /= len2;

            Vector2 n1 = new Vector2(-d1.y, d1.x);
            Vector2 n2 = new Vector2(-d2.y, d2.x);

            float angleFactor = 1f - Mathf.Abs(Vector2.Dot(d1, d2));
            float strength = edgeUncrossingStength * angleFactor * Time.fixedDeltaTime;

            float sideA1 = Mathf.Sign(Vector2.Dot(p3 - p1, n1));
            float sideA2 = Mathf.Sign(Vector2.Dot(p4 - p1, n1));

            float sideB1 = Mathf.Sign(Vector2.Dot(p1 - p3, n2));
            float sideB2 = Mathf.Sign(Vector2.Dot(p2 - p3, n2));

            if (sideA1 == 0) sideA1 = 1;
            if (sideA2 == 0) sideA2 = -1;
            if (sideB1 == 0) sideB1 = 1;
            if (sideB2 == 0) sideB2 = -1;

            Vector2 fA1 = n1 * sideA1 * strength;
            Vector2 fA2 = n1 * sideA2 * strength;

            Vector2 fB1 = n2 * sideB1 * strength;
            Vector2 fB2 = n2 * sideB2 * strength;

            // --- resolve sequence indices 
            int e1SeqA = e1.NodeA.MapEdgeIndexToSeqIndex(e1.mapIndex);
            int e1SeqB = e1.NodeB.MapEdgeIndexToSeqIndex(e1.mapIndex);

            int e2SeqA = e2.NodeA.MapEdgeIndexToSeqIndex(e2.mapIndex);
            int e2SeqB = e2.NodeB.MapEdgeIndexToSeqIndex(e2.mapIndex);

            // apply forces 
            e1.NodeA.ApplyForceToEdgeConnection(e1SeqA, fA1);
            e1.NodeB.ApplyForceToEdgeConnection(e1SeqB, fA2);

            e2.NodeA.ApplyForceToEdgeConnection(e2SeqA, fB1);
            e2.NodeB.ApplyForceToEdgeConnection(e2SeqB, fB2);

            // --- flip correction (torque) ---
            if (sideA1 == sideA2)
            {
                Vector2 torque = n1 * strength * 0.5f;
                e1.NodeA.ApplyForceToEdgeConnection(e1SeqA, torque);
                e1.NodeB.ApplyForceToEdgeConnection(e1SeqB, -torque);
            }

            if (sideB1 == sideB2)
            {
                Vector2 torque = n2 * strength * 0.5f;
                e2.NodeA.ApplyForceToEdgeConnection(e2SeqA, torque);
                e2.NodeB.ApplyForceToEdgeConnection(e2SeqB, -torque);
            }

            bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
            {
                float o1 = Orient(a1, a2, b1);
                float o2 = Orient(a1, a2, b2);
                float o3 = Orient(b1, b2, a1);
                float o4 = Orient(b1, b2, a2);

                if (o1 * o2 < 0f && o3 * o4 < 0f)
                {
                    return true; // proper intersection
                }

                // collinear cases
                if (o1 == 0f && OnSegment(a1, a2, b1)) return true;
                if (o2 == 0f && OnSegment(a1, a2, b2)) return true;
                if (o3 == 0f && OnSegment(b1, b2, a1)) return true;
                if (o4 == 0f && OnSegment(b1, b2, a2)) return true;

                return false;


                static float Orient(Vector2 a, Vector2 b, Vector2 c)
                {
                    // cross((b - a), (c - a))
                    return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                }

                static bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
                {
                    return p.x >= Mathf.Min(a.x, b.x) &&
                           p.x <= Mathf.Max(a.x, b.x) &&
                           p.y >= Mathf.Min(a.y, b.y) &&
                           p.y <= Mathf.Max(a.y, b.y);
                }
            }
        }
        void UpdateTopology()
        {
            foreach (var node in topo.nodes)
            {
                node.UpdatePositionAndRotation(false); // Update without immediate reset

                // Apply damping/friction
                node.ApplyDamping(damping);
            }
        }
    }

    static public Topology BuildMediumTopology()
    {



        var topology = new Topology();

        var start = new Topology.Node(topology,0,"Start", new int[1] { 0 });
        var mainPath = new Topology.Node(topology, 1, "MainPath", new int[8] { 0, 1, 2, 3, 6, 7, 8, 9 });
        var branch1 = new Topology.Node(topology, 2, "Branch1", new int[3] { 1,4,5 });
        var deadEnd2 = new Topology.Node(topology, 3, "DeadEnd2", new int[2] { 2,3 } );
        var branch1Retun = new Topology.Node(topology, 4, "branch1Retun", new int[3] { 4,5,6 });
        var deadEnd3 = new Topology.Node(topology, 5, "DeadEnd3", new int[2] { 7, 8 });
        var end = new Topology.Node(topology, 6, "End", new int[1] { 9 });

        Topology.Node[] nodes = new Topology.Node[] { start, mainPath, branch1, deadEnd2, branch1Retun, deadEnd3, end };
        int[] fixedNodes = new int[] { 0, 6 };
        
        Topology.Edge[] edges = new Topology.Edge[]{
        new Topology.Edge(topology, 0, start.mapIndex, mainPath.mapIndex),
        new Topology.Edge(topology, 1, mainPath.mapIndex, branch1.mapIndex),

        new Topology.Edge(topology, 2, mainPath.mapIndex, deadEnd2.mapIndex),
        new Topology.Edge(topology, 3, deadEnd2.mapIndex, mainPath.mapIndex),

        new Topology.Edge(topology, 4, branch1.mapIndex, branch1Retun.mapIndex),
        new Topology.Edge(topology, 5, branch1Retun.mapIndex, branch1.mapIndex),
        new Topology.Edge(topology, 6, branch1Retun.mapIndex, mainPath.mapIndex),

        new Topology.Edge(topology, 7, mainPath.mapIndex, deadEnd3.mapIndex),
        new Topology.Edge(topology, 8, deadEnd3.mapIndex, mainPath.mapIndex),

        new Topology.Edge(topology, 9, mainPath.mapIndex, end.mapIndex) };

        topology.SetupTopo(nodes, edges, fixedNodes);


        //geometric init
        start.uvPosition = new Vector2(0.05f, 0.05f);
        start.rotationAngleRad = Mathf.PI * 0.75f;
        end.uvPosition = new Vector2(0.95f, 0.95f);
        end.rotationAngleRad = Mathf.PI * -0.75f;
        Randize(mainPath);
        Randize(branch1);
        Randize(deadEnd2);
        Randize(branch1Retun);
        void Randize(Topology.Node node)
        {
            node.uvPosition = new Vector2(UnityEngine.Random.value, UnityEngine.Random.value);
            node.rotationAngleRad = 2f * Mathf.PI * UnityEngine.Random.value;
        }
        return topology;
    }
    static public Topology BuildSimpleTopology()
    {
        var topology = new Topology();

        // 0: Start, 1: MainPath, 2: LoopHub, 3: LoopReturn, 4: End
        var start = new Topology.Node(topology, 0, "Start", new int[] { 0 });
        var mainPath = new Topology.Node(topology, 1, "MainPath", new int[] { 0, 1, 4, 5 });
        var loopHub = new Topology.Node(topology, 2, "LoopHub", new int[] { 1, 2, 3 });
        var loopReturn = new Topology.Node(topology, 3, "LoopReturn", new int[] { 2, 3, 4 });
        var end = new Topology.Node(topology, 4, "End", new int[] { 5 });

        Topology.Node[] nodes = new Topology.Node[] { start, mainPath, loopHub, loopReturn, end };
        int[] fixedNodes = new int[] { 0, 4 };

        Topology.Edge[] edges = new Topology.Edge[] {
        new Topology.Edge(topology, 0, 0, 1), // Start -> Main
        new Topology.Edge(topology, 1, 1, 2), // Main -> LoopHub
        new Topology.Edge(topology, 2, 2, 3), // Loop (Top rail)
        new Topology.Edge(topology, 3, 3, 2), // Loop (Bottom rail)
        new Topology.Edge(topology, 4, 3, 1), // LoopReturn -> Main
        new Topology.Edge(topology, 5, 1, 4)  // Main -> End
    };

        topology.SetupTopo(nodes, edges, fixedNodes);

        // Initial Messy State
        start.uvPosition = new Vector2(0.1f, 0.5f);
        end.uvPosition = new Vector2(0.9f, 0.5f);
        mainPath.uvPosition = new Vector2(0.3f, 0.4f);
        loopHub.uvPosition = new Vector2(0.6f, 0.7f);
        loopReturn.uvPosition = new Vector2(0.6f, 0.3f);

        return topology;
    }
    static public Topology BuildComplexTopology()
    {
        var topology = new Topology();

        var start = new Topology.Node(topology, 0, "Start", new int[1] { 0 });
        var mainPath = new Topology.Node(topology, 1, "MainPath", new int[] { 0, 1, 2, 3,4,5, 6, 7, 14, 15, 16, 17, 18, 19 });
        var branch1 = new Topology.Node(topology, 2, "Branch1", new int[] { 1, 8,9,10,11,12,13 });
        var deadEnd1 = new Topology.Node(topology, 3, "DeadEnd1", new int[2] { 2, 3 });
        var deadEnd2 = new Topology.Node(topology, 4, "DeadEnd2", new int[2] { 4, 5 });
        var deadEnd3 = new Topology.Node(topology, 5, "DeadEnd3", new int[2] { 6, 7 });

        var branch1DeadEnd1 = new Topology.Node(topology, 6, "Branch1DeadEnd1", new int[2] { 8, 9 });
        var branch1DeadEnd2 = new Topology.Node(topology, 7, "Branch1DeadEnd1", new int[2] { 10, 11 });
        var branch1Retun = new Topology.Node(topology, 8, "branch1Retun", new int[3] { 12, 13, 14 });

        var deadEnd4 = new Topology.Node(topology, 9, "DeadEnd4", new int[2] { 15, 16 });
        var deadEnd5 = new Topology.Node(topology, 10, "DeadEnd5", new int[2] { 17, 18 });

        var end = new Topology.Node(topology, 11, "End", new int[1] { 19 });

        Topology.Node[] nodes = new Topology.Node[12] { start, mainPath, branch1, deadEnd1, deadEnd2, deadEnd3, branch1DeadEnd1, branch1DeadEnd2, branch1Retun, deadEnd4, deadEnd5, end };
        int[] fixedNodes = new int[] { 0, 11 };

        Topology.Edge[] edges = new Topology.Edge[]{
                new Topology.Edge(topology, 0, start.mapIndex, mainPath.mapIndex),
                new Topology.Edge(topology, 1, mainPath.mapIndex, branch1.mapIndex),
                //two way dead end
                new Topology.Edge(topology, 2, mainPath.mapIndex, deadEnd1.mapIndex),
                new Topology.Edge(topology, 3, deadEnd1.mapIndex, mainPath.mapIndex),
                //two way dead end
                new Topology.Edge(topology, 4, mainPath.mapIndex, deadEnd2.mapIndex),
                new Topology.Edge(topology, 5, deadEnd2.mapIndex, mainPath.mapIndex),
                //two way dead end
                new Topology.Edge(topology, 6, mainPath.mapIndex, deadEnd3.mapIndex),
                new Topology.Edge(topology, 7, deadEnd3.mapIndex, mainPath.mapIndex),

                //two way dead end (on branch1)
                new Topology.Edge(topology, 8, branch1.mapIndex, branch1DeadEnd1.mapIndex),
                new Topology.Edge(topology, 9, branch1DeadEnd1.mapIndex, branch1.mapIndex),
                //two way dead end (on branch1)
                new Topology.Edge(topology, 10, branch1.mapIndex, branch1DeadEnd2.mapIndex),
                new Topology.Edge(topology, 11, branch1DeadEnd2.mapIndex, branch1.mapIndex),

                //branch to branchReturn  - two way
                new Topology.Edge(topology, 12, branch1.mapIndex, branch1Retun.mapIndex),
                new Topology.Edge(topology, 13, branch1Retun.mapIndex, branch1.mapIndex),

                //branch return to main - one way
                new Topology.Edge(topology, 14, branch1Retun.mapIndex, mainPath.mapIndex),

                //two way dead end
                new Topology.Edge(topology, 15, mainPath.mapIndex, deadEnd4.mapIndex),
                new Topology.Edge(topology, 16, deadEnd4.mapIndex, mainPath.mapIndex),
                //two way dead end
                new Topology.Edge(topology, 17, mainPath.mapIndex, deadEnd5.mapIndex),
                new Topology.Edge(topology, 18, deadEnd5.mapIndex, mainPath.mapIndex),

                //main to end
                new Topology.Edge(topology, 19, mainPath.mapIndex, end.mapIndex) };

        topology.SetupTopo(nodes, edges, fixedNodes);


        //geometric init
        start.uvPosition = new Vector2(0.05f, 0.05f);
        start.rotationAngleRad = Mathf.PI * 0.75f;
        end.uvPosition = new Vector2(0.95f, 0.95f);
        end.rotationAngleRad = Mathf.PI * -0.75f;
        //randize all but start and end
        for (int i = 1; i < nodes.Length - 2; i++)
            Randize(nodes[i]);

        void Randize(Topology.Node node)
        {
            node.uvPosition = new Vector2(UnityEngine.Random.value, UnityEngine.Random.value);
            node.rotationAngleRad = 2f * Mathf.PI * UnityEngine.Random.value;
        }
        return topology;
    }

    private Material lineMaterial;
    void CreateMaterial()
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        lineMaterial = new Material(shader);
    }
    Color edgeColor = Color.green;
    Color teleportEdgeColor = Color.grey;
    Color nodeColor = Color.blue;
    Color fixedNodeColor = Color.black;
    Color borderColor = Color.red;
    void OnRenderObject()
    {
        if (topo == null) return;
        if (lineMaterial == null) CreateMaterial();

        lineMaterial.SetPass(0);

        GL.PushMatrix();
        // GL.LoadOrtho();
        GL.LoadIdentity();
        float margin = 0.1f;

        Matrix4x4 m = Matrix4x4.Ortho(
            0.0f - margin, 1.0f + margin,
            0.0f - margin, 1.0f + margin,
            -1.0f, 1.0f
        );

        GL.LoadProjectionMatrix(m);

        
        GL.Begin(GL.LINES);
        
        //draw border
        GL.Color(borderColor);

        GL.Vertex(Vector3.zero);
        GL.Vertex(Vector3.up);

        GL.Vertex(Vector3.up);
        GL.Vertex(new Vector3(1, 1, 0));

        GL.Vertex(new Vector3(1, 1, 0));
        GL.Vertex(Vector3.right);

        GL.Vertex(Vector3.right);
        GL.Vertex(Vector3.zero);

        // draw edges
        for (int i = 0; i < topo.edges.Length; i++)
        {
            Topology.Edge edge = topo.edges[i];

            if (edge.isTeleport)
                GL.Color(teleportEdgeColor);
            else
                GL.Color(edgeColor);

            Vector2 pa = edge.GetNodeAConnectionPosition();
            Vector2 pb = edge.GetNodeBConnectionPosition();

            GL.Vertex(new Vector3(pa.x, pa.y, 0));
            GL.Vertex(new Vector3(pb.x, pb.y, 0));
        }
        GL.End();

        // draw nodes

        GL.Begin(GL.LINES);

        for (int i = 0; i < topo.nodes.Length; i++)
        {
            Topology.Node node = topo.nodes[i];

            
            if (topo.isNodeFixed(i))
                GL.Color(fixedNodeColor);
            else
                GL.Color(nodeColor);
            Vector2 p = node.uvPosition;

            for (int j = 0; j < node.edgeSequence.Length; j++)
            {
                Vector2 edgePos = node.GetEdgeConnectionPosition(j);
                GL.Vertex(new Vector3(p.x, p.y, 0));
                GL.Vertex(new Vector3(edgePos.x, edgePos.y, 0));
            }
        }

        GL.End();

        GL.PopMatrix();
    }
}

public class TopoBuilder
{
    List<string> nodeNames;//ordered specifically
    Dictionary<string, Connection[]> connectionOrderByNodeName;
    HashSet<string> uniqueNames;
    List<int> fixedNodes;
    // nodes are defines first, in the constructor
    public TopoBuilder(string[] nodeNames)
    {
        this.nodeNames = new List<string>(nodeNames);
        uniqueNames = new HashSet<string>(nodeNames);
        connectionOrderByNodeName = new Dictionary<string, Connection[]>();
        fixedNodes = new List<int>();
    }
    public void SetNodeToFixed(string nameName)
    {
        if (!uniqueNames.Contains(nameName)) throw new System.Exception("nonexistent SetNodeToFixed specified: " + nameName);
        fixedNodes.Add( nodeNames.IndexOf(nameName));
    }

    //the connections are defined second
    public class Connection
    {
        public string nodeA; public string nodeB;
        public bool isTeleport; public bool isTwoWay;
        public Connection(string nodeA, string nodeB, bool isTeleport, bool isTwoWay = true)
        {
            this.nodeA = nodeA;
            this.nodeB = nodeB;
            this.isTeleport = isTeleport;
            this.isTwoWay = isTwoWay;
        }
    }

    public Connection CreateConnection(string nodeA, string nodeB, bool isTeleport, bool isTwoWay = true)
    {
        if (!uniqueNames.Contains(nodeA)) throw new System.Exception("nonexistent nodeA specified: " + nodeA);
        if (!uniqueNames.Contains(nodeB)) throw new System.Exception("nonexistent nodeB specified: " + nodeB);
        return new Connection(nodeA, nodeB, isTeleport, isTwoWay );
    }

    //node connections order is defined third
    public void SetNodeConnectionOrder(string node, Connection[] connections)
    {
        //check connections all ref node
        connectionOrderByNodeName[node] = connections;
    }
    // topology generated fourth
    public Topology GenerateTopology()
    {
        Topology topo = new Topology();
        // confirm more that one node
        // confirm all nodes have a connection order specified in connectionOrderByNodeName

        return topo;
    }
}


public class SimpleTopo
{
    public class NodeConnection
    {
        public int nodeAIndex;
        public int nodeBIndex;
        public int instanceIndex;

        public NodeConnection(int nodeIndex1, int nodeIndex2, int instanceIndex)
        {
            if (nodeIndex1 < nodeIndex2)
            {
                nodeAIndex = nodeIndex1;
                nodeBIndex = nodeIndex2;
            }
            else
            {
                nodeAIndex = nodeIndex2;
                nodeBIndex = nodeIndex1;
            }

            this.instanceIndex = instanceIndex;
        }

        public override bool Equals(object obj)
        {
            NodeConnection other = obj as NodeConnection;
            if (other == null) return false;

            return nodeAIndex == other.nodeAIndex &&
                   nodeBIndex == other.nodeBIndex &&
                   instanceIndex == other.instanceIndex;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + nodeAIndex;
                hash = hash * 31 + nodeBIndex;
                hash = hash * 31 + instanceIndex;
                return hash;
            }
        }
        public override string ToString()
        {
            return nodeAIndex + "->" + nodeBIndex +" # "+ (instanceIndex);
        }
    }


    /// <summary>
    /// The main input for this function is a list of nodes, each with an ordered list of other nodes it connections.  The inner list integers, reference the node index in the out list.
    /// Generates two collections: a list of NodeConnection objects along witha list of nodes, each containg a list of indexes into  the nodeConnection list.
    /// The important bit here is that the output lists, while in a similar format, will reference unique connection objects, rather than other nodes.
    /// </summary>
    /// <param name="nodeConnections"></param>
    /// <param name="allConnectionsList"></param>
    /// <param name="connectionIndicesInNodes"></param>
    static void BuildConnections(List<List<int>> nodeConnections, out List<NodeConnection> allConnectionsList, out List<List<int>> connectionIndicesInNodes)
    {
        int nodeCount = nodeConnections.Count;

        allConnectionsList = new List<NodeConnection>();
        connectionIndicesInNodes = new List<List<int>>(nodeCount);

        //for (int i = 0; i < nodeCount; i++) connectionIndicesInNodes.Add(new List<int>());

        
      //  Dictionary<NodeConnection, int> connectionLookup = new Dictionary<NodeConnection, int>();

        for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        {
            List<int> connections = nodeConnections[nodeIndex];
            //create list for connection indices for this node
            List<int> connectionIndicies = new List<int>();
            connectionIndicesInNodes.Add(connectionIndicies);
            Dictionary<NodeConnection, int> pairCounts = new Dictionary<NodeConnection, int>();
            for (int i = 0; i < connections.Count; i++)
            {
                int otherNodeIndex = connections[i];
                //use index 0 for all counting pairs
                NodeConnection pairKey = new NodeConnection(nodeIndex, otherNodeIndex, 0);
                int instanceCount=0;
                if (!pairCounts.TryGetValue(pairKey, out instanceCount)) 
                    instanceCount = 0;
                pairCounts[pairKey] = instanceCount + 1;
                //use actual index for storage in allConnectionList
                NodeConnection fullConnection = new NodeConnection(nodeIndex, otherNodeIndex, instanceCount);
                int fullConnectionIndex = allConnectionsList.IndexOf(fullConnection);
                if (fullConnectionIndex == -1)
                {
                    fullConnectionIndex = allConnectionsList.Count;
                    allConnectionsList.Add(fullConnection);// populate with new 
                }
                connectionIndicies.Add(fullConnectionIndex);//populate list for this node
            }
        }
        string log = "Connections per node";
        for (int i = 0; i < nodeCount; i++)
        {
            log += "\nNode["+i+"]: ";
            List<int> list = connectionIndicesInNodes[i];
            for (int j = 0; j < list.Count; j++)
            {
                int idx = list[j];
                NodeConnection conn = allConnectionsList[idx];
                log += conn+ " , ";
            }
        }
        Debug.Log(log);
    }


    /// <summary>
    /// This iterator will attmept to go through all variations of connection orders that are valid (meaning, the order of ALL connections, will not contradict the order of connections defined by any single node.
    /// Prunes iterations to prevent iterating deeper into known invalid orderings.
    /// </summary>
    /// <param name="connectionIndicesInNodes"></param>
    /// <param name="connectionCount"></param>
    /// <returns></returns>
    static IEnumerable<int[]> GenerateConnectionOrders(List<List<int>> connectionIndicesInNodes, int connectionCount, int randomSeed)
    {
        List<int>[] successors = new List<int>[connectionCount];
        int[] indegree = new int[connectionCount];

        for (int i = 0; i < connectionCount; i++) successors[i] = new List<int>();

        for (int n = 0; n < connectionIndicesInNodes.Count; n++)
        {
            List<int> list = connectionIndicesInNodes[n];

            for (int i = 1; i < list.Count; i++)
            {
                int a = list[i - 1];
                int b = list[i];

                successors[a].Add(b);
                indegree[b]++;
            }
        }

        List<int> available = new List<int>();
        for (int i = 0; i < connectionCount; i++)
            if (indegree[i] == 0)
                available.Add(i);

        int[] result = new int[connectionCount];
        bool[] used = new bool[connectionCount];

        foreach (int[] permutation in Backtrack(successors, indegree, available, used, result, 0))
            yield return permutation;

        IEnumerable<int[]> Backtrack(List<int>[] successors, int[] indegree, List<int> available, bool[] used, int[] result, int depth)
        {
            if (depth == result.Length)
            {
                int[] output = new int[result.Length];
                for (int i = 0; i < result.Length; i++) output[i] = result[i];
                yield return output;
                yield break;

            }
            if (randomSeed != 0)
            {
                int seed = randomSeed;
                available.Sort((a, b) =>
                {
                    int ha = (seed * 31 + a) * 31 + depth;
                    int hb = (seed * 31 + b) * 31 + depth;
                    return ha.CompareTo(hb);
                });
            }
            for (int i = 0; i < available.Count; i++)
            {
                int node = available[i];

                available.RemoveAt(i);
                used[node] = true;
                result[depth] = node;

                List<int> newlyAvailable = new List<int>();

                foreach (int nxt in successors[node])
                {
                    indegree[nxt]--;
                    if (indegree[nxt] == 0)
                    {
                        available.Add(nxt);
                        newlyAvailable.Add(nxt);
                    }
                }

                foreach (int[] backTrackResult in Backtrack(successors, indegree, available, used, result, depth + 1))
                    yield return backTrackResult;

                foreach (int nxt in successors[node])
                    indegree[nxt]++;

                foreach (int add in newlyAvailable)
                    available.Remove(add);

                available.Insert(i, node);
                used[node] = false;
            }
        }
    }

    static IEnumerable<int[]> GenerateNodeOrders(List<int> nodes, int index)
    {
        if (index == nodes.Count)
        {
            int[] result = new int[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) result[i] = nodes[i];
            yield return result;
            yield break;
        }

        for (int i = index; i < nodes.Count; i++)
        {
            int temp = nodes[index];
            nodes[index] = nodes[i];
            nodes[i] = temp;

            foreach (var result in GenerateNodeOrders(nodes, index + 1))
                yield return result;

            temp = nodes[index];
            nodes[index] = nodes[i];
            nodes[i] = temp;
        }
    }

    /// <summary>
    /// Validates that a candidate layout (node ordering + connection ordering) does NOT contain any
    /// geometric crossings where a node incorrectly lies between the endpoints of a connection that
    /// it is not part of.
    /// </summary>
    /// <param name="nodeOrder">
    /// A permutation of node indices defining vertical layout order (top to bottom).
    /// Index position in this list determines the node's row in the final layout.
    /// </param>
    /// <param name="connectionOrder">
    /// A permutation of connection indices defining horizontal layout order (left to right).
    /// Index position in this list determines the connection's column in the final layout.
    /// </param>
    /// <param name="allConnectionsList">
    /// Complete list of all unique connections, where each entry stores endpoint node indices (nodeAIndex, nodeBIndex).
    /// Must be consistent with nodeConnections input structure used during construction.
    /// </param>
    /// <param name="connectionIndicesInNodes">
    /// Adjacency mapping from each node to the list of connection indices it participates in.
    /// Each connection index refers into allConnectionsList.
    /// </param>
    /// <returns>
    /// True if the given node and connection order combination produces a valid non-crossing layout
    /// under the constraint that no node lies strictly between the endpoints of any connection it is not part of.
    /// False if any such violation exists.
    /// </returns>
    /// 
    static bool ValidateNodeAndConnectionOrderingCombination(List<int> nodeOrder,List<int> connectionOrder,List<NodeConnection> allConnectionsList,List<List<int>> connectionIndicesInNodes)
    {
        int totalNodeCount = nodeOrder.Count;
        int totalConnectionCount = connectionOrder.Count;



        // connectionIndex -> column lookup (from permutation)
        int[] connectionIndexToColumn = new int[totalConnectionCount];
        for (int col = 0; col < totalConnectionCount; col++)
        {
            int connectionIndex = connectionOrder[col];
            connectionIndexToColumn[connectionIndex] = col;
        }

        // nodeIndex -> row lookup (inverse of nodeOrder)
        int[] nodeIndexToRow = new int[totalNodeCount];
        int[] nodeStartColumn = new int[totalNodeCount];
        int[] nodeEndColumn = new int[totalNodeCount];
        for (int row = 0; row < totalNodeCount; row++)
        {
            int nodeIndex = nodeOrder[row];
            nodeIndexToRow[nodeIndex] = row;
            int startColumn = totalConnectionCount;
            int endColumn = -1;
            List<int> nodeConnections = connectionIndicesInNodes[nodeIndex];
            foreach (int usedConnectionIdx in nodeConnections)
            {
                int column = connectionIndexToColumn[usedConnectionIdx];
                startColumn = Mathf.Min(startColumn, column);
                endColumn = Mathf.Max(endColumn, column);
            }
            nodeStartColumn[nodeIndex] = startColumn;
            nodeEndColumn[nodeIndex] = endColumn;
        }

        // validate each connection globally
        for (int connectionIndex = 0; connectionIndex < totalConnectionCount; connectionIndex++)
        {
            NodeConnection connection = allConnectionsList[connectionIndex];
            int connectionColumn = connectionIndexToColumn[connectionIndex];
            int rowA = nodeIndexToRow[connection.nodeAIndex];
            int rowB = nodeIndexToRow[connection.nodeBIndex];

            int minRow = rowA < rowB ? rowA : rowB;
            int maxRow = rowA > rowB ? rowA : rowB;

            // check every row strictly between endpoints
            for (int row = minRow + 1; row < maxRow; row++)
            {
                int nodeAtRow = nodeOrder[row];
                //does node start and end, before or after this connection

                int startColumn = nodeStartColumn[nodeAtRow];
                int endColumn = nodeEndColumn[nodeAtRow];

                if (startColumn < connectionColumn && endColumn > connectionColumn)
                {
                    // only endpoints are allowed inside the span
                    if (nodeAtRow != connection.nodeAIndex &&
                      nodeAtRow != connection.nodeBIndex)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Used to alter the order of the input nodes.  Also alters the integer values in the list, to reflect the new index values of nodes.
    /// </summary>
    /// <param name="nodeConnectionsByNode"></param>
    /// <param name="rng"></param>
    /// <returns></returns>
    static List<List<int>> ShuffleNodeConnectionsByNode(List<List<int>> nodeConnectionsByNode,System.Random rng)
    {
        int nodeCount = nodeConnectionsByNode.Count;

        // 1. Create and shuffle node indices
        List<int> newNodeOrder = new List<int>(nodeCount);
        for (int i = 0; i < nodeCount; i++)
            newNodeOrder.Add(i);

        for (int i = nodeCount - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = newNodeOrder[i];
            newNodeOrder[i] = newNodeOrder[j];
            newNodeOrder[j] = tmp;
        }

        // 2. Build inverse map: oldIndex -> newIndex
        int[] oldToNew = new int[nodeCount];
        for (int newIndex = 0; newIndex < nodeCount; newIndex++)
        {
            int oldIndex = newNodeOrder[newIndex];
            oldToNew[oldIndex] = newIndex;
        }

        // 3. Build remapped adjacency list
        List<List<int>> remapped = new List<List<int>>(nodeCount);
        for (int i = 0; i < nodeCount; i++)
            remapped.Add(new List<int>());

        for (int oldNode = 0; oldNode < nodeCount; oldNode++)
        {
            List<int> oldConnections = nodeConnectionsByNode[oldNode];

            for (int i = 0; i < oldConnections.Count; i++)
            {
                int oldTarget = oldConnections[i];
                int newTarget = oldToNew[oldTarget];

                remapped[oldToNew[oldNode]].Add(newTarget);
            }
        }

        return remapped;
    }

    static IEnumerable<(List<int> nodeOrder, List<int> connectionOrder)> FindValidLayouts(int nodeCount,List<List<int>> connectionIndicesInNodes,List<NodeConnection> allConnectionsList, int randomSeed)
    {
        List<int> baseNodeOrder = new List<int>(nodeCount);
        for (int i = 0; i < nodeCount; i++) baseNodeOrder.Add(i);

        int connectionCount = allConnectionsList.Count;

        foreach (int[] connectionOrder in GenerateConnectionOrders(connectionIndicesInNodes, connectionCount, randomSeed))
        {
            Debug.Log("Trying connection ordering" + string.Join(",", connectionOrder));
            foreach (int[] nodeOrder in GenerateNodeOrders(baseNodeOrder, 0))
            {
                List<int> nodeOrderList = new List<int>(nodeOrder);
                Debug.Log("Trying node ordering: "+ string.Join(",",nodeOrderList));
                if (ValidateNodeAndConnectionOrderingCombination(nodeOrderList,new List<int>(connectionOrder),allConnectionsList,connectionIndicesInNodes))
                {
                    yield return (nodeOrderList, new List<int>(connectionOrder));
                }
                
            }
            
        }
    }

    /// <summary>
    /// Attempts to find the first valid non-crossing layout for a graph defined by per-node ordered connections.
    /// The function builds unique edge instances, then searches through valid connection orderings and node orderings
    /// until it finds a combination that satisfies all geometric constraints.
    ///
    /// A valid layout ensures:
    /// - Connection ordering respects all per-node ordering constraints
    /// - Node ordering produces no crossings where a node lies between endpoints of a connection it is not part of
    /// </summary>
    /// <param name="nodeConnectionsByNode">
    /// Input adjacency list where each node contains an ordered list of connected node indices.
    /// Each entry defines connection intent and ordering constraints local to that node.
    /// </param>
    /// <param name="nodeOrder">
    /// Output list representing the first valid vertical ordering of nodes (top to bottom).
    /// Null if no valid layout exists.
    /// </param>
    /// <param name="connectionOrder">
    /// Output list representing the first valid horizontal ordering of connections (left to right).
    /// Null if no valid layout exists.
    /// </param>
    /// <returns>
    /// True if a valid layout was found; otherwise false.
    /// </returns>
    public static bool FindFirstValidLayout(List<List<int>> nodeConnectionsByNode, out List<int> nodeOrder, out List<int> connectionOrder, out List<NodeConnection> allConnectionsList, int randomSeed=0)
    {
        if (randomSeed != 0)
        {
            System.Random rng = new System.Random(randomSeed);
            nodeConnectionsByNode = ShuffleNodeConnectionsByNode(nodeConnectionsByNode, rng);
        }
        BuildConnections(nodeConnectionsByNode, out allConnectionsList, out List<List<int>> connectionIndicesInNodes);

        int nodeCount = nodeConnectionsByNode.Count;

        foreach (var result in FindValidLayouts(nodeCount,connectionIndicesInNodes,allConnectionsList, randomSeed))
        {
            nodeOrder = result.nodeOrder;
            connectionOrder = result.connectionOrder;
            return true;
        }
        nodeOrder = null;
        connectionOrder = null;
        return false;
    }

    /// <summary>
    /// Creates a text visualization of a topology layout.
    /// Nodes are drawn as horizontal '-' spans between their first and last connection columns.
    /// Connections are drawn as vertical '|' segments occupying fixed columns.
    /// </summary>
    public static string DrawLayoutText(
        List<int> nodeOrder,
        List<int> connectionOrder,
        List<NodeConnection> allConnectionsList,
        Topology.Node[] nodes)
    {
        int nodeCount = nodeOrder.Count;
        int connectionCount = connectionOrder.Count;

        int[] nodeIndexToRow = new int[nodeCount];
        for (int row = 0; row < nodeCount; row++)
            nodeIndexToRow[nodeOrder[row]] = row;

        int[] connectionIndexToColumn = new int[connectionCount];
        for (int col = 0; col < connectionCount; col++)
            connectionIndexToColumn[connectionOrder[col]] = col * 4;

        int width = Mathf.Max(4, connectionCount * 4);
        int height = Mathf.Max(1, nodeCount * 3 - 2);

        char[,] grid = new char[height, width];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[y, x] = ' ';

        // Draw connections.
        for (int connectionIndex = 0; connectionIndex < connectionCount; connectionIndex++)
        {
            NodeConnection connection = allConnectionsList[connectionIndex];

            int x = connectionIndexToColumn[connectionIndex];

            int rowA = nodeIndexToRow[connection.nodeAIndex];
            int rowB = nodeIndexToRow[connection.nodeBIndex];

            int startY = Mathf.Min(rowA, rowB) * 3;
            int endY = Mathf.Max(rowA, rowB) * 3;

            for (int y = startY + 1; y < endY; y++)
                grid[y, x] = '|';
        }

        // Draw nodes.
        for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        {
            int minColumn = int.MaxValue;
            int maxColumn = int.MinValue;

            for (int connectionIndex = 0; connectionIndex < connectionCount; connectionIndex++)
            {
                NodeConnection connection = allConnectionsList[connectionIndex];

                if (connection.nodeAIndex != nodeIndex &&
                    connection.nodeBIndex != nodeIndex)
                    continue; //does not touch this node

                //Debug.Log("Node["+nodeIndex+"] touches connection ["+connectionIndex+"]:  nodeAidx:"+connection.nodeAIndex+"  nodeBidx: " + connection.nodeBIndex);

                int column = connectionIndexToColumn[connectionIndex];

                if (column < minColumn) minColumn = column;
                if (column > maxColumn) maxColumn = column;
            }

            if (minColumn == int.MaxValue)
            {
                minColumn = 0;
                maxColumn = 0;
            }

            int y = nodeIndexToRow[nodeIndex] * 3;

            for (int x = minColumn; x <= maxColumn + 2 && x < width; x++)
                grid[y, x] = '-';

            string label = nodeIndex.ToString();

            for (int i = 0; i < label.Length && minColumn + i < width; i++)
                grid[y, minColumn + i] = label[i];
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int y = 0; y < height; y++)
        {
            int lastNonSpace = width - 1;

            while (lastNonSpace >= 0 && grid[y, lastNonSpace] == ' ')
                lastNonSpace--;

            for (int x = 0; x <= lastNonSpace; x++)
                sb.Append(grid[y, x]);

            sb.Append('\n');
        }
        for (int n = 0; n < nodes.Length; n++)
        {
            sb.Append("[" + n + "]: " + nodes[n].name+ "\n");
        }

        return sb.ToString();
    }

}