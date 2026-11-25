using System;
using System.Collections.Generic;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.XR;

// Snapshot labelled triangles in tracking space. Only intersect blocks touched by the swept orb.
internal sealed class PortalSurfaces
{
    private struct Face { public Vector3 a,b,c,n; public bool floor; }
    private sealed class Patch
    {
        public readonly List<Face> faces=new();
        public Bounds bounds;
        public Vector3 floorCenter;
        public int floors;
    }
    private readonly Dictionary<Guid,Patch> patches=new();
    // Losing every previously usable block invalidates placements even if tracking
    // never reports an interruption and a replacement mesh arrives in this batch.
    public bool Update(List<PxrSpatialMeshInfo> changes)
    {
        bool hadSupport=patches.Count>0;
        foreach(var block in changes)
            if(block.state==MeshChangeState.Removed)patches.Remove(block.uuid);
        bool lostSupport=hadSupport && patches.Count==0;
        foreach(var block in changes)
        {
            if(block.state==MeshChangeState.Removed || block.state==MeshChangeState.Unchanged)continue;
            patches.Remove(block.uuid);
            if(block.vertices==null || block.indices==null || block.labels?.Length!=block.vertices.Length) continue;
            var world=Matrix4x4.TRS(block.position,block.rotation,Vector3.one);
            var patch=new Patch();
            for(int i=0;i+2<block.indices.Length;i+=3)
            {
                int a=block.indices[i],b=block.indices[i+1],c=block.indices[i+2];
                if(a>=block.vertices.Length || b>=block.vertices.Length || c>=block.vertices.Length)continue;
                var label=block.labels[a];bool floor=label==PxrSemanticLabel.Floor;
                if((!floor && label!=PxrSemanticLabel.Wall) || label!=block.labels[b] || label!=block.labels[c])continue;
                var face=new Face{a=world.MultiplyPoint3x4(block.vertices[a]),b=world.MultiplyPoint3x4(block.vertices[b]),
                    c=world.MultiplyPoint3x4(block.vertices[c]),floor=floor};
                face.n=Vector3.Cross(face.b-face.a,face.c-face.a).normalized;
                if(face.n.sqrMagnitude<.5f || (floor ? Mathf.Abs(face.n.y)<.78f : Mathf.Abs(face.n.y)>.39f))continue;
                if(patch.faces.Count==0)patch.bounds=new Bounds(face.a,Vector3.zero);
                patch.faces.Add(face);patch.bounds.Encapsulate(face.a);patch.bounds.Encapsulate(face.b);patch.bounds.Encapsulate(face.c);
                if(floor) {patch.floors++;patch.floorCenter+=(face.a+face.b+face.c)/3;}
            }
            if(patch.faces.Count==0)continue;
            if(patch.floors>0)patch.floorCenter/=patch.floors;
            patches[block.uuid]=patch;
        }
        return lostSupport;
    }
    public float FloorHeight(Vector3 eye)
    {
        float height=eye.y-1.55f,best=float.PositiveInfinity;
        foreach(var patch in patches.Values)
        {
            if(patch.floors==0 || patch.floorCenter.y>=eye.y-.45f)continue;
            float d=(patch.floorCenter-eye).sqrMagnitude;
            if(d<best){height=patch.floorCenter.y;best=d;}
        }
        return height;
    }
    public bool Hit(Vector3 from,Vector3 to,float radius,out Vector3 point,out Vector3 normal,out bool floor)
    {
        point=normal=default;floor=false;
        var swept=new Bounds(from,Vector3.zero);swept.Encapsulate(to);swept.Expand(2*radius);
        var motion=to-from;float best=2;
        foreach(var patch in patches.Values)
        {
            if(!swept.Intersects(patch.bounds))continue;
            foreach(var f in patch.faces)
            {
                var n=Vector3.Dot(motion,f.n)<0?f.n:-f.n;
                float closing=-Vector3.Dot(motion,n),dist=Vector3.Dot(from-f.a,n);
                if(closing<1e-6f || dist<-.001f)continue;
                float t=Mathf.Max(0,(dist-radius)/closing);
                if(t>1 || t>=best)continue;
                var center=from+t*motion;
                var p=center-n*Vector3.Dot(center-f.a,n);
                if(Vector3.Dot(Vector3.Cross(f.b-f.a,p-f.a),f.n)<-.00001f ||
                   Vector3.Dot(Vector3.Cross(f.c-f.b,p-f.b),f.n)<-.00001f ||
                   Vector3.Dot(Vector3.Cross(f.a-f.c,p-f.c),f.n)<-.00001f)continue;
                best=t;point=p;normal=n;floor=f.floor;
            }
        }
        return best<=1;
    }
    // Highest scanned floor under a probe. Keep its triangle so flecks can spread while
    // remaining inside a confirmed floor face, even at holes and irregular scan edges.
    public bool TryLanding(Vector3 probe, out Vector3 point, out Vector3 b, out Vector3 c)
    {
        return TryLanding(probe,out point,out b,out c,out _);
    }
    public bool TryLanding(Vector3 probe,out Vector3 point,out Vector3 b,out Vector3 c,out Vector3 normal)
    {
        normal=Vector3.up;
        point=b=c=default;
        float highest=probe.y-4f;
        bool found=false;
        foreach(var patch in patches.Values)
        {
            if(patch.floors==0)continue;
            var min=patch.bounds.min;var max=patch.bounds.max;
            if(probe.x<min.x || probe.x>max.x || probe.z<min.z || probe.z>max.z ||
               max.y<highest || min.y>probe.y)continue;
            foreach(var f in patch.faces)
            {
                if(!f.floor)continue;
                var q=probe;
                q.y=f.a.y-((q.x-f.a.x)*f.n.x+(q.z-f.a.z)*f.n.z)/f.n.y;
                if(q.y<highest || q.y>probe.y)continue;
                if(Vector3.Dot(Vector3.Cross(f.b-f.a,q-f.a),f.n)<-.00001f ||
                   Vector3.Dot(Vector3.Cross(f.c-f.b,q-f.b),f.n)<-.00001f ||
                   Vector3.Dot(Vector3.Cross(f.a-f.c,q-f.c),f.n)<-.00001f)continue;
                highest=q.y;point=q;b=f.b;c=f.c;normal=f.n.y>=0?f.n:-f.n;found=true;
            }
        }
        return found;
    }

    public void Clear()=>patches.Clear();
}
