using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class CeilingPetals
{
    private struct Fall { public Vector3 start; public float endY, born; public int seed; }
    private readonly Dictionary<Guid,Vector3> ceiling = new();
    private readonly List<Vector3> sources = new();
    private const int Limit = 5;
    private readonly List<Fall> falling = new(Limit);
    private readonly GameObject holder;
    private readonly Mesh mesh = new() { name="Falling petals" };
    private readonly GardenArt art = new();
    private float nextSpawn;
    private int sequence;
    public int Count => falling.Count;
    public CeilingPetals(Transform parent,Material material)
    {
        holder=new GameObject("Petals beneath scanned ceiling");
        holder.transform.SetParent(parent,false);
        mesh.MarkDynamic(); holder.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=holder.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;
    }
    public void Remove(Guid id) => ceiling.Remove(id);
    public void Set(Guid id,Vector3 point) => ceiling[id]=point;
    public void Update(Vector3 focus,Dictionary<Vector2Int,Vector3> floor,float now)
    {
        if(now>=nextSpawn)
        {
            nextSpawn=now+1.6f;
            sources.Clear();
            foreach(var p in ceiling.Values) if((p-focus).sqrMagnitude<21) sources.Add(p);
            if(sources.Count>0 && falling.Count<Limit)
            {
                int seed=WallFlowers.Hash(++sequence,7,41);
                var p=sources[seed%sources.Count];
                var cell=new Vector2Int(Mathf.RoundToInt(p.x/FloorGrass.Spacing),Mathf.RoundToInt(p.z/FloorGrass.Spacing));
                float ground= p.y-2.5f;
                if(floor.TryGetValue(cell,out var f)) ground=f.y;
                falling.Add(new Fall {start=p-Vector3.up*0.035f,endY=ground+0.035f,born=now,seed=seed});
            }
        }
        if(falling.Count==0 && mesh.vertexCount==0) return;
        art.Clear();
        for(int i=falling.Count-1;i>=0;i--)
        {
            var fall=falling[i];
            float t=now-fall.born, phase=(fall.seed%630)*0.01f;
            float drop=t*(0.31f+(fall.seed%11)*0.012f);
            var p=fall.start+new Vector3((Mathf.Sin(t*2+phase)-Mathf.Sin(phase))*0.09f,-drop,
                (Mathf.Cos(t*1.6f+phase)-Mathf.Cos(phase))*0.07f);
            if(p.y<=fall.endY) {falling.RemoveAt(i);continue;}
            float fade=Mathf.Clamp01(Mathf.Min(t*3,(p.y-fall.endY)*6));
            art.FallingPetal(p,Quaternion.Euler(37+30*Mathf.Sin(t*3+phase),phase*50+t*80,t*54),
                (0.028f+(fall.seed%5)*0.002f)*fade,fall.seed);
        }
        art.Upload(mesh);
    }
    public void Clear() {ceiling.Clear();falling.Clear();mesh.Clear();}
    public void Dispose() {UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(holder);}
}
