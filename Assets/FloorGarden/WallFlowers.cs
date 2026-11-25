using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class WallFlowers
{
    private const float Lane = 1.2f, Step = 0.13f, DepthBin = 0.24f;
    private const int Limit = 4, MaxNodes = 20;
    private struct Site { public Vector3 point, normal; }
    private sealed class Vine
    {
        public readonly List<Site> path = new(MaxNodes+1);
        public Vector3Int key;
        public float length, grown, lastSeen;
    }
    private readonly Dictionary<Guid,Dictionary<Vector3Int,Site>> blocks = new();
    private readonly Dictionary<Vector3Int,Site> wall = new();
    private readonly List<(Vector3Int key, float y, float score)> roots = new();
    private readonly List<Vine> vines = new();
    private readonly List<Site> path = new(MaxNodes+1);
    private Dictionary<Vector3Int,Site> working;
    private float previousTime;
    public int Count => vines.Count;
    public bool Animating { get; private set; }

    public bool NearRoot(Vector3 point, float radius)
    {
        foreach (var vine in vines)
            if ((vine.path[0].point - point).sqrMagnitude < radius * radius) return true;
        return false;
    }

    public void Begin(Guid id)
    {
        if(!blocks.TryGetValue(id,out working)) blocks[id]=working=new();
        working.Clear();
    }
    public bool Remove(Guid id) => blocks.Remove(id);
    public void Clear() { blocks.Clear(); vines.Clear(); previousTime=0; Animating=false; }

    public void Sample(Vector3 a,Vector3 ab,Vector3 ac,Vector3 normal,Vector3 eye)
    {
        bool useX=Mathf.Abs(normal.z)>=Mathf.Abs(normal.x);
        float u=useX?a.x:a.z, du=useX?ab.x:ab.z, eu=useX?ac.x:ac.z;
        float det=du*ac.y-eu*ab.y;
        normal.Normalize();
        if(Vector3.Dot(normal,eye-a)<0) normal=-normal;
        int c0=Mathf.FloorToInt(Mathf.Min(u,Mathf.Min(u+du,u+eu))/Lane)-1;
        int c1=Mathf.CeilToInt(Mathf.Max(u,Mathf.Max(u+du,u+eu))/Lane)+1;
        int r0=Mathf.CeilToInt(Mathf.Min(a.y,Mathf.Min(a.y+ab.y,a.y+ac.y))/Step);
        int r1=Mathf.FloorToInt(Mathf.Max(a.y,Mathf.Max(a.y+ab.y,a.y+ac.y))/Step);
        for(int c=c0;c<=c1;c++)
        {
            float phase=(Hash(c,0,useX?1:2)%630)*0.01f;
            for(int r=r0;r<=r1 && working.Count<1024;r++)
            {
                float h=r*Step;
                float x=c*Lane+Mathf.Sin(h*3.1f+phase)*0.14f+Mathf.Sin(h*5.4f+phase)*0.045f-u;
                float y=h-a.y;
                float s=(x*ac.y-eu*y)/det, t=(du*y-ab.y*x)/det;
                if(s<0 || t<0 || s+t>1) continue;
                var point=a+s*ab+t*ac;
                int depth=Mathf.RoundToInt((useX?point.z:point.x)/DepthBin);
                working[new Vector3Int(c,r,2*depth+(useX?1:0))]=new Site {point=point,normal=normal};
            }
        }
    }
    internal static int Hash(int x,int y,int z)
    {
        unchecked {
            uint h=(uint)(x*73856093 ^ y*19349663 ^ z*83492791);
            h^=h>>16; h*=0x7feb352d; h^=h>>15;
            return (int)(h&0x7fffffff);
        }
    }

    private static bool FloorAt(Vector3 p,Dictionary<Vector2Int,Vector3> floor,out float y)
    {
        var grid=new Vector2Int(Mathf.RoundToInt(p.x/FloorGrass.Spacing),Mathf.RoundToInt(p.z/FloorGrass.Spacing));
        float nearest=0.45f*0.45f;
        y=float.NegativeInfinity;
        for(int i=-2;i<=2;i++)
        for(int j=-2;j<=2;j++)
        {
            if(!floor.TryGetValue(grid+new Vector2Int(i,j),out var f)) continue;
            float dh=p.y-f.y;
            if(dh < -0.02f || dh>0.34f) continue;
            float dx=p.x-f.x,dz=p.z-f.z,d=dx*dx+dz*dz;
            if(d>=nearest) continue;
            nearest=d; y=f.y;
        }
        return !float.IsNegativeInfinity(y);
    }

    private void Trace(Vector3Int key,float floorY)
    {
        path.Clear();
        var site=wall[key];
        var root=site; root.point.y=floorY;
        path.Add(root);
        int heightSteps=MaxNodes-Hash(key.x,0,key.z)%6;
        for(int i=0;i<heightSteps;i++)
        {
            if((site.point-path[path.Count-1].point).sqrMagnitude>0.0001f) path.Add(site);
            var nextKey=new Vector3Int(key.x,key.y+1,key.z);
            float closest=0.265f*0.265f;
            Site next=default;
            bool found=false;
            for(int d=-1;d<=1;d++)
            {
                var probe=new Vector3Int(nextKey.x,nextKey.y,key.z+d*2);
                if(!wall.TryGetValue(probe,out var v) || Mathf.Abs(Vector3.Dot(site.normal,v.normal))<0.72f) continue;
                float distance=(v.point-site.point).sqrMagnitude;
                if(distance>=closest) continue;
                if(Vector3.Dot(site.normal,v.normal)<0) v.normal=-v.normal;
                found=true; closest=distance; next=v; nextKey=probe;
            }
            if(!found) break;
            key=nextKey;site=next;
        }
    }

    public void Draw(GardenArt art,Vector3 focus,float now,Dictionary<Vector2Int,Vector3> floor)
    {
        wall.Clear(); roots.Clear();
        foreach(var block in blocks.Values)
        foreach(var pair in block)
            if((pair.Value.point-focus).sqrMagnitude<27f) wall[pair.Key]=pair.Value;
        foreach(var pair in wall)
        {
            // Only the foot of a continuous run can seed a vine.
            var key=pair.Key;
            if(wall.ContainsKey(new Vector3Int(key.x,key.y-1,key.z))) continue;
            if(FloorAt(pair.Value.point,floor,out var y))
                roots.Add((key,y,(pair.Value.point-focus).sqrMagnitude));
        }
        roots.Sort((a,b)=>a.score.CompareTo(b.score));
        foreach(var root in roots)
        {
            var basePoint=wall[root.key].point; basePoint.y=root.y;
            Vine vine=null;
            foreach(var v in vines)
                if(v.key.x==root.key.x && (v.key.z&1)==(root.key.z&1) && (v.path[0].point-basePoint).sqrMagnitude<0.13f)
                { vine=v; break; }
            if(vine!=null && vine.lastSeen==now) continue;
            if(vine==null && (vines.Count>=Limit || NearRoot(basePoint, 1.05f))) continue;
            Trace(root.key,root.y);
            if(path.Count<2) continue;
            if(vine==null) { vine=new Vine { key=root.key };vines.Add(vine); }
            vine.lastSeen=now;
            // Align rebuilt samples to the room side chosen when this vine rooted.
            bool reverse=vine.path.Count>0 && Vector3.Dot(vine.path[0].normal,path[0].normal)<0;
            vine.path.Clear(); vine.length=0;
            for(int i=0;i<path.Count;i++)
            {
                var node=path[i];
                if(reverse) node.normal=-node.normal;
                node.point+=node.normal*0.016f;
                if(i>0) vine.length+=Vector3.Distance(vine.path[i-1].point,node.point);
                vine.path.Add(node);
            }
        }
        float delta=Mathf.Clamp(now-previousTime,0,0.36f);
        previousTime=now; Animating=false;
        for(int i=vines.Count-1;i>=0;i--)
        {
            var vine=vines[i];
            if(now-vine.lastSeen>1.5f) {vines.RemoveAt(i);continue;}
            vine.grown=Mathf.Min(vine.length,vine.grown+delta*(0.29f+0.024f*(Hash(vine.key.x,0,vine.key.z)%6)));
            Animating|=vine.grown<vine.length || vine.lastSeen<now;
            Render(art,vine);
        }
    }
    private static void Render(GardenArt art,Vine vine)
    {
        float along=0;
        int seed=Hash(vine.key.x,0,vine.key.z);
        for(int i=1;i<vine.path.Count && along<vine.grown;i++)
        {
            var a=vine.path[i-1];var b=vine.path[i];
            float distance=Vector3.Distance(a.point,b.point);
            float remaining=vine.grown-along;
            float t=Mathf.Clamp01(remaining/Mathf.Max(distance,0.0001f));
            var tip=Vector3.Lerp(a.point,b.point,t);
            float r0=Mathf.Lerp(0.0025f,0.009f,Mathf.Clamp01(remaining/0.25f));
            float r1=Mathf.Lerp(0.0025f,0.009f,Mathf.Clamp01((remaining-distance)/0.25f));
            art.StemSegment(a.point,tip,a.normal,r0,r1);
            if(i>1) art.Branch(a.point,a.normal,seed,i,Mathf.Clamp01(remaining/0.22f));
            along+=distance;
        }
    }
}
