using UnityEngine;

// One bounded mesh for airborne dust and its surface-aligned prints.
internal sealed class ButterflyDust : MonoBehaviour
{
    private struct Grain { public Vector3 from,to,probe; public Quaternion resting; public float born,fall,hold,size,phase; public bool checkContact,discarded; }
    private const int Capacity=144;
    private readonly Grain[] grains=new Grain[Capacity];
    private readonly Vector3[] corners=new Vector3[Capacity*4];
    private readonly Color32[] colors=new Color32[Capacity*4];
    private readonly int[] indices=new int[Capacity*6];
    private Mesh mesh;
    private PortalSurfaces surfaces;
    private float floorY;
    private bool loggedHit,loggedMiss;
    private int count,next,visibleCount;

    public void Initialize(PortalArt art,PortalSurfaces scan,float height)
    {
        surfaces=scan;floorY=height;
        mesh=art.Own(new Mesh{name="Butterfly gold dust"});
        mesh.MarkDynamic();mesh.vertices=corners;
        var uv=new Vector2[corners.Length];
        for(int i=0;i<Capacity;i++)
        {
            int k=i*4,t=i*6;
            uv[k]=Vector2.zero;uv[k+1]=Vector2.right;uv[k+2]=Vector2.up;uv[k+3]=Vector2.one;
            indices[t]=k;indices[t+1]=k+2;indices[t+2]=k+1;
            indices[t+3]=k+1;indices[t+4]=k+2;indices[t+5]=k+3;
        }
        mesh.uv=uv;
        PortalArt.Show("Pollen grains",transform,mesh,art.MaterialFor("FloraPollen"));
    }
    public void UseGardenFloor()=>surfaces=null;
    public void SetFloorHeight(float height)=>floorY=height;
    public void Drop(Vector3 origin,bool guideTrail=false)
    {
        var drift=Random.insideUnitCircle*.085f;
        var probe=origin+new Vector3(drift.x,0,drift.y);
        var normal=Vector3.up;
        var target=new Vector3(probe.x,floorY,probe.z);
        if(surfaces!=null)
        {
            if(!surfaces.TryLanding(probe,out target,out _,out _,out normal))
            {
                if(!loggedMiss)Debug.Log($"[ButterflyDust] No Floor triangle at {probe:F3}; skipping VST dust");
                loggedMiss=true;
                return;
            }
            if(!loggedHit)Debug.Log($"[ButterflyDust] VST Floor triangle at {target:F3}, normal {normal:F3}");
            loggedHit=true;
        }
        target+=normal*.0015f;
        float phase=Random.Range(0,6.28f);
        var resting=Quaternion.FromToRotation(Vector3.forward,normal)*Quaternion.AngleAxis(phase*Mathf.Rad2Deg,Vector3.forward);
        grains[next]=new Grain{from=origin,to=target,probe=probe,resting=resting,born=Time.unscaledTime,checkContact=surfaces!=null,
            fall=Mathf.Sqrt(Mathf.Max(.015f,origin.y-target.y))*(guideTrail?.95f:1.35f),
            hold=guideTrail?Random.Range(2.1f,2.8f):Random.Range(.72f,1.12f),
            size=guideTrail?Random.Range(.060f,.112f):Random.Range(.038f,.074f),phase=phase};
        next=(next+1)%grains.Length;count=Mathf.Min(count+1,grains.Length);
    }
    private void LateUpdate()
    {
        var eye=Camera.main;
        if(!mesh || !eye)return;
        DrawGrains(eye.transform.rotation,Time.unscaledTime);
    }
    private void DrawGrains(Quaternion view,float now)
    {
        int n=0;
        var worldToLocal=transform.worldToLocalMatrix;
        for(int i=0;i<count;i++)
        {
            var g=grains[i];float age=now-g.born,groundAge=age-g.fall;
            if(g.discarded || groundAge>g.hold+.62f)continue;
            // A mesh block can move or disappear while the grain falls.
            if(g.checkContact && groundAge>=0)
            {
                g.checkContact=false;
                if(surfaces!=null)
                {
                    if(surfaces.TryLanding(g.probe,out var hit,out _,out _,out var normal))
                    {
                        g.to=hit+normal*.0015f;
                        g.resting=Quaternion.FromToRotation(Vector3.forward,normal)*Quaternion.AngleAxis(g.phase*Mathf.Rad2Deg,Vector3.forward);
                    }
                    else g.discarded=true;
                }
                grains[i]=g;
                if(g.discarded)continue;
            }
            float t=Mathf.Clamp01(age/g.fall);
            var p=Vector3.Lerp(g.from,g.to,t);
            p.y=Mathf.Lerp(g.from.y,g.to.y,t*(.38f+.62f*t));
            p.x+=Mathf.Sin(Mathf.PI*t)*.035f*Mathf.Sin(g.phase+age*4);
            p.z+=Mathf.Sin(Mathf.PI*t)*.046f*Mathf.Cos(g.phase+age*2.6f);
            float spread=Mathf.SmoothStep(0,1,groundAge/.45f);
            float alpha=1-Mathf.SmoothStep(0,1,(groundAge-g.hold)/.62f);
            var angle=g.phase*Mathf.Rad2Deg+38*t;
            var pose=g.resting;
            if(t<1)
            {
                var airborne=view*Quaternion.AngleAxis(angle,Vector3.forward);
                pose=Quaternion.Slerp(airborne,g.resting,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.76f,1,t)));
            }
            float radius=g.size*(1+.33f*spread)*.5f;
            var x=worldToLocal.MultiplyVector(pose*Vector3.right*radius);
            var y=worldToLocal.MultiplyVector(pose*Vector3.up*radius);
            var pos=worldToLocal.MultiplyPoint3x4(p);
            int k=n++*4;
            corners[k]=pos-x-y;corners[k+1]=pos+x-y;corners[k+2]=pos-x+y;corners[k+3]=pos+x+y;
            var tint=new Color32(255,255,255,(byte)Mathf.RoundToInt(alpha*255));
            colors[k]=colors[k+1]=colors[k+2]=colors[k+3]=tint;
        }
        if(n==0 && visibleCount==0)return;
        mesh.SetVertices(corners);
        mesh.SetColors(colors);
        if(n!=visibleCount){mesh.SetTriangles(indices,0,n*6,0,false);visibleCount=n;}
        mesh.RecalculateBounds();
    }
}
