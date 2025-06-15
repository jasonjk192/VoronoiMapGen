using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a texture by writing to a render texture using GL
/// 
/// References:
/// https://docs.unity3d.com/ScriptReference/GL.html
/// https://forum.unity.com/threads/rendering-gl-to-a-texture2d-immediately-in-unity4.158918/
/// </summary>
public static class MapTextureGenerator
{
    private static Material drawingMaterial;

    private static void CreateDrawingMaterial()
    {
        if (!drawingMaterial)
        {
            // Unity has a built-in shader that is useful for drawing
            // simple colored things.
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            drawingMaterial = new Material(shader);
            drawingMaterial.hideFlags = HideFlags.HideAndDontSave;
            // Turn on alpha blending
            drawingMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            drawingMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            // Turn backface culling off
            drawingMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            // Turn off depth writes
            drawingMaterial.SetInt("_ZWrite", 0);
        }
    }

    public static Texture2D GenerateTexture(MapGraph map, int meshSize, int textureSize, List<MapNodeTypeColor> colours, bool drawBoundries, bool drawTriangles, bool drawCenters)
    {
        CreateDrawingMaterial(); 
        var texture = RenderGLToTexture(map, textureSize, meshSize, drawingMaterial, colours, drawBoundries, drawTriangles, drawCenters);

        return texture;
    }

    public static RenderTexture GenerateRenderTexture(MapGraph map, int meshSize, int textureSize, List<MapNodeTypeColor> colours, bool drawBoundries, bool drawTriangles, bool drawCenters)
    {
        CreateDrawingMaterial();
        var renderTexture = new RenderTexture(textureSize, textureSize, 0);
        RenderTexture.active = renderTexture;
        GL.Clear(false, true, Color.white);
        GL.sRGBWrite = false;

        DrawToRenderTexture(map, drawingMaterial, textureSize, meshSize, colours, drawBoundries, drawTriangles, drawCenters);

        return renderTexture;
    }

    private static Texture2D RenderGLToTexture(MapGraph map, int textureSize, int meshSize, Material material, List<MapNodeTypeColor> colours, bool drawBoundries, bool drawTriangles, bool drawCenters)
    {
        var time = DateTime.Now;
        var renderTexture = CreateRenderTexture(textureSize, Color.white);
        Debug.Log(string.Format("CreateRenderTexture: {0:n0}ms", DateTime.Now.Subtract(time).TotalMilliseconds));
        time = DateTime.Now;

        // render GL immediately to the active render texture //
        DrawToRenderTexture(map, material, textureSize, meshSize, colours, drawBoundries,drawTriangles, drawCenters);
        Debug.Log(string.Format("DrawToRenderTexture: {0:n0}ms", DateTime.Now.Subtract(time).TotalMilliseconds));
        time = DateTime.Now;

        return CreateTextureFromRenderTexture(textureSize, renderTexture);
    }

    private static Texture2D CreateTextureFromRenderTexture(int textureSize, RenderTexture renderTexture)
    {
        var time = DateTime.Now;
        // read the active RenderTexture into a new Texture2D //
        Texture2D newTexture = new Texture2D(textureSize, textureSize, TextureFormat.ARGB32, false);
        Graphics.CopyTexture(renderTexture, newTexture);

        //newTexture.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
        Debug.Log(string.Format("ReadPixels: {0:n0}ms", DateTime.Now.Subtract(time).TotalMilliseconds));
        time = DateTime.Now;

        // apply pixels and compress //
        bool applyMipsmaps = false;
        newTexture.Apply(applyMipsmaps);
        Debug.Log(string.Format("Apply: {0:n0}ms", DateTime.Now.Subtract(time).TotalMilliseconds));
        time = DateTime.Now;
        bool highQuality = true;
        newTexture.Compress(highQuality);
        Debug.Log(string.Format("Compress: {0:n0}ms", DateTime.Now.Subtract(time).TotalMilliseconds));
        time = DateTime.Now;

        // clean up after the party //
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(renderTexture);

        // return the goods //
        return newTexture;
    }

    private static RenderTexture CreateRenderTexture(int textureSize, Color color)
    {
        // get a temporary RenderTexture //
        RenderTexture renderTexture = RenderTexture.GetTemporary(textureSize, textureSize);

        // set the RenderTexture as global target (that means GL too)
        RenderTexture.active = renderTexture;

        // clear GL //
        GL.Clear(false, true, color);
        GL.sRGBWrite = false;
        
        return renderTexture;
    }

    private static void DrawToRenderTexture(MapGraph map, Material material, int textureSize, int meshSize, List<MapNodeTypeColor> colours, bool drawBoundries, bool drawTriangles, bool drawCenters)
    {
        material.SetPass(0);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, meshSize, 0, meshSize);
        GL.Viewport(new Rect(0, 0, textureSize, textureSize));

        var coloursDictionary = new Dictionary<MapGraph.MapNodeType, Color>();
        foreach (var colour in colours)
        {
            if (!coloursDictionary.ContainsKey(colour.type)) coloursDictionary.Add(colour.type, colour.color);
        }

        /*
        var coloursDictionary = new Dictionary<MapGraph.MapNodeType, Color>
        {
            { MapGraph.MapNodeType.Beach, new Color(210f / 255f, 180f / 255f, 124f / 255f) },
            { MapGraph.MapNodeType.Grass, new Color(109f / 255f, 154f / 255f, 102f / 255f) },
            { MapGraph.MapNodeType.FreshWater, new Color(48f / 255f, 104f / 255f, 153f / 255f) },
            { MapGraph.MapNodeType.SaltWater, new Color(68f / 255f, 68f / 255f, 120f / 255f) },
            { MapGraph.MapNodeType.Mountain, new Color(162f / 255f, 99f / 255f, 68f / 255f) },
            { MapGraph.MapNodeType.Snow, new Color(248f / 255f, 248f / 255f, 248f / 255f) },
            { MapGraph.MapNodeType.City, Color.gray },
            { MapGraph.MapNodeType.Error, Color.red }
        };
        */

        DrawNodeTypes(map, coloursDictionary);

        if (drawCenters) DrawCenterPoints(map, Color.red);
        if (drawBoundries) DrawEdges(map, Color.black);
        DrawRivers(map, 2, coloursDictionary[MapGraph.MapNodeType.FreshWater]);
        if (drawTriangles) DrawDelauneyEdges(map, Color.red);

        GL.PopMatrix();
    }

    private static void DrawEdges(MapGraph map, Color color)
    {
        GL.Begin(GL.LINES);
        GL.Color(color);

        foreach (var edge in map.edges)
        {
            var start = edge.GetStartPosition();
            var end = edge.GetEndPosition();

            GL.Vertex3(start.x, start.z, 0);
            GL.Vertex3(end.x, end.z, 0);
        }

        GL.End();
    }

    private static void DrawDelauneyEdges(MapGraph map, Color color)
    {
        GL.Begin(GL.LINES);
        GL.Color(color);

        foreach (var edge in map.edges)
        {
            if (edge.opposite != null)
            {
                var start = edge.node.centerPoint;
                var end = edge.opposite.node.centerPoint;

                GL.Vertex3(start.x, start.z, 0);
                GL.Vertex3(end.x, end.z, 0);
            }
        }

        GL.End();
    }

    private static void DrawRivers(MapGraph map, int minRiverSize, Color color)
    {
        List<(Vector3 start, Vector3 end)> lineData = new();
        foreach (var edge in map.edges)
        {
            if (edge.water < minRiverSize) continue;

            var start = edge.GetStartPosition();
            var end = edge.GetEndPosition();

            lineData.Add((start, end));
        }

        GL.Begin(GL.LINES);
        GL.Color(color);

        foreach (var (start, end) in lineData)
        {
            GL.Vertex3(start.x, start.z, 0);
            GL.Vertex3(end.x, end.z, 0);
        }

        GL.End();
    }

    private static void DrawCenterPoints(MapGraph map, Color color)
    {
        GL.Begin(GL.QUADS);
        GL.Color(color);

        foreach (var point in map.nodesByCenterPosition.Values)
        {
            var x = point.centerPoint.x;
            var y = point.centerPoint.z;
            GL.Vertex3(x - .25f, y - .25f, 0);
            GL.Vertex3(x - .25f, y + .25f, 0);
            GL.Vertex3(x + .25f, y + .25f, 0);
            GL.Vertex3(x + .25f, y - .25f, 0);
        }

        GL.End();
    }

    private struct TriangleData
    {
        public Vector2 v1, v2, v3;
        public Color color;
    }

    private static void DrawNodeTypes(MapGraph map, Dictionary<MapGraph.MapNodeType, Color> colours)
    {
        var time = DateTime.Now;

        var nodesByCenterPositionValues = map.nodesByCenterPosition.Values;

        List<TriangleData> triangleData = new();
        foreach (var node in nodesByCenterPositionValues)
        {
            var color = colours.ContainsKey(node.nodeType) ? colours[node.nodeType] : Color.red;
            var edges = node.GetEdges();

            foreach (var edge in edges)
            {
                var start = edge.previous.destination.position;
                var end = edge.destination.position;

                triangleData.Add(new TriangleData() { v1 = new Vector2(node.centerPoint.x, node.centerPoint.z),
                                  v2 = new Vector2(start.x, start.z),
                                  v3 = new Vector2(end.x, end.z),
                                  color = color });
            }
        }


        GL.Begin(GL.TRIANGLES);
        foreach (var data in triangleData)
        {
            GL.Color(data.color);
            GL.Vertex3(data.v1.x, data.v1.y, 0);
            GL.Vertex3(data.v2.x, data.v2.y, 0);
            GL.Vertex3(data.v3.x, data.v3.y, 0);
        }
        GL.End();

        Debug.Log(string.Format("DrawNodeTypes: {0:n0}ms", DateTime.Now.Subtract(time).TotalMilliseconds));
    }
}
