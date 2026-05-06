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

        public bool isPartOfTwoWayEdge()
        {
            Node A = NodeA;
            Node B = NodeB;
            int seqA = A.MapEdgeIndexToSeqIndex(mapIndex);
            int seqB = B.MapEdgeIndexToSeqIndex(mapIndex);

            int checkA, checkB;
            Edge checkEdge;
            int testPassCount = 0;

            checkA = seqA - 1;
            if (CheckSeqIdx(checkA,A)) testPassCount++;
            checkA = seqA + 1;
            if (CheckSeqIdx(checkA, A)) testPassCount++;

            checkB = seqB - 1;
            if (CheckSeqIdx(checkB, B)) testPassCount++;
            checkB = seqB + 1;
            if (CheckSeqIdx(checkB, B)) testPassCount++;

            return (testPassCount > 1);

            bool CheckSeqIdx(int checkSeqIdx, Node inNode)
            {
                if (checkSeqIdx >= 0 && checkSeqIdx < inNode.edgeSequence.Length)
                {
                    checkEdge = inNode.EdgeInSequence(checkSeqIdx);
                    if (checkEdge.nodeBidx == nodeAidx && checkEdge.nodeAidx == nodeBidx)
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
        if (!IsAnalyticallyPlanar())
            throw new System.Exception("Topology fails planar test");
    }

    //geometric layout information
    public partial class Node
    {

        public Vector2 uvPosition;
        public float rotationAngleRad;
        const float distanceBetweenEdges = 0.02f;
        public float drawnLength
        {
            get
            {
                if (edgeSequence.Length == 1) return distanceBetweenEdges;
                return totalLength;
            }
        }
        float totalLength => distanceBetweenEdges * (edgeSequence.Length-1);
        float GetEdgeConnectionOffsetSignedDist(int edgeSeqIndex)
        {
            float offsetFromStart = distanceBetweenEdges * edgeSeqIndex;
            float start = -totalLength / 2f;
            return start + offsetFromStart;
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
        public void UpdatePositionRotation(bool thenReset = true)
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
        // called by layout iterator
        public void ApplyTorqueToNode(float torque)
        {
            angularVelocity += torque;
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
    
    
    bool IsAnalyticallyPlanar()
    {
        // 1. DATA PREPARATION: Identify "Physical Touches"
        // We map unique pairs of (NodeA, NodeB) to ignore the "Two-Way" edge inflation.
        // We also preserve the ORDER from your edgeSequence to maintain the rotation system.
        var simplifiedSequences = new Dictionary<int, List<int>>();

        foreach (var node in nodes)
        {
            List<int> physicalNextNodes = new List<int>();
            int lastNodeAdded = -1;

            foreach (int edgeIdx in node.edgeSequence)
            {
                if (edges[edgeIdx].isTeleport) continue;

                int neighborId = (edges[edgeIdx].NodeA.mapIndex == node.mapIndex)
                    ? edges[edgeIdx].NodeB.mapIndex
                    : edges[edgeIdx].NodeA.mapIndex;

                // Rule: Sequential edges to the same neighbor = a single "touch"
                if (neighborId != lastNodeAdded)
                {
                    physicalNextNodes.Add(neighborId);
                    lastNodeAdded = neighborId;
                }
            }

            // Handle the "Wrap-around" case: If first and last neighbor are the same, collapse them
            if (physicalNextNodes.Count > 1 && physicalNextNodes[0] == physicalNextNodes[physicalNextNodes.Count - 1])
            {
                physicalNextNodes.RemoveAt(physicalNextNodes.Count - 1);
            }

            simplifiedSequences[node.mapIndex] = physicalNextNodes;
        }

        // 2. COUNTING ACTUAL EDGES (Unique Undirected Connections)
        HashSet<(int, int)> uniquePhysicalEdges = new HashSet<(int, int)>();
        foreach (var entry in simplifiedSequences)
        {
            foreach (int neighborId in entry.Value)
            {
                int a = Mathf.Min(entry.Key, neighborId);
                int b = Mathf.Max(entry.Key, neighborId);
                uniquePhysicalEdges.Add((a, b));
            }
        }

        // 3. FACE WALKING (On the Simplified Topology)
        int faces = 0;
        HashSet<(int, int)> visitedTransitions = new HashSet<(int, int)>(); // (FromNode, ToNode)

        foreach (var nodeEntry in simplifiedSequences)
        {
            int u = nodeEntry.Key;
            for (int i = 0; i < nodeEntry.Value.Count; i++)
            {
                int v = nodeEntry.Value[i];

                if (visitedTransitions.Contains((u, v))) continue;

                // Start a new Face Walk
                faces++;
                int curr = u;
                int next = v;

                while (!visitedTransitions.Contains((curr, next)))
                {
                    visitedTransitions.Add((curr, next));

                    int prevNode = curr;
                    curr = next;

                    // At the new node, find where we came from and take the NEXT neighbor in sequence
                    List<int> neighbors = simplifiedSequences[curr];
                    int entryIdx = neighbors.IndexOf(prevNode);

                    // Logic: Go to the neighbor that is +1 (Clockwise) from where we entered
                    int nextIdx = (entryIdx + 1) % neighbors.Count;
                    next = neighbors[nextIdx];
                }
            }
        }

        // 4. GROUP COUNTING (Connectivity)
        int groups = 0;
        HashSet<int> unvisited = new HashSet<int>(simplifiedSequences.Keys);
        while (unvisited.Count > 0)
        {
            groups++;
            Queue<int> q = new Queue<int>();
            var en = unvisited.GetEnumerator(); en.MoveNext();
            q.Enqueue(en.Current);
            while (q.Count > 0)
            {
                int n = q.Dequeue();
                if (!unvisited.Contains(n)) continue;
                unvisited.Remove(n);
                foreach (int neighbor in simplifiedSequences[n]) q.Enqueue(neighbor);
            }
        }

        // 5. EULER CALCULATION
        int V = nodes.Length;
        int E = uniquePhysicalEdges.Count;
        int F = faces;
        int G = groups;

        // Result = V - E + F. Planar target is 1 + G (usually 2)
        int euler = V - E + F;
        int target = 1 + G;
        bool isPlanar = (euler == target);

        string report = $"V:{V} - E:{E} + F:{F} = {euler} (Target: {target}). Result: {(isPlanar ? "PLANAR" : "NON-PLANAR")}";
        Debug.Log($"<color={(isPlanar ? "green" : "red")}><b>--- Topology Report ---</b>\n{report}</color>");

        return isPlanar;
    }

}

public class TopoViz : MonoBehaviour
{
    [Header("Simulation Settings")]
    public bool runSimulation = true;
 public float simulationSpeed = 1f;
 public float damping = 0.1f;

    [Header("Force Strengths")]
    public float springStrength = 5f;
    public float nodeRepulsion = 0.5f;
    public float edgeRepulsion = 2f;
    public float edgeUncrossingStength = 2f;
    public float alignmentTorque = 2f;
    public float perpendicularStrength = 0.01f;
    public float idealEdgeLength = 0.2f;

    private Topology state;
    private Coroutine layoutCoroutine;

    private void OnEnable()
    {
        //state = BuildSampleTopology();
        //state = BuildMediumTopology();
        state = BuildComplexTopology();
        layoutCoroutine = StartCoroutine(LayoutRoutine());
    }

    private IEnumerator LayoutRoutine()
    {
        //memory reuse
        Vector2 p1;
        Vector2 p2;
        Vector2 p3;
        Vector2 p4;

        while (true)
        {
            if (runSimulation && state != null)
            {
                ApplyForces();
                UpdateTopology();
            }
            yield return null;
        }

        void ApplyForces()
        {
            // 1. Node-Node Repulsion (Keep nodes away from each other)
            for (int i = 0; i < state.nodes.Length; i++)
            {
                Topology.Node nodeA = state.nodes[i];
                for (int j = i + 1; j < state.nodes.Length; j++)
                {
                    Vector2 dir = nodeA.uvPosition - state.nodes[j].uvPosition;
                    float dist = Mathf.Max(dir.magnitude, 0.01f);
                    Vector2 force = (dir.normalized * nodeRepulsion) / (dist * dist);

                    nodeA.ApplyForce(force * Time.fixedDeltaTime);
                    state.nodes[j].ApplyForce(-force * Time.fixedDeltaTime);
                    
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
            foreach (var edge in state.edges)
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
                for (int i = 0; i < state.edges.Length; i++)
                {
                    for (int j = i + 1; j < state.edges.Length; j++)
                    {
                        Topology.Edge e1 = state.edges[i];
                        Topology.Edge e2 = state.edges[j];
                        if (e1.isTeleport || e2.isTeleport) continue;
                        if (state.EdgesShareBothNodesAndAreSequentialInThem(e1, e2)) continue;
                        HandleEdgeRepulsion(e1, e2);
                        HandleCrossingEdges(e1, e2);
                    }
                }
            }
        }

        void ApplyAlignmentTorque(Topology.Node node, int seqIdx, Vector2 targetPos)
        {
            Vector2 toTarget = (targetPos - node.uvPosition).normalized;
            Vector2 connectionDir = (node.GetEdgeConnectionPosition(seqIdx) - node.uvPosition).normalized;

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
            foreach (var node in state.nodes)
            {
                node.UpdatePositionRotation(false); // Update without immediate reset

                // Apply damping/friction
                node.ApplyDamping(damping);
            }
        }
    }

    static public Topology BuildSampleTopology()
    {
        var topology = new Topology();

        var start = new Topology.Node(topology,0,"Start", new int[1] { 0 });
        var mainPath = new Topology.Node(topology, 1, "MainPath", new int[6] { 0, 1, 2, 3, 6, 7 });
        var branch1 = new Topology.Node(topology, 2, "Branch1", new int[3] { 1,4,5 });
        var deadEnd2 = new Topology.Node(topology, 3, "DeadEnd2", new int[2] { 2,3 } );
        var branch1Retun = new Topology.Node(topology, 4, "branch1Retun", new int[3] { 4,5,6 });
        var end = new Topology.Node(topology, 5, "End", new int[1] { 7 });

        Topology.Node[] nodes = new Topology.Node[] { start, mainPath, branch1, deadEnd2, branch1Retun, end };
        int[] fixedNodes = new int[] { 0, 5 };
        
        Topology.Edge[] edges = new Topology.Edge[]{
        new Topology.Edge(topology, 0, start.mapIndex, mainPath.mapIndex),
        new Topology.Edge(topology, 1, mainPath.mapIndex, branch1.mapIndex),
        new Topology.Edge(topology, 2, mainPath.mapIndex, deadEnd2.mapIndex),
        new Topology.Edge(topology, 3, deadEnd2.mapIndex, mainPath.mapIndex),
        new Topology.Edge(topology, 4, branch1.mapIndex, branch1Retun.mapIndex),
        new Topology.Edge(topology, 5, branch1Retun.mapIndex, branch1.mapIndex),
        new Topology.Edge(topology, 6, branch1Retun.mapIndex, mainPath.mapIndex),
        new Topology.Edge(topology, 7, mainPath.mapIndex, end.mapIndex) };

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
            node.uvPosition = new Vector2(Random.value, Random.value);
            node.rotationAngleRad = 2f * Mathf.PI * Random.value;
        }
        return topology;
    }
    static public Topology BuildMediumTopology()
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
            node.uvPosition = new Vector2(Random.value, Random.value);
            node.rotationAngleRad = 2f * Mathf.PI * Random.value;
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
    void OnRenderObject()
    {
        if (state == null) return;
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

        // draw edges
        GL.Begin(GL.LINES);

        for (int i = 0; i < state.edges.Length; i++)
        {
            Topology.Edge edge = state.edges[i];

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

        for (int i = 0; i < state.nodes.Length; i++)
        {
            Topology.Node node = state.nodes[i];

            
            if (state.isNodeFixed(i))
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