﻿using Delaunay;
using Delaunay.Geo;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class MapGraph
{
    public enum MapNodeType
    {
        FreshWater,
        SaltWater,
        Grass,
        Mountain,
        City,
        Beach,
        Error,
        Snow
    }

    public Rect plotBounds;

    /// <summary>
    /// Map points indexed by point location
    /// </summary>
    public Dictionary<Vector3, MapPoint> points;

    /// <summary>
    /// Map nodes indexed by voronoi center point
    /// </summary>
    public Dictionary<Vector3, MapNode> nodesByCenterPosition;

    /// <summary>
    /// Map half edges
    /// </summary>
    public List<MapNodeHalfEdge> edges;

    public MapGraph(Voronoi voronoi, in HeightMap heightMap, float snapDistance)
    {     
        var time = DateTime.Now;
        CreateFromVoronoi(voronoi);
        Debug.Log(string.Format("\tCreateFromVoronoi: {0:n0}ms", DateTime.Now.Subtract(time).TotalMilliseconds));


        if (snapDistance > 0)
        {
            time = DateTime.Now;
            SnapPoints(snapDistance);
            Debug.Log(string.Format("\tSnapPoints: {0:n0}ms", DateTime.Now.Subtract(time).TotalMilliseconds));
        }

        time = DateTime.Now;
        UpdateHeights(heightMap);
        Debug.Log(string.Format("\tUpdateHeights: {0:n0}ms", DateTime.Now.Subtract(time).TotalMilliseconds));
    }

    private void SnapPoints(float snapDistance)
    {
        var keys = points.Keys.ToArray(); 
        float snapDistanceSqr = snapDistance * snapDistance;
        HashSet<MapPoint> visitedPoints = new();

        // TODO: Figure out a parallelization technique here. Currently this is the best solution with the hashset to prevent points from being processed again and again
        foreach (var key in keys)
        {
            if (!points.TryGetValue(key, out var point))
                continue;
            if(visitedPoints.Contains(point)) 
                continue;

            var neighbors = point.GetEdges();
            if (neighbors.Any(x => x.opposite == null)) continue;

            foreach (var neighbor in neighbors)
            {
                if(visitedPoints.Contains(neighbor.destination)) continue;
                visitedPoints.Add(neighbor.destination);

                // Don't snap if the neighboring nodes already have three edges
                bool canSnap = neighbor.opposite != null && snapDistanceSqr > (point.position - neighbor.destination.position).sqrMagnitude
                && neighbor.node.GetEdgesCount() > 3 && neighbor.opposite.node.GetEdgesCount() > 3;

                if (canSnap)
                {
                    visitedPoints.Add(point);
                    SnapPoints(point, neighbor);
                }
            }
        }
    }

    private void SnapPoints(MapPoint point, MapNodeHalfEdge edge)
    {
        var edgeDestinationEdges = edge.destination.GetEdgesAsList();
        // There are issues with this when snapping near the edge of the map
        if (edgeDestinationEdges.Any(x => x.opposite == null)) return;

        // Delete the edges
        edges.Remove(edge);

        // Delete the other point
        points.Remove(new Vector3(edge.destination.position.x, 0, edge.destination.position.z));

        //var otherEdges = edge.destination.GetEdgesAsList();

        // Update everything to point to the first point
        if (point.leavingEdge == edge) point.leavingEdge = edge.opposite.next;
        if (edge.node.startEdge == edge) edge.node.startEdge = edge.previous;
        edge.next.previous = edge.previous;
        edge.previous.next = edge.next;

        // Update the opposite edge as well
        // Delete edge
        edges.Remove(edge.opposite);

        // Update pointers
        edge.opposite.next.previous = edge.opposite.previous;
        edge.opposite.previous.next = edge.opposite.next;
        if (edge.opposite.node.startEdge == edge.opposite) edge.opposite.node.startEdge = edge.opposite.previous;

        foreach (var otherEdge in edgeDestinationEdges)
        {
            if (otherEdge.opposite != null) otherEdge.opposite.destination = point;
        }
    }

    private void CreateFromVoronoi(Voronoi voronoi)
    {
        var time = DateTime.Now;

        points = new Dictionary<Vector3, MapPoint>();
        nodesByCenterPosition = new Dictionary<Vector3, MapNode>();
        var edgesByStartPosition = new Dictionary<Vector3, List<MapNodeHalfEdge>>();
        edges = new List<MapNodeHalfEdge>();

        plotBounds = voronoi.plotBounds;
        var bottomLeftSite = voronoi.NearestSitePoint(plotBounds.xMin, plotBounds.yMin);
        var bottomRightSite = voronoi.NearestSitePoint(plotBounds.xMax, plotBounds.yMin);
        var topLeftSite = voronoi.NearestSitePoint(plotBounds.xMin, plotBounds.yMax);
        var topRightSite = voronoi.NearestSitePoint(plotBounds.xMax, plotBounds.yMax);

        var topLeft = new Vector3(plotBounds.xMin, 0, plotBounds.yMax);
        var topRight = new Vector3(plotBounds.xMax, 0, plotBounds.yMax);
        var bottomLeft = new Vector3(plotBounds.xMin, 0, plotBounds.yMin);
        var bottomRight = new Vector3(plotBounds.xMax, 0, plotBounds.yMin);

        var siteEdges = new Dictionary<Vector2, List<LineSegment>>();

        int edgePointsRemoved = 0;
        var voronoiEdges = voronoi.Edges();
        var epsilonSqr = 0.001f * 0.001f;

        Debug.Log(string.Format("\t\tSetup: {0:n0}ms", DateTime.Now.Subtract(time).TotalMilliseconds));
        time = DateTime.Now;

        foreach (var edge in voronoiEdges)
        {
            if (!edge.visible)
                continue;

            var p1 = edge.clippedEnds[Delaunay.LR.Side.LEFT];
            var p2 = edge.clippedEnds[Delaunay.LR.Side.RIGHT];

            if ((p1.Value - p2.Value).sqrMagnitude < epsilonSqr)
            {
                edgePointsRemoved++;
                continue;
            }

            var segment = new LineSegment(p1, p2);
            if (edge.leftSite != null)
            {
                if (!siteEdges.ContainsKey(edge.leftSite.Coord)) siteEdges.Add(edge.leftSite.Coord, new List<LineSegment>());
                siteEdges[edge.leftSite.Coord].Add(segment);
            }
            if (edge.rightSite != null)
            {
                if (!siteEdges.ContainsKey(edge.rightSite.Coord)) siteEdges.Add(edge.rightSite.Coord, new List<LineSegment>());
                siteEdges[edge.rightSite.Coord].Add(segment);
            }
            
        }
        if(edgePointsRemoved>0) Debug.LogWarning(string.Format("{0} edge points too close and have been removed", edgePointsRemoved));
        Debug.Log(string.Format("\t\tVoronoiEdges: {0:n0}ms", DateTime.Now.Subtract(time).TotalMilliseconds));
        time = DateTime.Now;

        var voronoiSiteCoords = voronoi.SiteCoords();
        foreach (var site in voronoiSiteCoords)
        {
            var boundries = GetBoundriesForSite(siteEdges, site);
            var center = ToVector3(site);
            var currentNode = new MapNode { centerPoint = center };
            nodesByCenterPosition.Add(center, currentNode);

            MapNodeHalfEdge firstEdge = null;
            MapNodeHalfEdge previousEdge = null;

            for (var i = 0; i < boundries.Count; i++)
            {
                var edge = boundries[i];

                var start = ToVector3(edge.p0.Value);
                var end = ToVector3(edge.p1.Value);
                if (start == end) continue;

                previousEdge = AddEdge(edgesByStartPosition, previousEdge, start, end, currentNode);
                firstEdge ??= previousEdge;
                currentNode.startEdge ??= previousEdge;

                // We need to figure out if the two edges meet, and if not then insert some more edges to close the polygon
                var insertEdges = false;
                if (i < boundries.Count - 1)
                {
                    start = ToVector3(boundries[i].p1.Value);
                    end = ToVector3(boundries[i + 1].p0.Value);
                    insertEdges = start != end;
                }
                else if (i == boundries.Count - 1)
                {
                    start = ToVector3(boundries[i].p1.Value);
                    end = ToVector3(boundries[0].p0.Value);
                    insertEdges = start != end;
                }

                if (insertEdges)
                {
                    // Check which corners are within this node
                    var startIsTop = start.z == plotBounds.yMax;
                    var startIsBottom = start.z == plotBounds.yMin;
                    var startIsLeft = start.x == plotBounds.xMin;
                    var startIsRight = start.x == plotBounds.xMax;

                    var hasTopLeft = site == topLeftSite && !(startIsTop && startIsLeft);
                    var hasTopRight = site == topRightSite && !(startIsTop && startIsRight);
                    var hasBottomLeft = site == bottomLeftSite && !(startIsBottom && startIsLeft);
                    var hasBottomRight = site == bottomRightSite && !(startIsBottom && startIsRight);

                    if (startIsTop)
                    {
                        if (hasTopRight) previousEdge = AddEdge(edgesByStartPosition, previousEdge, start, topRight, currentNode);
                        if (hasBottomRight) previousEdge = AddEdge(edgesByStartPosition, previousEdge, previousEdge.destination.position, bottomRight, currentNode);
                        if (hasBottomLeft) previousEdge = AddEdge(edgesByStartPosition, previousEdge, previousEdge.destination.position, bottomLeft, currentNode);
                        if (hasTopLeft) previousEdge = AddEdge(edgesByStartPosition, previousEdge, previousEdge.destination.position, topLeft, currentNode);

                    }
                    else if (startIsRight)
                    {
                        if (hasBottomRight) previousEdge = AddEdge(edgesByStartPosition, previousEdge, start, bottomRight, currentNode);
                        if (hasBottomLeft) previousEdge = AddEdge(edgesByStartPosition, previousEdge, previousEdge.destination.position, bottomLeft, currentNode);
                        if (hasTopLeft) previousEdge = AddEdge(edgesByStartPosition, previousEdge, previousEdge.destination.position, topLeft, currentNode);
                        if (hasTopRight) previousEdge = AddEdge(edgesByStartPosition, previousEdge, previousEdge.destination.position, topRight, currentNode);
                    }
                    else if (startIsBottom)
                    {
                        if (hasBottomLeft) previousEdge = AddEdge(edgesByStartPosition, previousEdge, start, bottomLeft, currentNode);
                        if (hasTopLeft) previousEdge = AddEdge(edgesByStartPosition, previousEdge, previousEdge.destination.position, topLeft, currentNode);
                        if (hasTopRight) previousEdge = AddEdge(edgesByStartPosition, previousEdge, previousEdge.destination.position, topRight, currentNode);
                        if (hasBottomRight) previousEdge = AddEdge(edgesByStartPosition, previousEdge, previousEdge.destination.position, bottomRight, currentNode);
                    }
                    else if (startIsLeft)
                    {
                        if (hasTopLeft) previousEdge = AddEdge(edgesByStartPosition, previousEdge, start, topLeft, currentNode);
                        if (hasTopRight) previousEdge = AddEdge(edgesByStartPosition, previousEdge, previousEdge.destination.position, topRight, currentNode);
                        if (hasBottomRight) previousEdge = AddEdge(edgesByStartPosition, previousEdge, previousEdge.destination.position, bottomRight, currentNode);
                        if (hasBottomLeft) previousEdge = AddEdge(edgesByStartPosition, previousEdge, previousEdge.destination.position, bottomLeft, currentNode);
                    }

                    previousEdge = AddEdge(edgesByStartPosition, previousEdge, previousEdge.destination.position, end, currentNode);
                }
            }

            // Connect up the end of the loop
            previousEdge.next = firstEdge;
            firstEdge.previous = previousEdge;
            AddLeavingEdge(firstEdge);
        }
        Debug.Log(string.Format("\t\tVoronoiSiteCoords: {0:n0}ms", DateTime.Now.Subtract(time).TotalMilliseconds));
        time = DateTime.Now;

        ConnectOpposites(edgesByStartPosition);
        Debug.Log(string.Format("\t\tConnectOpposites: {0:n0}ms", DateTime.Now.Subtract(time).TotalMilliseconds));
    }

    internal Vector3 GetCenter()
    {
        return ToVector3(plotBounds.center);
    }

    private static void AddLeavingEdge(MapNodeHalfEdge edge)
    {
        if (edge.previous.destination.leavingEdge == null) edge.previous.destination.leavingEdge = edge;
    }

    private void UpdateHeights(in HeightMap heightmap)
    {
        var nodesByCenterPositionValues = nodesByCenterPosition.Values;
        int xMax = heightmap.values.GetLength(0), yMax = heightmap.values.GetLength(1);

        foreach (var node in nodesByCenterPositionValues)
        {
            node.centerPoint = UpdateHeight(heightmap, node.centerPoint, xMax, yMax);
        }
        var pointsValues = points.Values;
        foreach (var point in pointsValues)
        {
            point.position = UpdateHeight(heightmap, point.position, xMax, yMax);
        }
    }

    private static Vector3 UpdateHeight(in HeightMap heightmap, in Vector3 oldPosition, in int xMax, in int yMax)
    {
        var position = oldPosition;
        var x = Mathf.FloorToInt(position.x);
        var y = Mathf.FloorToInt(position.z);
        if (x >= 0 && y >= 0 && x < xMax && y < yMax)
        {
            position.y = heightmap.values[x, y];
        }
        return position;
    }

    private List<LineSegment> GetBoundriesForSite(Voronoi voronoi, Vector2 site)
    {
        var boundries = voronoi.VoronoiBoundaryForSite(site);

        // Sort boundries clockwise
        FlipSegmentsClockwise(ref boundries, site);
        SortSegmentsClockwise(ref boundries, site);
        return boundries;
    }

    private List<LineSegment> GetBoundriesForSite(Dictionary<Vector2, List<LineSegment>> siteEdges, Vector2 site)
    {
        var boundries = siteEdges[site];

        // Sort boundries clockwise
        FlipSegmentsClockwise(ref boundries, site);
        SortSegmentsClockwise(ref boundries, site);
        SnapBoundries(ref boundries, 0.001f);

        return boundries;
    }

    private static void SnapBoundries(ref List<LineSegment> boundries, float snapDistance)
    {
        float snapDistanceSqr = snapDistance * snapDistance;

        for (int i = boundries.Count - 1; i >= 0; i--)
        {
            if ((boundries[i].p0.Value - boundries[i].p1.Value).sqrMagnitude < snapDistanceSqr)
            {
                var previous = i - 1;
                var next = i + 1;
                if (previous < 0) previous = boundries.Count - 1;
                if (next >= boundries.Count) next = 0;

                if ((boundries[previous].p1.Value - boundries[next].p0.Value).sqrMagnitude < snapDistanceSqr)
                {
                    boundries[previous].p1 = boundries[next].p0;
                }
                boundries.Remove(boundries[i]);
            }
        }
    }

    public MapNode GetClosestNode(float x, float y)
    {
        var position = new Vector3(x, 0, y);
        MapNode closestNode = null;
        float lowestDistanceSqr = int.MaxValue;
        foreach (var node in nodesByCenterPosition.Values)
        {
            var direction = node.centerPoint - position;
            var distanceSqr = direction.sqrMagnitude;
            if (distanceSqr < lowestDistanceSqr)
            {
                closestNode = node;
                lowestDistanceSqr = distanceSqr;
            }
        }
        return closestNode;
    }

    private void ConnectOpposites(Dictionary<Vector3, List<MapNodeHalfEdge>> edgesByStartPosition)
    {
        for (int i = 0; i < edges.Count; i++)
        {
            MapNodeHalfEdge edge = edges[i];
            if (edge.opposite != null)
                continue;

            var startEdgePosition = edge.previous.destination.position;
            var endEdgePosition = edge.destination.position;

            if (!edgesByStartPosition.ContainsKey(endEdgePosition))
                continue;
            
            var list = edgesByStartPosition[endEdgePosition];
            MapNodeHalfEdge opposite = null;
            foreach (var item in list)
            {
                // We use .5f to snap the coordinates to each other, otherwise there are holes in the graph
                if (Math.Abs(item.destination.position.x - startEdgePosition.x) < 0.5f && Math.Abs(item.destination.position.z - startEdgePosition.z) < 0.5f)
                {
                    opposite = item;
                }
            }
            if (opposite != null)
            {
                edge.opposite = opposite;
                opposite.opposite = edge;
            }
            else
            {
                // TODO: We need to check that this is at the world boundry, otherwise it's a bug
                var isAtEdge = endEdgePosition.x == 0 || endEdgePosition.x == plotBounds.width || endEdgePosition.z == 0 || endEdgePosition.z == plotBounds.height ||
                    startEdgePosition.x == 0 || startEdgePosition.x == plotBounds.width || startEdgePosition.z == 0 || startEdgePosition.z == plotBounds.height;

                if (!isAtEdge)
                {
                    edge.node.nodeType = MapNodeType.Error;
                    Debug.LogWarning("Edges without opposites must be at the boundry edge");
                }
            }
        }
    }

    private void SortSegmentsClockwise(ref List<LineSegment> segments, Vector2 center)
    {
        segments.Sort((line1, line2) =>
        {
            var firstVector = line1.p0.Value - center;
            var secondVector = line2.p0.Value - center;
            var angle = Vector2.SignedAngle(firstVector, secondVector);

            return angle > 0 ? 1 : (angle < 0 ? -1 : 0);
        });
    }

    private void FlipSegmentsClockwise(ref List<LineSegment> segments, in Vector2 center)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            LineSegment line = segments[i];
            var lineP0 = line.p0;
            var lineP1 = line.p1;
            var firstVector = lineP0.Value - center;
            var secondVector = lineP1.Value - center;
            var angle = Vector2.SignedAngle(firstVector, secondVector);
            if (angle > 0) segments[i] = new LineSegment(lineP1, lineP0);
        }
    }

    private MapNodeHalfEdge AddEdge(Dictionary<Vector3, List<MapNodeHalfEdge>> edgesByStartPosition, MapNodeHalfEdge previous, in Vector3 start, in Vector3 end, MapNode node)
    {
        if (start == end)
        {
            Debug.Assert(start != end, "Start and end vectors must not be the same");
        }
        var currentEdge = new MapNodeHalfEdge { node = node };

        if (!points.ContainsKey(start)) points.Add(start, new MapPoint { position = start, leavingEdge = currentEdge });
        if (!points.ContainsKey(end)) points.Add(end, new MapPoint { position = end });

        currentEdge.destination = points[end];

        if (!edgesByStartPosition.ContainsKey(start)) edgesByStartPosition.Add(start, new List<MapNodeHalfEdge>());
        edgesByStartPosition[start].Add(currentEdge);
        edges.Add(currentEdge);

        if (previous != null)
        {
            previous.next = currentEdge;
            currentEdge.previous = previous;
            AddLeavingEdge(currentEdge);
        }
        return currentEdge;
    }

    private Vector3 ToVector3(in Vector2 vector)
    {
        return new Vector3(Mathf.Round(vector.x * 1000f) * .001f, 0, Mathf.Round(vector.y * 1000f) * .001f);
        //return new Vector3(vector.x, 0f, vector.y);
    }
}