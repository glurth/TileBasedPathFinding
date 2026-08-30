using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

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
        static public float defaultDistanceBetweenEdges = 0.005f;
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

        Vector2 accumulatedForce;
        public Vector2[] accumulatedEdgeForces;
        public float[] edgeSpacingVelocity;
        public void ResetForceAccumulators()
        {
            if (accumulatedEdgeForces == null || accumulatedEdgeForces.Length != edgeSequence.Length)
                accumulatedEdgeForces = new Vector2[edgeSequence.Length];

            accumulatedForce = Vector2.zero;

            for (int i = 0; i < accumulatedEdgeForces.Length; i++)
                accumulatedEdgeForces[i] = Vector2.zero;
        }
        public void AccumulateEdgeConnectionForce(int seqIndex, Vector2 force)
        {
            accumulatedEdgeForces[seqIndex] += force;
        }

        public void AccumulateForce(Vector2 force)
        {
            accumulatedForce += force;
        }

        public void AccumulateForceAtPosition(Vector2 position, Vector2 force)
        {
            accumulatedForce += force;
            angularVelocity += 100f * Cross(position - uvPosition, force);

            float Cross(Vector2 a, Vector2 b)
            {
                return a.x * b.y - a.y * b.x;
            }
        }
        public void ResolveAccumulatedForces()
        {
            Vector2 axis = new Vector2(Mathf.Cos(rotationAngleRad), Mathf.Sin(rotationAngleRad));

            velocity += accumulatedForce;
            //string s = "Accumulating force on node " + mapIndex;

            for (int i = 0; i < accumulatedEdgeForces.Length; i++)
            {
                Vector2 force = accumulatedEdgeForces[i];

                float spacingForce = Vector2.Dot(force, axis);

                if (i > 0)//if not left-most node
                    edgeSpacingVelocity[i - 1] += spacingForce;

                if (i < edgeSpacingVelocity.Length) //if not rightmost node
                    edgeSpacingVelocity[i] -= spacingForce;

                
                Vector2 perpendicular = force - axis * spacingForce;
               // s += "edge in seq[" + i + "] total force: "+ force+"   spacingForce = " + spacingForce +"  plus perpendicular force = "+ perpendicular;
               // Debug.Log(s);
                velocity += perpendicular;
                angularVelocity += 100f * Cross(GetEdgeConnectionOffset(i), perpendicular);
            }
            
            float Cross(Vector2 a, Vector2 b)
            {
                return a.x * b.y - a.y * b.x;
            }
        }

        // called at the start(or end if after Update) of each iteration "frame"
        public void Reset()
        {
            velocity = Vector2.zero;
            angularVelocity = 0;
            if (edgeSpacingVelocity != null)
            {
                for (int i = 0; i < edgeSpacingVelocity.Length; i++)
                    edgeSpacingVelocity[i] = 0;
            }
        }
        // called at the end of each iteration "frame"
        public void UpdatePositionAndRotation(bool thenReset = true)
        {


                for (int i = 0; i < edgeSpacing.Length; i++)
                {
                    edgeSpacing[i] += edgeSpacingVelocity[i];

                    // prevent invalid/negative spacing
                  //  if (edgeSpacing[i] < defaultDistanceBetweenEdges * 0.1f)
                    //    edgeSpacing[i] = defaultDistanceBetweenEdges * 0.1f;
                }


            if (!map.isNodeFixed(mapIndex))
            {
                uvPosition += velocity;
                rotationAngleRad += angularVelocity;
            }

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
        public void XXApplyForceToEdgeConnection(int seqIndex,Vector2 force)
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
        public void XXApplyForceAtPosition(Vector2 position, Vector2 force)
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

            for (int i = 0; i < edgeSpacingVelocity.Length; i++)
                edgeSpacingVelocity[i] *= (1f - amount);
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

        Debug.Log("Starting Parse Process");
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

        List<List<string>> sequenceKeys = new List<List<string>>();
        HashSet<int> declaredNodes = new HashSet<int>();

        Topology topo = new Topology();

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            Debug.Log("   Processing line: " + line);

            if (line.Length == 0)
                continue;

            string[] parts = line.Split(':');

            if (parts.Length != 2)
                throw new System.Exception("Bad line: " + line);

            int from = GetNode(parts[0].Trim());
            declaredNodes.Add(from);

            while (sequenceKeys.Count <= from)
                sequenceKeys.Add(new List<string>());

            string[] tokens = parts[1].Split(',');

            foreach (string tRaw in tokens)
            {
                string t = tRaw.Trim();

                if (t.Length == 0)
                    continue;

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

                while (sequenceKeys.Count <= to)
                    sequenceKeys.Add(new List<string>());

                string key = MakeKey(from, to, label);

                sequenceKeys[from].Add(key);

                if (!edges.TryGetValue(key, out var e))
                    e = (from, to, false, false);

                if (!isOut && !isIn)
                    e = (e.a, e.b, true, true);
                else if (isOut)
                    e = (e.a, e.b, true, e.hasB);
                else
                    e = (e.a, e.b, e.hasA, true);

                edges[key] = e;
            }
        }

        List<Topology.Edge> edgeList = new List<Topology.Edge>();
        Dictionary<string, int> edgeLookup = new Dictionary<string, int>();

        foreach (var kv in edges)
        {
            var e = kv.Value;
            Debug.Log("   Processing edge: " + kv.Key);
            if (!e.hasA || !e.hasB)
                throw new System.Exception("Incomplete edge: " + kv.Key);

            int index = edgeList.Count;

            edgeLookup[kv.Key] = index;
            edgeList.Add(new Topology.Edge(topo, index, e.a, e.b));
        }

        Topology.Node[] nodes = new Topology.Node[nodeNames.Count];

        string debugString = "PARSE DEBUG";

        for (int i = 0; i < nodeNames.Count; i++)
        {
            List<int> sequence = new List<int>();

            if (declaredNodes.Contains(i))
            {
                foreach (string key in sequenceKeys[i])
                    sequence.Add(edgeLookup[key]);
            }
            else
            {
                for (int e = 0; e < edgeList.Count; e++)
                {
                    if (edgeList[e].nodeAidx == i ||
                        edgeList[e].nodeBidx == i)
                    {
                        sequence.Add(e);
                    }
                }
            }

            nodes[i] = new Topology.Node(
                topo,
                i,
                nodeNames[i],
                sequence.ToArray());

            debugString += "\nNode: " + i +
                "  seq: " + string.Join(",", sequence);
        }

        for (int i = 0; i < edgeList.Count; i++)
        {
            debugString += "\nEdges: " + i +
                "  connects: " +
                edgeList[i].nodeAidx +
                " , " +
                edgeList[i].nodeBidx;
        }

   //     Debug.Log(debugString);

        topo.SetupTopo(nodes, edgeList.ToArray(), new int[0]);

        return topo;
    }

    
    public void AssignInitialLayout(int[] rowByNodeIndex, int[] columnByConnectionIndex)
    {
        Debug.Log("starting Initial Layout");
      //  Dictionary<int, List<int>> simplifiedSequences = BuildSimplifiedSequences();
        bool normalizeNodeWidth = true;
        string s = "Positioning Log\n";

        s += "-Given columnByConnectionIndex array: " + string.Join(",", columnByConnectionIndex);

        //asign position of nodes
        for (int nodeIndex = 0; nodeIndex < rowByNodeIndex.Length; nodeIndex++)
        {
            int row = rowByNodeIndex[nodeIndex];
            Node node = nodes[nodeIndex];
            s += "   Node row: " + row + "  node index: " + nodeIndex + "\n";

            float y = row;
            if (node.edgeSequence.Length == 0) throw new System.Exception("uNEXPECETED NODE WITH NO EDGES. NOT SURE WHERE TO PLACE IT");
            int startEdgeIndex = node.edgeSequence[0];
            int startEdgeColumn = columnByConnectionIndex[startEdgeIndex];
            float x = startEdgeColumn;
            s += "       Starting Connection-  connetcionIndex: " + startEdgeIndex + "  connectionColumn:" + startEdgeColumn + "\n";
            if (node.edgeSequence.Length > 1)
            {
                int endEdgeIndex = node.edgeSequence[node.edgeSequence.Length - 1];
                int endEdgeColumn = columnByConnectionIndex[endEdgeIndex];
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
            node.edgeSpacingVelocity = new float[node.edgeSequence.Length - 1];
            node.accumulatedEdgeForces = new Vector2[node.edgeSequence.Length];
            int prevColumn = 0;
            for (int i = 0; i < node.edgeSequence.Length; i++)
            {
                int edgeIndex = node.edgeSequence[i];
                int edgeColumn = columnByConnectionIndex[edgeIndex]; //connectionOrder[edgeIndex];
                s += "       Connection-  nodeSeq:" + i + "  connetcionIndex: " + edgeIndex + "  connectionColumn:" + edgeColumn + "\n";
                node.accumulatedEdgeForces[i] = Vector2.zero;
                if (i > 0)
                {
                    node.edgeSpacing[i - 1] = (edgeColumn - prevColumn) * Node.defaultDistanceBetweenEdges;
                    node.edgeSpacingVelocity[i - 1] = 0;
                }
                prevColumn = edgeColumn;
            }// end connections in current node loop

        }// end row/nodes loop
        Debug.Log(s);
        // ------------------------------------------------------------
        //  normalization 
        // ------------------------------------------------------------
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < nodes.Length; i++)
        {
            Node node = nodes[i];
            int endIndex = node.edgeSequence.Length - 1;

            Vector2 p = node.GetEdgeConnectionPosition(0);
            if (p.x < min.x) min.x = p.x;
            if (p.y < min.y) min.y = p.y;
            if (p.x > max.x) max.x = p.x;
            if (p.y > max.y) max.y = p.y;
            p = node.GetEdgeConnectionPosition(endIndex);
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
                for (int j = 0; j < spacingArray.Length; j++)
                {
                    spacingArray[j] *= oneOverSizeX;
                }
            }
        }
        
    }

    public void OLDGenerateInitialLayout()
    {
        bool normalizeNodeWidth = true;
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
            node.edgeSpacingVelocity = new float[node.edgeSequence.Length - 1];
            node.accumulatedEdgeForces = new Vector2[node.edgeSequence.Length];
            int prevColumn=0;
            for (int i = 0; i < node.edgeSequence.Length; i++)
            {
                int edgeIndex = node.edgeSequence[i];
                int edgeColumn= connectionIndexToColumn[edgeIndex]; //connectionOrder[edgeIndex];
                s += "       Connection-  nodeSeq:" + i + "  connetcionIndex: " + edgeIndex + "  connectionColumn:" + edgeColumn + "\n";
                node.accumulatedEdgeForces[i] = Vector2.zero;
                if (i > 0)
                {
                    node.edgeSpacing[i - 1] = (edgeColumn - prevColumn) * Node.defaultDistanceBetweenEdges;
                    node.edgeSpacingVelocity[i - 1] = 0;
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
                /*
                if (physicalNeighbors.Count > 1)
                {
                    if (physicalNeighbors[0] ==
                        physicalNeighbors[physicalNeighbors.Count - 1])
                    {
                        physicalNeighbors.RemoveAt(physicalNeighbors.Count - 1);
                    }
                }*/

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
                Node node = nodes[i];
                int endIndex = node.edgeSequence.Length - 1;
                
                Vector2 p = node.GetEdgeConnectionPosition(0);
                if (p.x < min.x) min.x = p.x;
                if (p.y < min.y) min.y = p.y;
                if (p.x > max.x) max.x = p.x;
                if (p.y > max.y) max.y = p.y;
                p = node.GetEdgeConnectionPosition(endIndex);
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
        //string s = "Start:MainBranch\n";
        //s += "MainBranch:Start,DeadEnd1,SecondaryBranch[left],DeadEnd3,SecondaryBranch[right],End\n";
        //s += "SecondaryBranch: MainBranch[left],DeadEnd2,MainBranch[right]";


        string s = "Start:MainBranch\n";
        s += "MainBranch:Start,DeadEnd1,SecondaryBranch[left],DeadEnd3,End,SecondaryBranch[right]\n";
        s += "SecondaryBranch: MainBranch[left],DeadEnd2,MainBranch[right]";
       // Debug.Log("Conmplex string: " + ComplexMaze1());
        s = ComplexMaze1();
        topo = Topology.Parse(s);



       // topo.GenerateInitialLayout();
        //topo.SetFixedNodes(new int[2] { 0, 5 });
        topo.SetFixedNodes(new int[1] { 0 });
        layoutCoroutine = StartCoroutine(InitialSolverRoutine());
        //layoutCoroutine = StartCoroutine(LayoutRoutine());
    }

    static string ComplexMaze1()
    {
        return @"Start: Gate
Gate: Start, Cistern, G1
Cistern: Gate, Fork, C1
Fork: Cistern, Gallery, F1
Gallery: Fork, Chamber, G5
Chamber: Gallery, Spiral, C5
Spiral: Chamber, Vault, S1
Vault: Spiral, Bridge, V1
Bridge: Vault, Crypt, B1
Crypt: Bridge, Junction, K1
Junction: Crypt, Shrine, J1
Shrine: Junction, Passage, R1
Passage: Shrine, Antechamber, P1
Antechamber: Passage, End, A1
End: Antechamber

G1: Gate, G2
G2: G1, G3
G3: G2

C1: Cistern, C2, C3
C2: C1
C3: C1, C4
C4: C3

F1: Fork, F2
F2: F1, F3, F4
F3: F2
F4: F2

G5: Gallery, G6, G8
G6: G5, G7
G7: G6
G8: G5

C5: Chamber, C6
C6: C5, C7
C7: C6, C8
C8: C7

S1: Spiral, S2, S4
S2: S1, S3
S3: S2
S4: S1

V1: Vault, V2, V4
V2: V1, V3
V3: V2
V4: V1

B1: Bridge, B2
B2: B1, B3
B3: B2, B4
B4: B3

K1: Crypt, K2, K3
K2: K1
K3: K1, K4
K4: K3

J1: Junction, J2, J4
J2: J1, J3
J3: J2
J4: J1

R1: Shrine, R2, R4
R2: R1, R3
R3: R2
R4: R1

P1: Passage, P2, P4
P2: P1, P3
P3: P2
P4: P1

A1: Antechamber, A2, A4
A2: A1, A3
A3: A2
A4: A1";
    }

    static string ComplexMaze2()
    {
        return @"Start: Gate
F2: F1, F3, F4
V3: V2
Gate: Start, Cistern, G1
C4: C3
J1: Junction, J2, J4
Gallery: Fork, Chamber, G5
A3: A2
S1: Spiral, S2, S4
Cistern: Gate, Fork, C1
R4: R1
B2: B1, B3
Antechamber: Passage, End, A1
G7: G6
K3: K1, K4
Passage: Shrine, Antechamber, P1
C6: C5, C7
F1: Fork, F2
V1: Vault, V2, V4
G2: G1, G3
Shrine: Junction, Passage, R1
C1: Cistern, C2, C3
P3: P2
Bridge: Vault, Crypt, B1
A1: Antechamber, A2, A4
S4: S1
G5: Gallery, G6, G8
C3: C1, C4
J3: J2
B4: B3
Crypt: Bridge, Junction, K1
R1: Shrine, R2, R4
C7: C6, C8
Fork: Cistern, Gallery, F1
V4: V1
P1: Passage, P2, P4
G8: G5
K1: Crypt, K2, K3
S2: S1, S3
A4: A1
Chamber: Gallery, Spiral, C5
R2: R1, R3
B1: Bridge, B2
C5: Chamber, C6
G1: Gate, G2
J4: J1
V2: V1, V3
P4: P1
C2: C1
S3: S2
B3: B2, B4
Junction: Crypt, Shrine, J1
A2: A1, A3
R3: R2
G6: G5, G7
Spiral: Chamber, Vault, S1
K4: K3
F3: F2
P2: P1, P3
Vault: Spiral, Bridge, V1
K2: K1
End: Antechamber";
    }


    [Header("Force Strengths")]
    public float springStrength = 5f;  // for between (edge/node) each connection point and: all other connections points, and boundry.
    public float perpendicularSpringStrength = 5f;  // for between (edge/node) each connection point and: all other connections points, and boundry.
    public float edgeSpacingStrength = 1f; // for between endpoints on the same node
    public float boundrySpacingStrength = 1f; // for between endpoints on the same node
    public float idealEdgeSpacing = 0.05f;
    public float idealEdgeLength = 0.2f;

    private IEnumerator InitialSolverRoutine()
    {
        TopologyLayout initialOrderingLayout = null;
        System.Exception exception = null;
        TopologyLayoutSolver solver = new TopologyLayoutSolver(topo);
        System.Threading.CancellationToken cancellationToken = new System.Threading.CancellationToken();
        yield return solver.SolveAsync(cancellationToken).ToCoroutine(
            value => initialOrderingLayout = value,
            ex => exception = ex);

        Debug.Log("TopologyLayoutSolver solved");
        if (exception != null)
        {
            Debug.LogException(exception);
          //  yield break;
        }
        Debug.Log("TopologyLayoutSolver solved");
        topo.AssignInitialLayout(initialOrderingLayout.RowByNodeIndex, initialOrderingLayout.ColumnByConnectionIndex);
        Debug.Log("AssignInitialLayout complete");
        yield return LayoutRoutine();
    }
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
                foreach (Topology.Node node in topo.nodes)
                    node.ResetForceAccumulators();

                foreach (Topology.Edge edge in topo.edges)
                {
                    HandleEndpoint(edge, true);
                    HandleEndpoint(edge, false);
                }

                foreach (Topology.Node node in topo.nodes)
                {
                   ApplySpacingSpringForces(node);
                }
                foreach (Topology.Node node in topo.nodes)
                {
                    node.ResolveAccumulatedForces();

                    node.UpdatePositionAndRotation(false);
                    node.ApplyDamping(damping);
                }
            }

        }

        void ApplySpacingSpringForces(Topology.Node node)
        {
            if (node.edgeSpacing == null || node.edgeSpacing.Length == 0)
                return;

            Vector2 axis = new Vector2(Mathf.Cos(node.rotationAngleRad), Mathf.Sin(node.rotationAngleRad));

            for (int i = 1; i < node.edgeSpacing.Length; i++)
            {
                float delta = node.edgeSpacing[i] - idealEdgeSpacing;//positive when spce is too big

                Vector2 force = axis * (-delta * edgeSpacingStrength * simulationSpeed);// towards start when spce is too big
                if (delta < 0)// if spring is being compressed- we need to increase force apart towards infinity as dist approaches zero
                {
                    float spacing = Mathf.Max(node.edgeSpacing[i], 0.0000001f);
                    force = axis * ((100f / spacing) * edgeSpacingStrength * simulationSpeed);// towards end (space is too small)
                }

                node.AccumulateEdgeConnectionForce(i-1, force);
                node.AccumulateEdgeConnectionForce(i, -force);
            }
        }

        void ApplyBoundarySpring(Vector2 position, Topology.Node node, int seqIndex)
        {
            float left = position.x;
            float right = 1f - position.x;
            float bottom = position.y;
            float top = 1f - position.y;

            float distance = left;
            Vector2 direction = Vector2.right;

            if (right < distance)
            {
                distance = right;
                direction = Vector2.left;
            }

            if (bottom < distance)
            {
                distance = bottom;
                direction = Vector2.up;
            }

            if (top < distance)
            {
                distance = top;
                direction = Vector2.down;
            }

            const float barrierDistance = 0.05f;

            if (distance >= barrierDistance)
                return;

            float strength = boundrySpacingStrength * (1f / Mathf.Max(distance, 0.0001f) - 1f / barrierDistance);

            Vector2 force = direction * strength * simulationSpeed;

            //node.ApplyForceToEdgeConnection(seqIndex, force);
            node.AccumulateEdgeConnectionForce(seqIndex, force);
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
            // Endpoint -> every other edge that does not touch endpointNode
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

                ApplySpring(endpointNode, seqIndex, endpointPos, other, t, nearest);
            }

            //
            // Endpoint -> every other node
            //
            foreach (Topology.Node other in topo.nodes)
            {
                if (other == endpointNode)
                    continue;

                Vector2 nearest = GetClosestPointOnNode(other, endpointPos, out _);

                ApplySpringToNode(other, nearest, endpointPos, endpointNode);
            }
            //
            // Endpoint -> own node
            //
            {
                Vector2 nearest = GetClosestPointOnNode(endpointNode, nearPoint, out _);

                ApplyEndpointToOwnNode(endpointNode, otherNode, nearPoint, nearest, otherEndpointPos);
            }
            //endpoint -> boundry edge
            ApplyBoundarySpring(endpointPos, endpointNode, seqIndex);

        }

        void ApplyEndpointToOwnNode(Topology.Node node, Topology.Node otherNode, Vector2 nearPoint, Vector2 nearestPoint, Vector2 otherNodeConnectionPosition)
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

            float strength = perpendicularSpringStrength * (1f / dist - 1f / barrierRadius);

            Vector2 force = delta.normalized * strength * simulationSpeed;

            bool nodeFixed = topo.isNodeFixed(node.mapIndex);
            bool otherNodeFixed = topo.isNodeFixed(otherNode.mapIndex);

            if (nodeFixed || otherNodeFixed)
                force *= 2f;

            if (!nodeFixed)
            {
                node.AccumulateForceAtPosition(nearestPoint, force);
            }

            if (!otherNodeFixed)
            {
                otherNode.AccumulateForceAtPosition(otherNodeConnectionPosition, -force);
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
                node.AccumulateEdgeConnectionForce(seqIndex, force);
            else
                force *= 2f;

            if (!nodeAFixed)
                otherEdge.NodeA.AccumulateEdgeConnectionForce(
                    otherEdge.NodeA.MapEdgeIndexToSeqIndex(otherEdge.mapIndex),
                    -force * (1f - t));

            if (!nodeBFixed)
                otherEdge.NodeB.AccumulateEdgeConnectionForce(
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
                endPointsNode.AccumulateForceAtPosition(endpointPos, force);

            if (!nodeFixed)
                node.AccumulateForceAtPosition(forcePos, -force);
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

        string log1 = "generating: ";
        for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        {
            List<int> connections = nodeConnections[nodeIndex];
            //create list for connection indices for this node
            List<int> connectionIndicies = new List<int>();
            connectionIndicesInNodes.Add(connectionIndicies);
            Dictionary<NodeConnection, int> pairCounts = new Dictionary<NodeConnection, int>();
            log1 += "\n node: " + nodeIndex;
            for (int i = 0; i < connections.Count; i++)
            {
                int otherNodeIndex = connections[i];
                log1 += "\n connection[" + i + "] in node.  ";
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
                log1 += "allConnections Index: " + fullConnectionIndex;
            }
        }
        string log = "Connections per node";
        for (int i = 0; i < nodeCount; i++)
        {
            log += "\nNode["+i+"]: ";
            List<int> connectionIndicesInNode = connectionIndicesInNodes[i];
            for (int j = 0; j < connectionIndicesInNode.Count; j++)
            {
                int idx = connectionIndicesInNode[j];
                NodeConnection conn = allConnectionsList[idx];
                log += conn+ " , ";
            }
        }
        Debug.Log(log1);
        Debug.Log(log);
    }


    /// <summary>
    /// This iterator will attmept to go through all variations of connection orders that are valid (meaning, the order of ALL connections, will not contradict the order of connections defined by any single node- so really we are ordering nodes.
    /// Prunes iterations to prevent iterating deeper into known invalid orderings.
    /// </summary>
    /// <param name="connectionIndicesInNodes"></param>
    /// <param name="connectionCount"></param>
    /// <returns></returns>
    static IEnumerable<int[]> GenerateConnectionOrders(List<List<int>> connectionIndicesInNodes, int connectionCount, int randomSeed)
    {
        List<int> result= new List<int>();
        foreach(List<List<int>> nodeOrder in Permutations.PermutationsOf<List<int>>(connectionIndicesInNodes))
        {
            result.Clear();
            foreach (List<int> nodeConnections in nodeOrder)
                foreach (int connectionIndex in nodeConnections)
                    result.Add(connectionIndex);
            yield return result.ToArray();

        }

        /*

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
       */
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
            //Debug.Log("Testing " + connection +" rows " + rowA + "-" + rowB);

            // check every row strictly between endpoints
            for (int row = minRow + 1; row < maxRow; row++)
            {
                int nodeAtRow = nodeOrder[row];


                int startColumn = nodeStartColumn[nodeAtRow];
                int endColumn = nodeEndColumn[nodeAtRow];

                    /*Debug.Log(
        " middle node " + nodeAtRow +
        " span " + nodeStartColumn[nodeAtRow] +
        "-" + nodeEndColumn[nodeAtRow] +
        " conn col " + connectionColumn);*/
                if (startColumn < connectionColumn && endColumn > connectionColumn)
                {
                    // only endpoints are allowed inside the span
                    if (nodeAtRow != connection.nodeAIndex &&
                        nodeAtRow != connection.nodeBIndex )
                    {
                        /*Debug.Log(
                                    "REJECT: connection " + connection +
                                    " col " + connectionColumn +
                                    " crosses node " + nodeAtRow +
                                    " span " + startColumn + "-" + endColumn);*/
                        return false;
                    }
                }
            }
        }
        Debug.Log("VALID NODE ORDER: " + string.Join(",", nodeOrder));
        Debug.Log("Node 3 connections:");
        foreach (int idx in connectionIndicesInNodes[3])
        {
            Debug.Log(allConnectionsList[idx] + " column " + connectionIndexToColumn[idx]);
        }
        Debug.Log("VALID CONN ORDER: " + string.Join(",", connectionOrder));
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
           // Debug.Log("Trying connection ordering" + string.Join(",", connectionOrder));
            foreach (int[] nodeOrder in GenerateNodeOrders(baseNodeOrder, 0))
            {
                List<int> nodeOrderList = new List<int>(nodeOrder);
             //   Debug.Log("Trying node ordering: "+ string.Join(",",nodeOrderList));
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