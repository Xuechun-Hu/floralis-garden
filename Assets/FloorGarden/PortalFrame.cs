using UnityEngine;

// Each quad carries its source UV centre so flecks detach from the exact painted carving.
internal sealed class PortalFrame
{
    private const int Columns=80, Rows=100;
    private readonly Transform window;
    private readonly MeshFilter filter;
    private readonly Mesh pieces;
    private readonly Material ink;
    public PortalFrame(PortalArt art, Transform parent)
    {
        window=parent;
        Vector3 Position(float u,float v)=>new((u-.5f)*1.34f,(v-.5f)*1.67f,-.02f);
        ink=art.MaterialFor("PortalFrame");
        ink.SetTexture("_MainTex",Resources.Load<Texture2D>("LilyWindow"));
        var panel=PortalArt.Show("Ivory lily carving",parent,art.SurfaceMesh("Lily arch",1,1,Position),ink);
        filter=panel.GetComponent<MeshFilter>();
        const int cols=Columns, rows=Rows;
        var positions=new Vector3[cols*rows*4];
        var uv=new Vector2[positions.Length];var centers=new Vector2[positions.Length];
        var indices=new int[cols*rows*6];
        for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)
        {
            int cell=y*cols+x,k=cell*4,t=cell*6;
            var center=new Vector2((x+.5f)/cols,(y+.5f)/rows);
            for(int j=0;j<4;j++)
            {
                float u=(x+(j&1))/(float)cols,v=(y+(j>>1))/(float)rows;
                positions[k+j]=Position(u,v);uv[k+j]=new Vector2(u,v);centers[k+j]=center;
            }
            indices[t]=k;indices[t+1]=k+2;indices[t+2]=k+1;
            indices[t+3]=k+1;indices[t+4]=k+2;indices[t+5]=k+3;
        }
        pieces=art.Own(new Mesh{name="Lily carving flecks"});
        pieces.vertices=positions;pieces.uv=uv;pieces.uv2=centers;pieces.triangles=indices;
        pieces.bounds=new Bounds(new Vector3(0,-1,-.7f),new Vector3(4,8,4));
    }
    public System.Collections.Generic.List<Vector3> Begin(PortalSurfaces surfaces)
    {
        const int across=19, deep=9;
        var points=new Vector3[across*deep];
        var bs=new Vector3[points.Length];var cs=new Vector3[points.Length];
        var normals=new Vector3[points.Length];
        var valid=new bool[points.Length];
        var random=new System.Random(8701);
        float Rand()=>(float)random.NextDouble();
        for(int z=0;z<deep;z++)for(int x=0;x<across;x++)
        {
            int i=z*across+x;
            var probe=new Vector3(Mathf.Lerp(-1.05f,1.05f,(x+Rand())/across),.43f,
                                  -Mathf.Lerp(.13f,1.57f,(z+Rand())/deep));
            valid[i]=surfaces.TryLanding(window.TransformPoint(probe),out points[i],out bs[i],out cs[i],out normals[i]);
        }
        var targets=new Vector4[Columns*Rows*4];
        var landingNormals=new Vector3[targets.Length];
        var landingSites=new Vector3?[points.Length];
        int landed=0;
        for(int cell=0;cell<Columns*Rows;cell++)
        {
            float u=(cell%Columns+.5f)/Columns;
            int x=Mathf.Clamp(Mathf.FloorToInt((u+(Rand()-.5f)*.31f)*across),0,across-1);
            int i=random.Next(deep)*across+x;
            if(!valid[i])continue;
            // Both points and all convex combinations below are inside the same scanned triangle.
            var edge=Vector3.Lerp(bs[i],cs[i],Rand());
            var hit=Vector3.MoveTowards(points[i],edge,.075f*Rand());
            landingSites[i]??=hit;
            var local=window.InverseTransformPoint(hit+normals[i]*.0015f);
            var dest=new Vector4(local.x,local.y,local.z,1);
            var localNormal=window.InverseTransformDirection(normals[i]);
            for(int k=0;k<4;k++){targets[4*cell+k]=dest;landingNormals[4*cell+k]=localNormal;}
            landed++;
        }
        pieces.SetUVs(2,targets);pieces.SetUVs(3,landingNormals);
        filter.sharedMesh=pieces;
        Debug.Log($"[AngelWindow] Floor rays with hits {System.Array.FindAll(valid,ok=>ok).Length}/{points.Length}, frame cells with landing targets {landed}");
        // Visit cells in a permuted order so both jambs and the path get flowers.
        var flowerSites=new System.Collections.Generic.List<Vector3>(20);
        for(int n=0;n<landingSites.Length && flowerSites.Count<20;n++)
        {
            var candidate=landingSites[(n*71+41)%landingSites.Length];
            if(!candidate.HasValue)continue;
            bool spaced=true;
            foreach(var site in flowerSites)
                if((candidate.Value-site).sqrMagnitude<.24f*.24f){spaced=false;break;}
            if(spaced)flowerSites.Add(candidate.Value);
        }
        return flowerSites;
    }
    public void Tick(float age)=>ink.SetFloat("_Age",age);
}
