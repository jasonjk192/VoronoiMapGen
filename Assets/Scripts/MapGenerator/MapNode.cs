﻿using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

public partial class MapGraph
{
    /// <summary>
    /// Represents a half-edge face
    /// </summary>
    public class MapNode
    {
        private float? _heightDifference;
        private Rect? _boundingRectangle;

        public Vector3 centerPoint { get; set; }
        /// <summary>
        /// An arbitrary half-edge that borders on this map node (face)
        /// </summary>
        public MapNodeHalfEdge startEdge { get; set; }

        public MapNodeType nodeType { get; set; }

        public List<MapNodeHalfEdge> GetEdgesAsList()
        {
            var list = new List<MapNodeHalfEdge>() { startEdge };
            var next = startEdge.next;
            while (next != startEdge)
            {
                list.Add(next);
                next = next.next;
            }
            return list;
        }

        public int GetEdgesCount()
        {
            if(startEdge == null) return 0;
            int count = 1;
            var next = startEdge.next;
            while(next != startEdge)
            {
                count++;
                next = next.next;
            }
            return count;
        }

        public IEnumerable<MapNodeHalfEdge> GetEdges()
        {
            yield return startEdge;

            var next = startEdge.next;
            while(next != startEdge)
            {
                yield return next;
                next = next.next;
            }
        }

        public List<MapPoint> GetCornersAsList()
        {
            var list = new List<MapPoint>() { startEdge.destination };
            var next = startEdge.next;
            while (next != startEdge)
            {
                list.Add(next.destination);
                next = next.next;
            }
            return list;
        }

        public IEnumerable<MapPoint> GetCorners()
        {
            yield return startEdge.destination;

            var next = startEdge.next;
            while (next != startEdge)
            {
                yield return next.destination;
                next = next.next;
            }
        }

        public bool IsEdge()
        {
            foreach (var edge in GetEdges())
            {
                if (edge.opposite == null) return true;
            }
            return false;
        }

        public float GetElevation()
        {
            return centerPoint.y;
        }

        public float GetHeightDifference()
        {
            if (!_heightDifference.HasValue)
            {
                var lowestY = centerPoint.y;
                var highestY = centerPoint.y;
                foreach(var corner in GetCorners())
                {
                    if (corner.position.y > highestY) highestY = corner.position.y;
                    if (corner.position.y < lowestY) lowestY = corner.position.y;
                }
                _heightDifference = highestY - lowestY;
            }
            return _heightDifference.Value;
        }

        internal MapPoint GetLowestCorner()
        {
            float lowestY = startEdge.destination.position.y;
            MapPoint lowestCorner = startEdge.destination;
            var next = startEdge.next;
            while (next != startEdge)
            {
                float currentY = next.destination.position.y;
                if (currentY < lowestY)
                {
                    lowestY = currentY;
                    lowestCorner = next.destination;
                }
                next = next.next;
            }
            return lowestCorner;
        }

        public MapNodeHalfEdge GetLowestEdge()
        {
            MapNodeHalfEdge lowestEdge = null;
            foreach(var edge in GetEdges())
            {

                if (lowestEdge == null || lowestEdge.destination.position.y > edge.destination.position.y || lowestEdge.previous.destination.position.y > edge.previous.destination.position.y)
                {
                    lowestEdge = edge;
                }
            }
            return lowestEdge;
        }

        public List<MapNode> GetNeighborNodes()
        {
            if (startEdge.opposite == null || startEdge.opposite.node == null) return null;
            List<MapNode> nodes = new() { startEdge.opposite.node };

            var next = startEdge.next;
            while (next != startEdge)
            {
                if (next.opposite != null && next.opposite.node != null)
                    nodes.Add(next.opposite.node);
                next = next.next;
            }
            return nodes;
            //return GetEdges().Where(x => x.opposite != null && x.opposite.node != null).Select(x => x.opposite.node).ToList();
        }

        /// <summary>
        /// Returns a 2d bounding rectangle for the site, flattening the y plane
        /// </summary>
        /// <returns></returns>
        public Rect GetBoundingRectangle()
        {
            if (!_boundingRectangle.HasValue)
            {

                var minX = float.MaxValue;
                var maxX = float.MinValue;
                var minY = float.MaxValue;
                var maxY = float.MinValue;

                foreach (var corner in GetCorners())
                {
                    if (corner.position.x < minX) minX = corner.position.x;
                    if (corner.position.x > maxX) maxX = corner.position.x;
                    if (corner.position.z < minY) minY = corner.position.z;
                    if (corner.position.z > maxY) maxY = corner.position.z;
                }

                _boundingRectangle = new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            return _boundingRectangle.Value;
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}", base.ToString(), centerPoint);
        }
    }
}