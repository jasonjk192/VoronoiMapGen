using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public partial class MapGraph
{
    /// <summary>
    /// Represents a half-edge vertex
    /// </summary>
    public class MapPoint
    {
        public Vector3 position { get; set; }
        /// <summary>
        /// The half-edge that starts at this point
        /// </summary>
        public MapNodeHalfEdge leavingEdge { get; set; }

        public MapNodeHalfEdge GetDownSlopeEdge()
        {
            var edges = GetEdges();

            MapNodeHalfEdge bestEdge = null;

            foreach (var edge in edges)
            {
                if (edge.destination.position.y <= this.position.y)
                {
                    if (bestEdge == null || edge.destination.position.y < bestEdge.destination.position.y)
                    {
                        bestEdge = edge;
                    }
                }
            }
            return bestEdge;

            /*var current = this;
            return edges.Where(x => x.destination.position.y <= current.position.y).OrderBy(x => x.destination.position.y).FirstOrDefault();*/
        }

        public List<MapNodeHalfEdge> GetEdgesAsList(int maxIterations = 20)
        {
            List<MapNodeHalfEdge> list = new();
            var firstEdge = leavingEdge;
            var nextEdge = firstEdge;
            var iterations = 0;

            do
            {
                list.Add(nextEdge);
                nextEdge = nextEdge.opposite?.next;
                iterations++;
            }
            while (nextEdge != firstEdge && nextEdge != null && iterations < maxIterations);
            return list;
        }

        public IEnumerable<MapNodeHalfEdge> GetEdges()
        {
            var firstEdge = leavingEdge;
            var nextEdge = firstEdge;

            var maxIterations = 20;
            var iterations = 0;

            do
            {
                yield return nextEdge;
                nextEdge = nextEdge.opposite == null ? null : nextEdge.opposite.next;
                iterations++;
            }
            while (nextEdge != firstEdge && nextEdge != null && iterations < maxIterations);
        }

        public List<MapNode> GetNodes()
        {
            return GetEdges().Select(x => x.node).ToList();
        }

        public MapNode GetLowestNode()
        {
            MapNode lowestNode = null;
            foreach (var node in GetNodes())
            {
                if (lowestNode == null || node.centerPoint.y < lowestNode.centerPoint.y) lowestNode = node;
            }
            return lowestNode;
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}", base.ToString(), position);
        }
    }
}