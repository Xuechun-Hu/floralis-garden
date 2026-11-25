using System;
using System.Collections.Generic;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class FloorGrass : MonoBehaviour
{
    public const int TuftLimit = 48;
    private const int BlockLimit = 128;
    private const int PointsPerBlock = 512;
    internal const float Spacing = 0.22f;
    private readonly Dictionary<Guid, List<Vector3>> patches = new();
    private readonly Dictionary<Guid, PxrSpatialMeshInfo> pending = new();
    private readonly Queue<Guid> queue = new();
    private readonly HashSet<Vector2Int> cells = new();
    private readonly GardenArt art = new();
    private readonly GardenArt vineArt = new();
    private readonly WallFlowers flowers = new();
    private Material foliage;
    private Material viewTint;
    private Mesh screenTriangle;
    private CeilingPetals petals;
    private readonly List<XRMeshSubsystem> subsystems = new();
    private readonly Dictionary<Vector2Int, Vector3> candidates = new();
    private readonly Dictionary<Vector2Int, float> missingSince = new();
    private readonly Dictionary<Vector2Int, Vector3> planted = new();
    private readonly List<Vector2Int> ranked = new();
    private readonly Dictionary<Guid, Vector3> centers = new();
    private ScanOverlay overlay;
    private Vector3 eye, focus, lastFocus;
    private bool buttonHeld;
    private float nextLog;
    private Mesh grass;
    private Mesh vineMesh;
    private XRMeshSubsystem scanner;
    private float nextScan, nextDraw;
    private bool changed, permission;

    private void Awake()
    {
        grass = new Mesh { name = "Scanned floor grass" };
        grass.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = grass;
        var renderer = GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
        renderer.receiveShadows = true;
        overlay = new ScanOverlay(transform, renderer.sharedMaterial);
        foliage = new Material(Resources.Load<Shader>("GardenInk"));
        renderer.sharedMaterial = foliage;
        var vines = new GameObject("Climbing vines");
        vines.transform.SetParent(transform, false);
        vineMesh = new Mesh { name = "Rooted wall vines", indexFormat = IndexFormat.UInt32 };
        vineMesh.MarkDynamic();
        vines.AddComponent<MeshFilter>().sharedMesh = vineMesh;
        var vineRenderer = vines.AddComponent<MeshRenderer>();
        vineRenderer.sharedMaterial = foliage;
        vineRenderer.shadowCastingMode = ShadowCastingMode.TwoSided;
        petals = new CeilingPetals(transform, foliage);
        viewTint = new Material(Resources.Load<Shader>("GardenViewTint"));
        screenTriangle = new Mesh { name = "Garden view tint" };
        screenTriangle.vertices = new[] { new Vector3(-1,-1), new Vector3(3,-1), new Vector3(-1,3) };
        screenTriangle.triangles = new[] { 0, 1, 2 };
    }

    private void OnEnable()
    {
        PXR_Manager.SpatialMeshDataUpdated += QueueBlocks;
#if UNITY_ANDROID && !UNITY_EDITOR
        PXR_PermissionRequest.RequestUserPermissionMR(null, _ => permission = true);
#else
        permission = true;
#endif
    }

    private void QueueBlocks(List<PxrSpatialMeshInfo> updates)
    {
        foreach (var block in updates)
        {
            if (block.state == MeshChangeState.Unchanged) continue;
            if (!pending.ContainsKey(block.uuid))
            {
                if (pending.Count >= BlockLimit && !centers.ContainsKey(block.uuid)) continue;
                queue.Enqueue(block.uuid);
            }
            pending[block.uuid] = block;
        }
    }

    private void Update()
    {
        var head = Camera.main;
        if (head)
        {
            var ahead = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
            eye = transform.InverseTransformPoint(head.transform.position);
            focus = transform.InverseTransformPoint(head.transform.position + ahead * 1.1f);
            var tintPass = new RenderParams(viewTint)
            {
                camera = head,
                worldBounds = new Bounds(head.transform.position, Vector3.one * 4f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };
            Graphics.RenderMesh(tintPass, screenTriangle, 0, Matrix4x4.identity);
        }
        bool pressed = InputDevices.GetDeviceAtXRNode(XRNode.RightHand).TryGetFeatureValue(CommonUsages.primaryButton, out var button) && button;
        if (pressed && !buttonHeld)
        {
            overlay.Toggle();
        }
        buttonHeld = pressed;
        if (permission && (scanner == null || !scanner.running) && Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count > 0)
            {
                scanner = subsystems[0];
                if (!scanner.running) scanner.Start();
            }
        }
        for (int count = 0; count < 2 && queue.Count > 0; count++)
        {
            var id = queue.Dequeue();
            var block = pending[id];
            pending.Remove(id);
            if (block.state == MeshChangeState.Removed || block.vertices == null ||
                block.indices == null || block.vertices.Length == 0 || block.indices.Length < 3)
            {
                centers.Remove(id);
                petals.Remove(id);
                overlay.Remove(id);
                changed |= patches.Remove(id);
                changed |= flowers.Remove(id);
                continue;
            }
            var blockToGrass = transform.worldToLocalMatrix * Matrix4x4.TRS(block.position, block.rotation, Vector3.one);
            var center = blockToGrass.MultiplyPoint3x4(block.vertices[block.vertices.Length / 2]);
            if (!centers.ContainsKey(id) && centers.Count >= BlockLimit)
            {
                var farthest = id;
                float farDistance = (center - focus).sqrMagnitude;
                foreach (var entry in centers)
                {
                    var distance = (entry.Value - focus).sqrMagnitude;
                    if (distance <= farDistance) continue;
                    farthest = entry.Key;
                    farDistance = distance;
                }
                if (farthest == id) continue;
                centers.Remove(farthest);
                petals.Remove(farthest);
                patches.Remove(farthest);
                flowers.Remove(farthest);
                overlay.Remove(farthest);
            }
            centers[id] = center;
            if (!patches.TryGetValue(id, out var points))
            {
                if (patches.Count >= BlockLimit) continue;
                points = new List<Vector3>(PointsPerBlock);
            }
            points.Clear();
            overlay.BeginBlock();
            flowers.Begin(id);
            SampleSurfaces(block, points, blockToGrass);
            overlay.Commit(block);
            if (points.Count > 0) patches[id] = points;
            else patches.Remove(id);
            changed = true;
        }
        if (Time.unscaledTime >= nextDraw && (changed || flowers.Animating || missingSince.Count > 0 || (lastFocus-focus).sqrMagnitude > 0.04f))
        {
            bool floorChanged = changed || (lastFocus-focus).sqrMagnitude > 0.04f;
            if (floorChanged) CollectFloorSamples();
            vineArt.Clear();
            int oldVines = flowers.Count;
            flowers.Draw(vineArt, focus, Time.unscaledTime, candidates);
            vineArt.Upload(vineMesh);
            if (floorChanged || oldVines != flowers.Count || missingSince.Count > 0) Redraw();
            lastFocus = focus;
            nextDraw = Time.unscaledTime + (flowers.Animating ? 0.07f : 0.35f);
            changed = false;
        }
        petals.Update(focus, candidates, Time.unscaledTime);
        if (Time.unscaledTime >= nextLog)
        {
            Debug.Log($"[FloorGarden] blocks={centers.Count} grass={planted.Count} vines={flowers.Count} petals={petals.Count} plantFaces={(grass.GetIndexCount(0)+vineMesh.GetIndexCount(0))/3} meshFaces={overlay.FaceCount} overlay={overlay.Visible}");
            nextLog = Time.unscaledTime + 5f;
        }
    }

    private void SampleSurfaces(PxrSpatialMeshInfo block, List<Vector3> points, Matrix4x4 blockToGrass)
    {
        bool labeled = block.labels?.Length == block.vertices.Length;
        float ceilingArea = 0;
        petals.Remove(block.uuid);
        cells.Clear();
        int triangles = block.indices.Length / 3;
        for (int face = 0; face < triangles; face++)
        {
            int first = block.indices[3 * face], second = block.indices[3 * face + 1], third = block.indices[3 * face + 2];
            if (first >= block.vertices.Length || second >= block.vertices.Length || third >= block.vertices.Length) continue;
            var start = blockToGrass.MultiplyPoint3x4(block.vertices[first]);
            var side = blockToGrass.MultiplyVector(block.vertices[second] - block.vertices[first]);
            var end = blockToGrass.MultiplyVector(block.vertices[third] - block.vertices[first]);
            var normal = Vector3.Cross(side, end);
            if (labeled && block.labels[first] == PxrSemanticLabel.Ceiling &&
                block.labels[second] == PxrSemanticLabel.Ceiling && block.labels[third] == PxrSemanticLabel.Ceiling &&
                normal.sqrMagnitude > ceilingArea && Mathf.Abs(normal.y) >= 0.8f * normal.magnitude)
            {
                ceilingArea = normal.sqrMagnitude;
                petals.Set(block.uuid, start + (side + end) / 3f);
            }
            bool eligible = labeled && block.labels[first] == PxrSemanticLabel.Floor && block.labels[second] == PxrSemanticLabel.Floor && block.labels[third] == PxrSemanticLabel.Floor && Mathf.Abs(normal.y) >= 0.85f * normal.magnitude && normal.sqrMagnitude > 1e-10f;
            overlay.AddFace(first, second, third, eligible);
            if (labeled && block.labels[first] == PxrSemanticLabel.Wall &&
                block.labels[second] == PxrSemanticLabel.Wall && block.labels[third] == PxrSemanticLabel.Wall &&
                normal.sqrMagnitude > 1e-10f && Mathf.Abs(normal.y) < 0.35f * normal.magnitude)
                flowers.Sample(start, side, end, normal, eye);
            if (!eligible || points.Count >= PointsPerBlock) continue;
            float det = side.x * end.z - side.z * end.x;
            var low = Vector3.Min(start, Vector3.Min(start + side, start + end));
            var high = Vector3.Max(start, Vector3.Max(start + side, start + end));
            int minX = Mathf.FloorToInt(low.x / Spacing), minZ = Mathf.FloorToInt(low.z / Spacing);
            int maxX = Mathf.Min(minX + 64, Mathf.CeilToInt(high.x / Spacing));
            int maxZ = Mathf.Min(minZ + 64, Mathf.CeilToInt(high.z / Spacing));
            for (int row = minZ; row <= maxZ && points.Count < PointsPerBlock; row++)
            for (int col = minX; col <= maxX && points.Count < PointsPerBlock; col++)
            {
                int seed = WallFlowers.Hash(col, row, 17);
                float dx = (col + ((seed % 61) - 30) * 0.01f) * Spacing - start.x;
                float dz = (row + (((seed >> 8) % 61) - 30) * 0.01f) * Spacing - start.z;
                float along = (dx * end.z - dz * end.x) / det, across = (side.x * dz - side.z * dx) / det;
                if (along < 0 || across < 0 || along + across > 1 || !cells.Add(new Vector2Int(col, row))) continue;
                points.Add(start + along * side + across * end);
            }
        }
    }

    private float Score(Vector2Int cell)
    {
        float dx = cell.x * Spacing - focus.x, dz = cell.y * Spacing - focus.z;
        return dx*dx + dz*dz - (planted.ContainsKey(cell) ? 0.65f : 0);
    }

    private void CollectFloorSamples()
    {
        candidates.Clear();
        foreach (var points in patches.Values)
        {
            foreach (var point in points)
            {
                float dx = point.x - focus.x, dz = point.z - focus.z;
                if (dx * dx + dz * dz > 25f) continue;
                var cell = new Vector2Int(Mathf.RoundToInt(point.x / Spacing), Mathf.RoundToInt(point.z / Spacing));
                if (!candidates.TryGetValue(cell, out var previous) || point.y < previous.y) candidates[cell] = point;
            }
        }
    }

    private void Redraw()
    {
        float now = Time.unscaledTime;
        ranked.Clear();
        foreach (var entry in candidates)
        {
            var cell = entry.Key;
            // Stagger widely spaced planting sites. Keep the finer grid for
            // recognising contact between walls, floor and falling petals.
            if (cell.y % 3 != 0 || (cell.x - (cell.y / 3 & 1)) % 3 != 0) continue;
            if (!flowers.NearRoot(entry.Value, 0.95f) && WallFlowers.Hash(cell.x,cell.y,89) % 7 != 0) continue;
            ranked.Add(cell);
        }
        cells.Clear();
        cells.UnionWith(ranked);
        foreach (var entry in planted)
        {
            if (cells.Contains(entry.Key))
            {
                missingSince.Remove(entry.Key);
                continue;
            }
            if (!missingSince.TryGetValue(entry.Key, out var since))
                missingSince[entry.Key] = since = now;
            if (now - since <= 1.4f) ranked.Add(entry.Key);
        }
        ranked.Sort((left,right) => {
            int order = Score(left).CompareTo(Score(right));
            return order != 0 ? order : left.x != right.x ? left.x.CompareTo(right.x) : left.y.CompareTo(right.y);
        });
        cells.Clear();
        bool geometryChanged = planted.Count != Mathf.Min(TuftLimit, ranked.Count);
        for (int index = 0; index < Mathf.Min(TuftLimit, ranked.Count); index++)
        {
                var cell = ranked[index];
                if (!candidates.TryGetValue(cell, out var point)) point = planted[cell];
                cells.Add(cell);
                if (planted.TryGetValue(cell, out var previous))
                {
                    point.y = Mathf.Abs(previous.y-point.y) < 0.012f ? previous.y : Mathf.Lerp(previous.y,point.y,0.28f);
                    geometryChanged |= (previous-point).sqrMagnitude > 1e-10f;
                }
                else geometryChanged = true;
                planted[cell] = point;
        }
        ranked.Clear();
        foreach (var cell in planted.Keys) if (!cells.Contains(cell)) ranked.Add(cell);
        foreach (var cell in ranked) { planted.Remove(cell); missingSince.Remove(cell); }
        if (geometryChanged)
        {
            art.Clear();
            foreach (var plant in planted) art.Tuft(plant.Value, plant.Key);
            art.Upload(grass);
        }
    }

    private void OnDisable()
    {
        PXR_Manager.SpatialMeshDataUpdated -= QueueBlocks;
        if (scanner != null && scanner.running) scanner.Stop();
        scanner = null;
        pending.Clear();
        queue.Clear();
        patches.Clear();
        flowers.Clear();
        petals?.Clear();
        centers.Clear(); planted.Clear(); missingSince.Clear();
        overlay?.Clear();
        if (grass != null) grass.Clear();
        if (vineMesh != null) vineMesh.Clear();
    }

    private void OnDestroy()
    {
        if (grass != null) Destroy(grass);
        if (vineMesh != null) Destroy(vineMesh);
        if (foliage != null) Destroy(foliage);
        if (viewTint != null) Destroy(viewTint);
        if (screenTriangle != null) Destroy(screenTriangle);
        petals?.Dispose();
        overlay?.Dispose();
    }
}
