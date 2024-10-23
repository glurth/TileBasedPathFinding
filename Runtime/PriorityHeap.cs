using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace Priority_Queue
{
    /// <summary>
    /// The IPriorityHeap interface.  This is mainly here for purists, and in case I decide to add more implementations later.
    /// For speed purposes, it is actually recommended that you *don't* access the priority queue through this interface, since the JIT can
    /// (theoretically?) optimize method calls from concrete-types slightly better.
    /// </summary>
    public interface IPriorityHeap<T> : IEnumerable<T>
        where T : IPriorityHeapNode
    {
        void Remove(T node);
        //void UpdatePriority(T node, double priority);
        //void Enqueue(T node, double priority);
        void UpdatePriority(T node);
        void Enqueue(T node);
        T Dequeue();
        T First { get; }
        int Count { get; }
     //   int MaxSize { get; }
        void Clear();
        bool Contains(T node);
    }

    public interface IPriorityHeapNode
    {
        /// <summary>
        /// The Priority to insert this node at.  Must be set BEFORE adding a node to the queue
        /// </summary>
        public double Priority
        {
            get;
         //   set;
        }

        /// <summary>
        /// <b>Used by the priority queue - do not edit this value.</b>
        /// Represents the order the node was inserted in
        /// </summary>
        public long InsertionIndex { get; set; }

        /// <summary>
        /// <b>Used by the priority queue - do not edit this value.</b>
        /// Represents the current position in the queue
        /// </summary>
        public int QueueIndex { get; set; }
    }

    /// <summary>
    /// Optional use class.  You can choose to derive your class from this to avoid the need to define all the IPriorityHeap interface functions yourself.
    /// </summary>
    [System.Serializable]
    public class PriorityHeapNodeBase: IPriorityHeapNode
    {

        /// <summary>
        /// The Priority to insert this node at.  Must be set BEFORE adding a node to the queue
        /// </summary>
        public virtual double Priority
        {
            get;
            set;
        }

        /// <summary>
        /// <b>Used by the priority queue - do not edit this value.</b>
        /// Represents the order the node was inserted in
        /// </summary>
        public long InsertionIndex { get; set; }

        /// <summary>
        /// <b>Used by the priority queue - do not edit this value.</b>
        /// Represents the current position in the queue
        /// </summary>
        public int QueueIndex { get; set; }
    }
    public class PriorityHeapNodeWithReferenceObject<T> : IPriorityHeapNode
    {

        public T referenceObject;

        /// <summary>
        /// The Priority to insert this node at.  Must be set BEFORE adding a node to the queue
        /// </summary>
        public double Priority
        {
            get;
            set;
        }

        /// <summary>
        /// <b>Used by the priority queue - do not edit this value.</b>
        /// Represents the order the node was inserted in
        /// </summary>
        public long InsertionIndex { get; set; }

        /// <summary>
        /// <b>Used by the priority queue - do not edit this value.</b>
        /// Represents the current position in the queue
        /// </summary>
        public int QueueIndex { get; set; }
    }
    /// <summary>
    /// An implementation of a min-Priority Queue using a heap.  Has O(1) .Contains()!
    /// Use this class to store a set of items that have a "priority" value.  Use the Dequeue function to get the item in the set with the lowest priority value, and remove it from the set.  You may add new items to the set, at anytime, and in any order, with Enqueue function.
    /// See https://bitbucket.org/BlueRaja/high-speed-priority-queue-for-c/wiki/Getting%20Started for more information
    /// </summary>
    /// <typeparam name="T">The values in the queue.  Must implement the PriorityHeapNode interface</typeparam>
    public class PriorityHeap<T> : IPriorityHeap<T>
        where T : class, IPriorityHeapNode
    {
        protected int _numNodes;
        protected T[] _actual_nodes;
        public virtual T[] _nodes
        {
            get
            {
                return _actual_nodes;
            }
            set
            {
                _actual_nodes = value;
            }
        }

        protected long _numNodesEverEnqueued;

        /// <summary>
        /// Instantiate a new Priority Queue
        /// </summary>
        /// <param name="maxNodes">The max nodes ever allowed to be enqueued (going over this will cause an exception)</param>
        public PriorityHeap(int arrayIncrementAmount)
        {
            this.arrayIncrementAmount = arrayIncrementAmount;
            _numNodes = 0;
            _actual_nodes = new T[arrayIncrementAmount];//maxNodes + 1];
            //_nodes = new T[maxNodes + 1];
            _numNodesEverEnqueued = 0;
        }

        /// <summary>
        /// Returns the number of nodes in the queue.  O(1)
        /// </summary>
        public int Count
        {
            get
            {
                return _numNodes;
            }
        }

        /// <summary>
        /// Removes every node from the queue.  O(n) (So, don't do this often!)
        /// </summary>
#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void Clear()
        {
            for (int i = 1; i < _nodes.Length; i++)
                _nodes[i] = null;
            _numNodes = 0;
        }

        /// <summary>
        /// Returns (in O(1)!) whether the given node is in the queue.  O(1)
        /// </summary>
#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public bool Contains(T node)
        {
            return (_nodes[node.QueueIndex] == node);
        }

#nullable enable
        public T? Find(System.Predicate<T> predicate)
        {
            foreach (T node in _nodes)
            {
                if(node!=null)
                    if (predicate(node)) return node;
            }
            return null;
        }
#nullable disable
        int arrayIncrementAmount = 1000; 
        /// <summary>
        /// Enqueue a node - .Priority must be set beforehand!  O(log n)
        /// </summary>
#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void Enqueue(T node)
        {
            
            _numNodes++;
            if (_numNodes >= _actual_nodes.Length)
            {
                T[] newArray = new T[_numNodes + arrayIncrementAmount];
                _actual_nodes.CopyTo(newArray,0);
                _actual_nodes = newArray;
            }
            try { _nodes[_numNodes] = node; }
            catch
            {
                Debug.Log("lookup failed");
            }
            node.QueueIndex = _numNodes;
            node.InsertionIndex = _numNodesEverEnqueued++;
            CascadeUp(_nodes[_numNodes]);
        }

#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void Swap(T node1, T node2)
        {
            //Swap the nodes
            _nodes[node1.QueueIndex] = node2;
            _nodes[node2.QueueIndex] = node1;

            //Swap their indicies
            int temp = node1.QueueIndex;
            node1.QueueIndex = node2.QueueIndex;
            node2.QueueIndex = temp;
        }

        //Performance appears to be slightly better when this is NOT inlined o_O
        private void CascadeUp(T node)
        {
            //aka Heapify-up
            int parent = node.QueueIndex / 2;
            while (parent >= 1)
            {
                T parentNode = _nodes[parent];
                if (HasHigherPriority(parentNode, node))
                    break;

                //Node has lower priority value, so move it up the heap
                Swap(node, parentNode); //For some reason, this is faster with Swap() rather than (less..?) individual operations, like in CascadeDown()

                parent = node.QueueIndex / 2;
            }
        }



#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void CascadeDown(T node)
        {
            //aka Heapify-down
            T newParent;
            int finalQueueIndex = node.QueueIndex;
            while (true)
            {
                newParent = node;
                int childLeftIndex = 2 * finalQueueIndex;

                //Check if the left-child is higher-priority than the current node
                if (childLeftIndex > _numNodes)
                {
                    //This could be placed outside the loop, but then we'd have to check newParent != node twice
                    node.QueueIndex = finalQueueIndex;
                    _nodes[finalQueueIndex] = node;
                    break;
                }

                T childLeft = _nodes[childLeftIndex];
                if (HasHigherPriority(childLeft, newParent))
                {
                    newParent = childLeft;
                }

                //Check if the right-child is higher-priority than either the current node or the left child
                int childRightIndex = childLeftIndex + 1;
                if (childRightIndex <= _numNodes)
                {
                    T childRight = _nodes[childRightIndex];
                    if (HasHigherPriority(childRight, newParent))
                    {
                        newParent = childRight;
                    }
                }

                //If either of the children has higher (smaller) priority, swap and continue cascading
                if (newParent != node)
                {
                    //Move new parent to its new index.  node will be moved once, at the end
                    //Doing it this way is one less assignment operation than calling Swap()
                    _nodes[finalQueueIndex] = newParent;

                    int temp = newParent.QueueIndex;
                    newParent.QueueIndex = finalQueueIndex;
                    finalQueueIndex = temp;
                }
                else
                {
                    //See note above
                    node.QueueIndex = finalQueueIndex;
                    _nodes[finalQueueIndex] = node;
                    break;
                }
            }
        }

        /// <summary>
        /// Returns true if 'higher' has higher priority than 'lower', false otherwise.
        /// Note that calling HasHigherPriority(node, node) (ie. both arguments the same node) will return false
        /// </summary>
#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private bool HasHigherPriority(T higher, T lower)
        {
            if (higher.Priority < lower.Priority)
                return true;
            return (higher.Priority == lower.Priority && higher.InsertionIndex < lower.InsertionIndex);
        }

        /// <summary>
        /// Removes the head of the queue (node with highest priority; ties are broken by order of insertion), and returns it.  O(log n)
        /// </summary>
        public T Dequeue()
        {
            T returnMe = _nodes[1];
            Remove(returnMe);
            return returnMe;
        }

        /// <summary>
        /// Returns the head of the queue, without removing it (use Dequeue() for that).  O(1)
        /// </summary>
        public T First
        {
            get
            {
                return _nodes[1];
            }
        }

        public void FirstUpdatedResort()
        {
            OnNodeUpdated(_nodes[1]);
        }

        public T ReturnSecondItem()
        {
            T node = _nodes[1];
            T newParent;
            int finalQueueIndex = node.QueueIndex;

            newParent = node;
            int childLeftIndex = 2 * finalQueueIndex;

            //Check if the left-child is higher-priority than the current node
            if (childLeftIndex <= _numNodes)
            {
                T childLeft = _nodes[childLeftIndex];
                if (HasHigherPriority(childLeft, newParent))
                {
                    newParent = childLeft;
                }
            }
            //Check if the right-child is higher-priority than either the current node or the left child
            int childRightIndex = childLeftIndex + 1;
            if (childRightIndex <= _numNodes)
            {
                T childRight = _nodes[childRightIndex];
                if (HasHigherPriority(childRight, newParent))
                {
                    newParent = childRight;
                }
            }

            //If either of the children has higher (smaller) priority, swap and continue cascading
            if (newParent != node)
            {
            }
            return newParent;
        }
        /// <summary>
        /// This method must be called on a node every time its priority changes while it is in the queue.  
        /// <b>Forgetting to call this method will result in a corrupted queue!</b>
        /// O(log n)
        /// </summary>
#if NET_VERSION_4_5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void UpdatePriority(T node)//, double priority)
        {
          //  node.Priority = priority;
            OnNodeUpdated(node);
        }

        private void OnNodeUpdated(T node)
        {
            //Bubble the updated node up or down as appropriate
            int parentIndex = node.QueueIndex / 2;
            T parentNode = _nodes[parentIndex];

            if (parentIndex > 0 && HasHigherPriority(node, parentNode))
            {
                CascadeUp(node);
            }
            else
            {
                //Note that CascadeDown will be called if parentNode == node (that is, node is the root)
                CascadeDown(node);
            }
        }

        /// <summary>
        /// Removes a node from the queue.  Note that the node does not need to be the head of the queue.  O(log n)
        /// </summary>
        public void Remove(T node)
        {
            if (_numNodes <= 1)
            {
                _nodes[1] = null;
                _numNodes = 0;
                return;
            }

            //Make sure the node is the last node in the queue
            bool wasSwapped = false;
            T formerLastNode = _nodes[_numNodes];
            if (node.QueueIndex != _numNodes)
            {
                //Swap the node with the last node
                Swap(node, formerLastNode);
                wasSwapped = true;
            }

            _numNodes--;
            _nodes[node.QueueIndex] = null;

            if (wasSwapped)
            {
                //Now bubble formerLastNode (which is no longer the last node) up or down as appropriate
                OnNodeUpdated(formerLastNode);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 1; i <= _numNodes; i++)
                yield return _nodes[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// <b>Should not be called in production code.</b>
        /// Checks to make sure the queue is still in a valid state.  Used for testing/debugging the queue.
        /// </summary>
        public bool IsValidQueue()
        {
            for (int i = 1; i < _nodes.Length; i++)
            {
                if (_nodes[i] != null)
                {
                    int childLeftIndex = 2 * i;
                    if (childLeftIndex < _nodes.Length && _nodes[childLeftIndex] != null && HasHigherPriority(_nodes[childLeftIndex], _nodes[i]))
                        return false;

                    int childRightIndex = childLeftIndex + 1;
                    if (childRightIndex < _nodes.Length && _nodes[childRightIndex] != null && HasHigherPriority(_nodes[childRightIndex], _nodes[i]))
                        return false;
                }
            }
            return true;
        }
    }
}