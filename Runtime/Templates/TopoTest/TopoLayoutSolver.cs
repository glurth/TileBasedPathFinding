using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;


public class TopologyLayout
{
    public int[] ColumnByConnectionIndex;
    public int[] RowByNodeIndex;

    public TopologyLayout(int connectionCount, int nodeCount)
    {
        ColumnByConnectionIndex = new int[connectionCount];
        RowByNodeIndex = new int[nodeCount];
    }

    /// <summary>
    /// Creates a text visualization of a topology layout.
    /// Nodes are drawn as horizontal '-' spans between their first and last connection columns.
    /// Connections are drawn as vertical '|' segments occupying fixed columns.
    /// </summary>
    public string DrawLayoutText(Topology topology)
    {
        int nodeCount = RowByNodeIndex.Length;
        int connectionCount = ColumnByConnectionIndex.Length;

        int[] nodeIndexToRow = RowByNodeIndex;

        /*int[] nodeIndexToRow = new int[nodeCount];
        for (int row = 0; row < nodeCount; row++)
            nodeIndexToRow[RowByNodeIndex[row]] = row;*/

        int[] connectionIndexToColumn = new int[connectionCount];
        for (int connectionIndex = 0;connectionIndex < connectionCount;connectionIndex++)
        {
            connectionIndexToColumn[connectionIndex] = ColumnByConnectionIndex[connectionIndex] * 4;
        }
        /*
        int[] connectionIndexToColumn = new int[connectionCount];
        for (int col = 0; col < connectionCount; col++)
            connectionIndexToColumn[ColumnByConnectionIndex[col]] = col * 4;*/

        int width = Mathf.Max(4, connectionCount * 4);
        int height = Mathf.Max(1, nodeCount * 3 - 2);

        char[,] grid = new char[height, width];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[y, x] = ' ';

        // Draw connections.
        for (int connectionIndex = 0; connectionIndex < connectionCount; connectionIndex++)
        {
            Topology.Edge connection = topology.edges[connectionIndex];

            int x = connectionIndexToColumn[connectionIndex];

            int rowA = nodeIndexToRow[connection.nodeAidx];
            int rowB = nodeIndexToRow[connection.nodeBidx];

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
                Topology.Edge connection = topology.edges[connectionIndex];

                if (connection.nodeAidx != nodeIndex &&
                    connection.nodeBidx != nodeIndex)
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
        for (int n = 0; n < topology.nodes.Length; n++)
        {
            sb.Append("[" + n + "]: " + topology.nodes[n].name + "\n");
        }

        return sb.ToString();
    }
}


public class TopologyLayoutImpossibleException : Exception
{
    public TopologyLayoutImpossibleException(string message) : base(message){}
}


public class TopologyLayoutSolver
{
    private const int OperationsBetweenYields = 1000;

    private readonly Topology topology;

    private int operationsSinceLastYield;
    public int totalOps = 0;

    public static async UniTask<TopologyLayout> SolveAsync(Topology topology, CancellationToken cancellationToken = default)
    {
        TopologyLayoutSolver solver = new TopologyLayoutSolver(topology);
        return await solver.SolveAsync(cancellationToken);
    }

    public TopologyLayoutSolver(Topology topology)
    {
        this.topology = topology;
    }


    public async UniTask<TopologyLayout> SolveAsync(
        CancellationToken cancellationToken = default)
    {
        operationsSinceLastYield = 0;
        totalOps = 0;
        UnityEngine.Debug.Log("generating initial constraint state");

        ConstraintState constraintState =
            CreateInitialConstraintState();
        UnityEngine.Debug.Log("computing node ordering based on constraints");
        int[] rowByNodeIndex = await FindNodeRowsAsync(constraintState,cancellationToken);
        UnityEngine.Debug.Log("computing connection columns");
        int[] columnByConnectionIndex = CreateConnectionColumns(constraintState);
        UnityEngine.Debug.Log("layout generated:  \nrowByNodeIndex:"+ string.Join(",", rowByNodeIndex) + "\ncolumnByConnectionIndex:"+ string.Join(",", columnByConnectionIndex) );
        //SimpleTopo.DrawLayoutText(rowByNodeIndex, columnByConnectionIndex, topology.edges, topology.nodes);
        TopologyLayout layout = new TopologyLayout(topology.edges.Length,topology.nodes.Length)
        {
            RowByNodeIndex = rowByNodeIndex,
            ColumnByConnectionIndex = columnByConnectionIndex
        };
        System.IO.File.WriteAllText("layoutOutput.txt", layout.DrawLayoutText(topology));
        //Debug.Log(layout.DrawLayoutText(topology));
        return layout;

    }


    private ConstraintState CreateInitialConstraintState()
    {
        ConstraintState constraintState =
            new ConstraintState(topology.edges.Length);

        /*
         * Every node gives us a fixed left-to-right ordering of its
         * connections.
         *
         * For example:
         *
         *     [ 7, 3, 12, 5 ]
         *
         * means:
         *
         *     7 < 3 < 12 < 5
         *
         * Only adjacent relationships need to be stored.
         */
        for (int nodeIndex = 0;
             nodeIndex < topology.nodes.Length;
             nodeIndex++)
        {
            int[] edgeSequence =
                topology.nodes[nodeIndex].edgeSequence;

            for (int edgeSequenceIndex = 0;
                 edgeSequenceIndex < edgeSequence.Length - 1;
                 edgeSequenceIndex++)
            {
                int precedingConnectionIndex =
                    edgeSequence[edgeSequenceIndex];

                int followingConnectionIndex =
                    edgeSequence[edgeSequenceIndex + 1];

                /*
                 * These constraints come directly from the topology.
                 * There is no layout choice involved yet, so a
                 * contradiction means the topology cannot be represented
                 * by this layout system.
                 */
                if (!constraintState.TryAddConstraint(
                        precedingConnectionIndex,
                        followingConnectionIndex))
                {
                    throw new TopologyLayoutImpossibleException(
                        "The topology contains contradictory connection ordering constraints.");
                }
            }
        }

        return constraintState;
    }


    private async UniTask<int[]> FindNodeRowsAsync(
        ConstraintState constraintState,
        CancellationToken cancellationToken)
    {
        int[] rowByNodeIndex =
            new int[topology.nodes.Length];

        /*
         * -1 means that the node has not yet been assigned a row.
         */
        for (int nodeIndex = 0;
             nodeIndex < rowByNodeIndex.Length;
             nodeIndex++)
        {
            rowByNodeIndex[nodeIndex] = -1;
        }

        /*
         * The node-row search is deliberately a depth-first search.
         *
         * At each level:
         *
         *     1. Choose an unplaced node.
         *     2. Give it the next available row.
         *     3. Resolve every connection whose second endpoint has
         *        now become known.
         *     4. If every resulting constraint is valid, continue.
         *     5. If the branch eventually fails, roll everything back
         *        and try another node or another left/right choice.
         *
         * A valid complete assignment is returned immediately.
         */
        bool foundValidLayout =
            await TryPlaceNextNodeAsync(
                rowByNodeIndex,
                0,
                constraintState,
                cancellationToken);

        if (!foundValidLayout)
        {
            throw new TopologyLayoutImpossibleException(
                "No valid planar layout could be found for the topology.");
        }

        return rowByNodeIndex;
    }


    private async UniTask<bool> TryPlaceNextNodeAsync(
        int[] rowByNodeIndex,
        int nextRowIndex,
        ConstraintState constraintState,
        CancellationToken cancellationToken)
    {
        await YieldIfNecessaryAsync(cancellationToken);

        /*
         * Every node has been assigned a row, so the current constraint
         * graph represents a complete candidate layout.
         */
        if (nextRowIndex == rowByNodeIndex.Length)
        {
            return true;
        }

        /*
         * Try every currently-unplaced node as the next row.
         *
         * We cannot arbitrarily choose one node here because the
         * vertical ordering is part of the problem we are solving.
         */
        for (int candidateNodeIndex = 0;
             candidateNodeIndex < rowByNodeIndex.Length;
             candidateNodeIndex++)
        {
            /*
             * A node that already has a row cannot be used again.
             */
            if (rowByNodeIndex[candidateNodeIndex] >= 0)
            {
                continue;
            }

            rowByNodeIndex[candidateNodeIndex] =
                nextRowIndex;

            int constraintCheckpoint =
                constraintState.CreateCheckpoint();

            /*
             * Placing this node may complete one or more connections.
             *
             * Those newly-completed connections may now pass through
             * nodes which were placed between their endpoints.
             */
            bool placementIsPossible =
                await ResolveConnectionsCompletedByNodeAsync(
                    candidateNodeIndex,
                    rowByNodeIndex,
                    constraintState,
                    cancellationToken);

            /*
             * If all completed connections can be routed around their
             * intervening nodes, continue recursively with the next row.
             */
            if (placementIsPossible)
            {
                bool remainingLayoutIsPossible =
                    await TryPlaceNextNodeAsync(
                        rowByNodeIndex,
                        nextRowIndex + 1,
                        constraintState,
                        cancellationToken);

                if (remainingLayoutIsPossible)
                {
                    return true;
                }
            }

            /*
             * Either this node could not be placed here, or a later
             * choice made the resulting layout impossible.
             *
             * Restore both the connection constraints and the row
             * assignment before trying another candidate.
             */
            constraintState.RollbackToCheckpoint(
                constraintCheckpoint);

            rowByNodeIndex[candidateNodeIndex] = -1;
        }

        return false;
    }


    private async UniTask<bool> ResolveConnectionsCompletedByNodeAsync(
        int newlyPlacedNodeIndex,
        int[] rowByNodeIndex,
        ConstraintState constraintState,
        CancellationToken cancellationToken)
    {
        int[] edgeSequence =
            topology.nodes[newlyPlacedNodeIndex].edgeSequence;

        /*
         * Only connections referenced by this node can have become
         * complete when this node was placed.
         */
        for (int edgeSequenceIndex = 0;
             edgeSequenceIndex < edgeSequence.Length;
             edgeSequenceIndex++)
        {
            await YieldIfNecessaryAsync(cancellationToken);

            int connectionIndex =
                edgeSequence[edgeSequenceIndex];

            Topology.Edge connection =
                topology.edges[connectionIndex];

            int firstEndpointNodeIndex =
                connection.nodeAidx;

            int secondEndpointNodeIndex =
                connection.nodeBidx;

            /*
             * We only need to examine this connection once both of its
             * endpoint nodes have received rows.
             */
            if (rowByNodeIndex[firstEndpointNodeIndex] < 0 ||
                rowByNodeIndex[secondEndpointNodeIndex] < 0)
            {
                continue;
            }

            /*
             * The connection has already been handled if its second
             * endpoint was placed earlier.
             *
             * We therefore only process it when the newly placed node
             * is one of its endpoints and the other endpoint was already
             * assigned a row.
             */
            if (firstEndpointNodeIndex != newlyPlacedNodeIndex &&
                secondEndpointNodeIndex != newlyPlacedNodeIndex)
            {
                continue;
            }

            bool connectionCanBeRouted =
                await ResolveCompletedConnectionAsync(
                    connectionIndex,
                    firstEndpointNodeIndex,
                    secondEndpointNodeIndex,
                    rowByNodeIndex,
                    constraintState,
                    cancellationToken);

            if (!connectionCanBeRouted)
            {
                return false;
            }
        }

        return true;
    }


    private async UniTask<bool> ResolveCompletedConnectionAsync(
        int connectionIndex,
        int firstEndpointNodeIndex,
        int secondEndpointNodeIndex,
        int[] rowByNodeIndex,
        ConstraintState constraintState,
        CancellationToken cancellationToken)
    {
        int firstEndpointRow =
            rowByNodeIndex[firstEndpointNodeIndex];

        int secondEndpointRow =
            rowByNodeIndex[secondEndpointNodeIndex];

        int lowerEndpointRow =
            Math.Min(firstEndpointRow, secondEndpointRow);

        int higherEndpointRow =
            Math.Max(firstEndpointRow, secondEndpointRow);

        /*
         * There is nothing to route around if the endpoints are on
         * adjacent rows.
         */
        if (higherEndpointRow - lowerEndpointRow <= 1)
        {
            return true;
        }

        /*
         * Find every node whose row lies strictly between the two
         * endpoints.
         *
         * The connection must pass completely to one side of each
         * such node.
         */
        for (int interveningNodeIndex = 0;
             interveningNodeIndex < rowByNodeIndex.Length;
             interveningNodeIndex++)
        {
            await YieldIfNecessaryAsync(cancellationToken);

            if (interveningNodeIndex == firstEndpointNodeIndex ||
                interveningNodeIndex == secondEndpointNodeIndex)
            {
                continue;
            }

            int interveningNodeRow =
                rowByNodeIndex[interveningNodeIndex];

            /*
             * This node has to lie strictly between the connection's
             * endpoint rows before the connection can intersect it.
             */
            if (interveningNodeRow <= lowerEndpointRow ||
                interveningNodeRow >= higherEndpointRow)
            {
                continue;
            }

            int[] interveningNodeConnections =
                topology.nodes[interveningNodeIndex].edgeSequence;

            /*
             * A node is represented by the horizontal segment spanning
             * from its first connection to its last connection.
             *
             * Therefore the connection can avoid the node in exactly
             * two ways:
             *
             *     connection < first node connection
             *
             * or:
             *
             *     last node connection < connection
             */
            if (interveningNodeConnections.Length == 0)
            {
                continue;
            }

            int firstNodeConnectionIndex =
                interveningNodeConnections[0];

            int lastNodeConnectionIndex =
                interveningNodeConnections[
                    interveningNodeConnections.Length - 1];

            int constraintCheckpoint =
                constraintState.CreateCheckpoint();

            /*
             * First try routing the connection to the left of the node.
             */
            if (constraintState.TryAddConstraint(
                    connectionIndex,
                    firstNodeConnectionIndex))
            {
                bool remainingConnectionsArePossible =
                    await ResolveRemainingInterveningNodesAsync(
                        connectionIndex,
                        firstEndpointNodeIndex,
                        secondEndpointNodeIndex,
                        interveningNodeIndex,
                        rowByNodeIndex,
                        constraintState,
                        cancellationToken);

                if (remainingConnectionsArePossible)
                {
                    return true;
                }
            }

            /*
             * The left-hand choice either produced an immediate
             * contradiction or caused a later contradiction.
             *
             * Restore the state before trying the other side.
             */
            constraintState.RollbackToCheckpoint(
                constraintCheckpoint);

            /*
             * Now try routing the connection to the right of the node.
             */
            if (constraintState.TryAddConstraint(
                    lastNodeConnectionIndex,
                    connectionIndex))
            {
                bool remainingConnectionsArePossible =
                    await ResolveRemainingInterveningNodesAsync(
                        connectionIndex,
                        firstEndpointNodeIndex,
                        secondEndpointNodeIndex,
                        interveningNodeIndex,
                        rowByNodeIndex,
                        constraintState,
                        cancellationToken);

                if (remainingConnectionsArePossible)
                {
                    return true;
                }
            }

            /*
             * Neither side works for this node.
             *
             * The caller owns the checkpoint for this particular
             * left/right decision and will restore it before trying
             * another layout branch.
             */
            constraintState.RollbackToCheckpoint(
                constraintCheckpoint);

            return false;
        }

        return true;
    }


    private async UniTask<bool> ResolveRemainingInterveningNodesAsync(
        int connectionIndex,
        int firstEndpointNodeIndex,
        int secondEndpointNodeIndex,
        int previouslyResolvedNodeIndex,
        int[] rowByNodeIndex,
        ConstraintState constraintState,
        CancellationToken cancellationToken)
    {
        int firstEndpointRow =
            rowByNodeIndex[firstEndpointNodeIndex];

        int secondEndpointRow =
            rowByNodeIndex[secondEndpointNodeIndex];

        int lowerEndpointRow =
            Math.Min(firstEndpointRow, secondEndpointRow);

        int higherEndpointRow =
            Math.Max(firstEndpointRow, secondEndpointRow);

        /*
         * Continue from the node immediately after the node whose
         * left/right choice was just made.
         */
        int previousNodeRow =
            rowByNodeIndex[previouslyResolvedNodeIndex];

        /*
         * Find the next intervening node in row order.
         *
         * We deliberately don't rely on node indexes being related to
         * their eventual row indexes.
         */
        int nextInterveningNodeIndex = -1;
        int nextInterveningNodeRow = int.MaxValue;

        for (int candidateNodeIndex = 0;
             candidateNodeIndex < rowByNodeIndex.Length;
             candidateNodeIndex++)
        {
            await YieldIfNecessaryAsync(cancellationToken);

            if (candidateNodeIndex == firstEndpointNodeIndex ||
                candidateNodeIndex == secondEndpointNodeIndex)
            {
                continue;
            }

            int candidateNodeRow =
                rowByNodeIndex[candidateNodeIndex];

            /*
             * We only want a node strictly between the endpoints and
             * strictly after the node whose choice we just resolved.
             */
            if (candidateNodeRow <= previousNodeRow ||
                candidateNodeRow <= lowerEndpointRow ||
                candidateNodeRow >= higherEndpointRow)
            {
                continue;
            }

            /*
             * Select the nearest remaining node in row order.
             */
            if (candidateNodeRow < nextInterveningNodeRow)
            {
                nextInterveningNodeRow = candidateNodeRow;
                nextInterveningNodeIndex = candidateNodeIndex;
            }
        }

        /*
         * No intervening nodes remain, so this connection has been
         * successfully constrained around every node it crosses.
         */
        if (nextInterveningNodeIndex < 0)
        {
            return true;
        }

        int[] interveningNodeConnections =
            topology.nodes[nextInterveningNodeIndex].edgeSequence;

        if (interveningNodeConnections.Length == 0)
        {
            return await ResolveRemainingInterveningNodesAsync(
                connectionIndex,
                firstEndpointNodeIndex,
                secondEndpointNodeIndex,
                nextInterveningNodeIndex,
                rowByNodeIndex,
                constraintState,
                cancellationToken);
        }

        int firstNodeConnectionIndex =
            interveningNodeConnections[0];

        int lastNodeConnectionIndex =
            interveningNodeConnections[
                interveningNodeConnections.Length - 1];

        int constraintCheckpoint =
            constraintState.CreateCheckpoint();

        /*
         * First try putting the connection to the left of this node.
         */
        if (constraintState.TryAddConstraint(
                connectionIndex,
                firstNodeConnectionIndex))
        {
            bool remainingConnectionsArePossible =
                await ResolveRemainingInterveningNodesAsync(
                    connectionIndex,
                    firstEndpointNodeIndex,
                    secondEndpointNodeIndex,
                    nextInterveningNodeIndex,
                    rowByNodeIndex,
                    constraintState,
                    cancellationToken);

            if (remainingConnectionsArePossible)
            {
                return true;
            }
        }

        constraintState.RollbackToCheckpoint(
            constraintCheckpoint);

        /*
         * Then try putting the connection to the right of this node.
         */
        if (constraintState.TryAddConstraint(
                lastNodeConnectionIndex,
                connectionIndex))
        {
            bool remainingConnectionsArePossible =
                await ResolveRemainingInterveningNodesAsync(
                    connectionIndex,
                    firstEndpointNodeIndex,
                    secondEndpointNodeIndex,
                    nextInterveningNodeIndex,
                    rowByNodeIndex,
                    constraintState,
                    cancellationToken);

            if (remainingConnectionsArePossible)
            {
                return true;
            }
        }

        constraintState.RollbackToCheckpoint(
            constraintCheckpoint);

        return false;
    }


    private int[] CreateConnectionColumns(
        ConstraintState constraintState)
    {
        int connectionCount =
            topology.edges.Length;

        int[] columnByConnectionIndex =
            new int[connectionCount];

        int[] numberOfPrecedingConnectionsByConnectionIndex =
            new int[connectionCount];

        Queue<int> connectionsWithNoPrecedingConnections =
            new Queue<int>();

        /*
         * Count how many constraints point into each connection.
         *
         * A connection with a count of zero can safely receive the
         * next available column.
         */
        for (int precedingConnectionIndex = 0;
             precedingConnectionIndex < connectionCount;
             precedingConnectionIndex++)
        {
            List<int> followingConnectionIndexes =
                constraintState
                    .FollowingConnectionIndexesByPrecedingConnectionIndex[
                        precedingConnectionIndex];

            for (int followingConnectionListIndex = 0;
                 followingConnectionListIndex <
                 followingConnectionIndexes.Count;
                 followingConnectionListIndex++)
            {
                int followingConnectionIndex =
                    followingConnectionIndexes[
                        followingConnectionListIndex];

                numberOfPrecedingConnectionsByConnectionIndex[
                    followingConnectionIndex]++;
            }
        }

        for (int connectionIndex = 0;
             connectionIndex < connectionCount;
             connectionIndex++)
        {
            if (numberOfPrecedingConnectionsByConnectionIndex[
                    connectionIndex] == 0)
            {
                connectionsWithNoPrecedingConnections.Enqueue(
                    connectionIndex);
            }
        }

        int nextColumnIndex = 0;
        int numberOfSortedConnections = 0;

        /*
         * Standard iterative topological sort.
         *
         * The constraint graph has already been checked for cycles,
         * but checking the final count here protects against a future
         * bug in constraint construction.
         */
        while (connectionsWithNoPrecedingConnections.Count > 0)
        {
            int connectionIndex =
                connectionsWithNoPrecedingConnections.Dequeue();

            columnByConnectionIndex[connectionIndex] =
                nextColumnIndex++;

            numberOfSortedConnections++;

            List<int> followingConnectionIndexes =
                constraintState
                    .FollowingConnectionIndexesByPrecedingConnectionIndex[
                        connectionIndex];

            for (int followingConnectionListIndex = 0;
                 followingConnectionListIndex <
                 followingConnectionIndexes.Count;
                 followingConnectionListIndex++)
            {
                int followingConnectionIndex =
                    followingConnectionIndexes[
                        followingConnectionListIndex];

                numberOfPrecedingConnectionsByConnectionIndex[
                    followingConnectionIndex]--;

                /*
                 * This connection becomes eligible for the next
                 * column once all of its preceding constraints have
                 * been satisfied.
                 */
                if (numberOfPrecedingConnectionsByConnectionIndex[
                        followingConnectionIndex] == 0)
                {
                    connectionsWithNoPrecedingConnections.Enqueue(
                        followingConnectionIndex);
                }
            }
        }

        /*
         * A complete topological ordering must contain every connection.
         */
        if (numberOfSortedConnections != connectionCount)
        {
            throw new TopologyLayoutImpossibleException(
                "The final connection constraint graph contains a cycle.");
        }

        return columnByConnectionIndex;
    }

    int opsSinceLastLog = 0;
    int OperationsBetweenLogs = 5000;
    private async UniTask YieldIfNecessaryAsync(
        CancellationToken cancellationToken)
    {
        operationsSinceLastYield++;
        totalOps++;
        opsSinceLastLog++;
        /*
         * Most individual operations are extremely cheap.
         *
         * Yielding after every operation would therefore add
         * unnecessary overhead. Instead, periodically return control
         * to Unity so that a large search does not monopolize a frame.
         */
        if (operationsSinceLastYield < OperationsBetweenYields)
        {
            if (opsSinceLastLog > OperationsBetweenLogs)
            {
                Debug.Log("Total Ops: " + totalOps);
                opsSinceLastLog = 0;
            }
            return;
        }

        operationsSinceLastYield = 0;

        cancellationToken.ThrowIfCancellationRequested();

        await UniTask.Yield();
    }


    private class ConstraintState
    {
        public readonly List<int>[]
            FollowingConnectionIndexesByPrecedingConnectionIndex;

        private readonly List<ConnectionConstraint>
            addedConstraints;


        public ConstraintState(int connectionCount)
        {
            FollowingConnectionIndexesByPrecedingConnectionIndex =
                new List<int>[connectionCount];

            for (int connectionIndex = 0;
                 connectionIndex < connectionCount;
                 connectionIndex++)
            {
                FollowingConnectionIndexesByPrecedingConnectionIndex[
                    connectionIndex] =
                    new List<int>();
            }

            addedConstraints =
                new List<ConnectionConstraint>();
        }


        public int CreateCheckpoint()
        {
            return addedConstraints.Count;
        }


        public void RollbackToCheckpoint(int checkpoint)
        {
            /*
             * Every constraint added after the checkpoint belongs to
             * the branch we are abandoning.
             */
            while (addedConstraints.Count > checkpoint)
            {
                ConnectionConstraint constraintToRemove =
                    addedConstraints[
                        addedConstraints.Count - 1];

                addedConstraints.RemoveAt(
                    addedConstraints.Count - 1);

                FollowingConnectionIndexesByPrecedingConnectionIndex[
                    constraintToRemove.PrecedingConnectionIndex]
                    .Remove(
                        constraintToRemove.FollowingConnectionIndex);
            }
        }


        public bool TryAddConstraint(
            int precedingConnectionIndex,
            int followingConnectionIndex)
        {
            /*
             * A connection cannot precede itself.
             */
            if (precedingConnectionIndex == followingConnectionIndex)
            {
                return false;
            }

            List<int> followingConnectionIndexes =
                FollowingConnectionIndexesByPrecedingConnectionIndex[
                    precedingConnectionIndex];

            /*
             * Adding a constraint which already exists does not change
             * the state and therefore cannot introduce a contradiction.
             */
            if (followingConnectionIndexes.Contains(
                    followingConnectionIndex))
            {
                return true;
            }

            /*
             * If the proposed following connection can already reach
             * the proposed preceding connection, adding this relationship
             * would create a cycle.
             */
            if (CanReach(
                    followingConnectionIndex,
                    precedingConnectionIndex))
            {
                return false;
            }

            followingConnectionIndexes.Add(
                followingConnectionIndex);

            addedConstraints.Add(
                new ConnectionConstraint(
                    precedingConnectionIndex,
                    followingConnectionIndex));

            return true;
        }


        private bool CanReach(
            int startingConnectionIndex,
            int targetConnectionIndex)
        {
            Stack<int> connectionsToVisit =
                new Stack<int>();

            bool[] alreadyVisitedConnectionIndexes =
                new bool[
                    FollowingConnectionIndexesByPrecedingConnectionIndex.Length];

            connectionsToVisit.Push(
                startingConnectionIndex);

            while (connectionsToVisit.Count > 0)
            {
                int currentConnectionIndex =
                    connectionsToVisit.Pop();

                /*
                 * Reaching the target means that the proposed new
                 * constraint would close a cycle.
                 */
                if (currentConnectionIndex == targetConnectionIndex)
                {
                    return true;
                }

                /*
                 * Once a connection has been explored, visiting it again
                 * cannot reveal a new path.
                 */
                if (alreadyVisitedConnectionIndexes[
                        currentConnectionIndex])
                {
                    continue;
                }

                alreadyVisitedConnectionIndexes[
                    currentConnectionIndex] = true;

                List<int> followingConnectionIndexes =
                    FollowingConnectionIndexesByPrecedingConnectionIndex[
                        currentConnectionIndex];

                for (int followingConnectionListIndex = 0;
                     followingConnectionListIndex <
                     followingConnectionIndexes.Count;
                     followingConnectionListIndex++)
                {
                    connectionsToVisit.Push(
                        followingConnectionIndexes[
                            followingConnectionListIndex]);
                }
            }

            return false;
        }
    }


    private struct ConnectionConstraint
    {
        public readonly int PrecedingConnectionIndex;
        public readonly int FollowingConnectionIndex;


        public ConnectionConstraint(
            int precedingConnectionIndex,
            int followingConnectionIndex)
        {
            PrecedingConnectionIndex =
                precedingConnectionIndex;

            FollowingConnectionIndex =
                followingConnectionIndex;
        }
    }
}