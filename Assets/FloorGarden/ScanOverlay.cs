using System;
using System.Collections.Generic;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.Rendering;

// Each device block keeps its original triangle indices and world pose.
internal sealed class ScanOverlay
{
    private readonly GameObject root = new("Space Pro scan overlay");
    private readonly Dictionary<Guid, MeshFilter> blocks = new();
    private readonly Material[] materials;
    private readonly List<int> faces = new(), floorLines = new(), otherLines = new();
    private readonly HashSet<uint> floorEdges = new(), otherEdges = new();
    private readonly Dictionary<Mesh, (SubMeshDescriptor floor, SubMeshDescriptor other)> hiddenEdges = new();
    public bool Visible { get; private set; }
    public int FaceCount { get; private set; }

    public ScanOverlay(Transform parent, Material grass)
    {
        root.transform.SetParent(parent, false);
        materials = new[] {
            new Material(Resources.Load<Shader>("FloorGardenScanDepth")),
            new Material(grass), new Material(grass)
        };
        materials[1].SetColor("_BaseColor", new Color(0.36f, 0.95f, 0.12f));
        materials[2].SetColor("_BaseColor", new Color(0.03f, 0.75f, 1f));
    }

    public void Toggle()
    {
        Visible = !Visible;
        foreach (var filter in blocks.Values)
        {
            var mesh = filter.sharedMesh;
            if (!Visible) HideEdges(mesh);
            else if (hiddenEdges.TryGetValue(mesh, out var saved))
            {
                mesh.SetSubMesh(1, saved.floor, MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetSubMesh(2, saved.other, MeshUpdateFlags.DontRecalculateBounds);
            }
        }
        if (Visible) hiddenEdges.Clear();
    }

    private void HideEdges(Mesh mesh)
    {
        var floor = mesh.GetSubMesh(1);
        var other = mesh.GetSubMesh(2);
        hiddenEdges[mesh] = (floor, other);
        floor.indexCount = other.indexCount = 0;
        mesh.SetSubMesh(1, floor, MeshUpdateFlags.DontRecalculateBounds);
        mesh.SetSubMesh(2, other, MeshUpdateFlags.DontRecalculateBounds);
    }

    public void BeginBlock()
    {
        faces.Clear(); floorLines.Clear(); otherLines.Clear();
        floorEdges.Clear(); otherEdges.Clear();
    }

    public void AddFace(int a, int b, int c, bool floor)
    {
        faces.Add(a); faces.Add(b); faces.Add(c);
        var seen = floor ? floorEdges : otherEdges;
        var lines = floor ? floorLines : otherLines;
        AddEdge(a, b, seen, lines);
        AddEdge(b, c, seen, lines);
        AddEdge(c, a, seen, lines);
    }

    private static void AddEdge(int a, int b, HashSet<uint> seen, List<int> lines)
    {
        uint key = ((uint)Mathf.Min(a, b) << 16) | (uint)Mathf.Max(a, b);
        if (!seen.Add(key)) return;
        lines.Add(a); lines.Add(b);
    }

    public void Commit(PxrSpatialMeshInfo block)
    {
        if (!blocks.TryGetValue(block.uuid, out var filter))
        {
            var child = new GameObject("Spatial mesh block");
            child.transform.SetParent(root.transform, false);
            filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = new Mesh { name = "Spatial surface and edges" };
            filter.sharedMesh.MarkDynamic();
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            blocks.Add(block.uuid, filter);
        }
        var mesh = filter.sharedMesh;
        if (mesh.subMeshCount == 3) FaceCount -= (int)mesh.GetIndexCount(0) / 3;
        filter.transform.SetPositionAndRotation(block.position, block.rotation);
        mesh.Clear();
        mesh.SetVertices(block.vertices);
        mesh.subMeshCount = 3;
        mesh.SetIndices(faces, MeshTopology.Triangles, 0, false);
        mesh.SetIndices(floorLines, MeshTopology.Lines, 1, false);
        mesh.SetIndices(otherLines, MeshTopology.Lines, 2, false);
        mesh.RecalculateBounds();
        if (!Visible) HideEdges(mesh);
        FaceCount += faces.Count / 3;
    }

    public void Remove(Guid id)
    {
        if (!blocks.TryGetValue(id, out var filter)) return;
        FaceCount -= (int)filter.sharedMesh.GetIndexCount(0) / 3;
        hiddenEdges.Remove(filter.sharedMesh);
        UnityEngine.Object.Destroy(filter.sharedMesh);
        UnityEngine.Object.Destroy(filter.gameObject);
        blocks.Remove(id);
    }

    public void Clear()
    {
        foreach (var id in new List<Guid>(blocks.Keys)) Remove(id);
    }

    public void Dispose()
    {
        Clear();
        foreach (var material in materials) UnityEngine.Object.Destroy(material);
        UnityEngine.Object.Destroy(root);
    }
}
