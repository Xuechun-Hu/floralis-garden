using System.Collections.Generic;
using UnityEngine;

// Curves are modelled in metres. UV.x runs across a leaf, UV.y along its midrib.
internal sealed class GardenArt
{
    private readonly List<Vector3> vertices = new(), normals = new();
    private readonly List<Vector2> uv = new();
    private readonly List<Color32> colors = new();
    private readonly List<int> indices = new();
    private static readonly Color32 Stem = new(158,147,90,255), LeafGreen = new(144,164,122,255),
        Fresh = new(190,192,140,255), Meadow = new(126,157,129,255),
        Rose = new(232,175,156,255), Lilac = new(211,193,170,255), Honey = new(228,192,120,255),
        Peach = new(241,188,164,255), Cream = new(253,231,195,255), Umber = new(124,91,66,255);

    public void Clear() { vertices.Clear(); normals.Clear(); uv.Clear(); colors.Clear(); indices.Clear(); }
    public void Upload(Mesh mesh)
    {
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetNormals(normals);
        mesh.SetUVs(0,uv); mesh.SetColors(colors); mesh.SetTriangles(indices,0);
    }
    private int Vertex(Vector3 p, Vector3 n, Color32 color, float u, float v)
    {
        vertices.Add(p); normals.Add(n); colors.Add(color); uv.Add(new Vector2(u,v));
        return vertices.Count-1;
    }
    private void Triangle(int a,int b,int c) { indices.Add(a); indices.Add(b); indices.Add(c); }
    private void Quad(int a,int b,int c,int d) { Triangle(a,b,c); Triangle(a,c,d); }

    // A folded ribbon with a raised spine, curved outline and a distinct tip.
    private void Leaf(Vector3 root, Vector3 axis, Vector3 width, Vector3 arch, Color32 tint)
    {
        Vector3 n = Vector3.Cross(width,axis).normalized;
        if (Vector3.Dot(n,arch)<0) n=-n;
        var ridge = n * width.magnitude * 0.33f;
        int first=vertices.Count;
        for (int row=0; row<6; row++)
        {
            float t=row/5f, curve=4*t*(1-t);
            var mid=root+axis*t+arch*curve;
            var span=width*(0.04f+0.96f*curve*(1.12f-0.3f*t));
            var shade=Color.Lerp((Color)tint*0.68f,tint,0.23f+t*0.77f);
            Vertex(mid-span, (n-width.normalized*0.32f).normalized,shade,0,t);
            Vertex(mid+ridge*curve,n,shade,0.5f,t);
            Vertex(mid+span, (n+width.normalized*0.32f).normalized,shade,1,t);
            if(row==0) continue;
            int p=first+3*row;
            Quad(p-3,p-2,p+1,p); Quad(p-2,p-1,p+2,p+1);
        }
    }
    public void Tuft(Vector3 root,Vector2Int key)
    {
        int seed=WallFlowers.Hash(key.x,key.y,3);
        float phase=(seed%360)*Mathf.Deg2Rad, size=0.68f+(seed%71)*0.011f;
        for(int i=0;i<7;i++)
        {
            float angle=phase+i*2.39996f;
            var outwards=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            var tangent=Vector3.Cross(Vector3.up,outwards);
            float curl=(i%3)*0.014f;
            var basePoint=root+outwards*(0.01f*size);
            Leaf(basePoint, (outwards*(0.076f+curl)+Vector3.up*(0.047f+0.017f*(i%4))+tangent*curl)*size,
                tangent*((i%3==0?0.019f:0.009f)*size),
                (Vector3.up*0.036f-outwards*0.009f)*size,i%3==0?Fresh:Meadow);
        }
        if(seed%5==0)
        {
            var top=root+(Vector3.up*0.137f+new Vector3(0.018f,0,0.012f))*size;
            StemSegment(root,top,Vector3.forward,0.0023f,0.0014f);
            for(int i=0;i<6;i++)
            {
                float a=phase+i*Mathf.PI/3;
                var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Leaf(top,dir*(0.026f*size)+Vector3.up*0.008f,Vector3.Cross(dir,Vector3.up)*0.009f,
                    Vector3.up*0.006f, new Color32(255,224,159,255));
            }
        }
    }

    public int IndexCount => indices.Count;

    public void Sedge(Vector3 root, float phase)
    {
        for(int i=0;i<5;i++)
        {
            float angle=phase+i*2.4f;
            var dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            var side=Vector3.Cross(Vector3.up,dir);
            Leaf(root,dir*(.065f+.014f*i)+Vector3.up*(.12f+.015f*i),side*.008f,
                Vector3.up*.055f, i%2==0?Meadow:Fresh);
            if(i>=2)continue;
            var neck=root+dir*.046f+Vector3.up*(.18f+.035f*i);
            StemSegment(root,neck,side,.0022f,.0016f);
            Leaf(neck,dir*.024f+Vector3.up*.036f,side*.005f,Vector3.up*.003f,Honey);
        }
    }

    public void VineSegment(Vector3 start, Vector3 end, int segment, int segments)
    {
        float t=(float)segment/segments;
        StemSegment(start,end,Vector3.up,Mathf.Lerp(.0055f,.0023f,t),Mathf.Lerp(.0052f,.002f,t));
        if(segment%2==0)return;
        var tangent=(end-start).normalized;
        var side=Vector3.Cross(Vector3.up,tangent).normalized*(segment%4==1?1:-1);
        var leaf=(side*.75f+tangent*.38f+Vector3.up*.52f)*Mathf.Lerp(.114f,.057f,t);
        Leaf(end,leaf,Vector3.Cross(Vector3.up,leaf).normalized*leaf.magnitude*.33f,
            Vector3.up*.025f,t>.72f?Fresh:LeafGreen);
    }

    public void Pebble(Vector3 center,float radius,float angle,Color32 color)
    {
        int first=vertices.Count;
        const int sides=9;
        var rotation=Quaternion.Euler(0,angle,0);
        float aspect=1.12f+.28f*Mathf.Abs(Mathf.Sin(angle));
        float height=.72f+.34f*Mathf.Abs(Mathf.Cos(angle));
        for(int ring=0;ring<4;ring++)
        {
            float elevation=ring==0?-.28f:ring==1?.19f:ring==2?.88f:Mathf.PI*.5f;
            for(int i=0;i<sides;i++)
            {
                float a=2*Mathf.PI*i/sides;
                float bulge=1+.11f*Mathf.Sin(3*a+angle);
                var normal=new Vector3(Mathf.Cos(elevation)*Mathf.Cos(a),Mathf.Sin(elevation),Mathf.Cos(elevation)*Mathf.Sin(a));
                var p=Vector3.Scale(normal,new Vector3(aspect*bulge,height,bulge))*radius;
                p.x+=radius*.19f*Mathf.Max(0,normal.y);
                p.y+=radius*.09f*Mathf.Cos(a+angle)*Mathf.Cos(elevation);
                var tone=Color.Lerp((Color)color*.61f,Color.Lerp(color,Cream,.33f),Mathf.Clamp01(normal.y+.12f));
                Vertex(center+rotation*p,rotation*new Vector3(normal.x/aspect,normal.y/height,normal.z).normalized,tone,-2,0);
                if(ring==0)continue;
                int j=first+ring*sides+i,k=first+ring*sides+(i+1)%sides;
                Quad(j-sides,j,k,k-sides);
            }
        }
    }

    public void StemSegment(Vector3 a,Vector3 b,Vector3 facing,float r0,float r1)
    {
        var axis=(b-a).normalized;
        if(axis.sqrMagnitude<0.1f) return;
        var side=Vector3.Cross(axis,facing).normalized;
        var forward=Vector3.Cross(side,axis);
        int start=vertices.Count;
        for(int i=0;i<5;i++)
        {
            float angle=i*(2*Mathf.PI/5);
            var n=forward*Mathf.Cos(angle)+side*Mathf.Sin(angle);
            Vertex(a+n*r0,n,Stem,-2,0); Vertex(b+n*r1,n,Stem,-2,1);
        }
        for(int i=0;i<5;i++)
        {
            int x=start+2*i, y=start+2*((i+1)%5);
            Quad(x,y,y+1,x+1);
        }
    }

    public void Branch(Vector3 joint,Vector3 normal,int seed,int tier,float maturity)
    {
        float k=Mathf.SmoothStep(0,1,maturity);
        if(k<0.01f) return;
        var up=Vector3.ProjectOnPlane(Vector3.up,normal).normalized;
        var side=Vector3.Cross(up,normal)*(tier%2==0?1:-1);
        int branchSeed=WallFlowers.Hash(seed,tier,9);
        float reach=(0.125f+(branchSeed%20)*0.007f)*k;
        var fork=joint+(side*0.48f+up*0.62f+normal*0.18f)*reach;
        var tip=joint+(side+up*(0.60f+0.03f*(branchSeed%14))+normal*0.31f)*reach;
        StemSegment(joint,fork,normal,0.0048f*k,0.0034f*k);
        StemSegment(fork,tip,normal,0.0034f*k,0.0018f*k);
        for(int j=0;j<5;j++)
        {
            float t=(j+1)/6f;
            var at=t<0.5f?Vector3.Lerp(joint,fork,2*t):Vector3.Lerp(fork,tip,2*t-1);
            var leafAxis=(side*0.55f+up*(j%2==0?0.92f:-0.68f)+normal*0.48f).normalized;
            float length=(0.09f+0.029f*(j%3))*k;
            Leaf(at,leafAxis*length,Vector3.Cross(normal,leafAxis).normalized*length*0.26f,
                normal*length*0.14f,j%3==0?Fresh:LeafGreen);
        }
        if(tier%4==2)
        {
            var last=tip;
            float loop=reach*0.53f;
            for(int j=1;j<=15;j++)
            {
                float a=j*0.49f, radius=loop*(1f-j*0.051f);
                var curl=tip+side*Mathf.Sin(a)*radius+up*(loop-Mathf.Cos(a)*radius)+normal*(j*0.0007f*k);
                StemSegment(last,curl,normal,0.0032f*k,0.0018f*k);
                last=curl;
            }
        }
        if((tier+seed%3)%4==2 && maturity>0.35f)
        {
            float open=Mathf.SmoothStep(0,1,(maturity-0.35f)/0.65f);
            var facing=(normal*0.94f+up*0.33f+side*0.13f).normalized;
            Rosette(tip+facing*0.008f,facing,branchSeed,(0.10f+0.006f*(branchSeed%8))*open);
        }
        if((tier+(seed%4))%3==1 && maturity>0.28f)
        {
            float bloom=Mathf.SmoothStep(0,1,(maturity-0.28f)/0.72f);
            var pendant=tip-Vector3.up*((0.19f+(branchSeed%8)*0.009f)*bloom)+normal*(0.028f*bloom);
            StemSegment(tip,pendant,normal,0.0025f*bloom,0.0012f*bloom);
            for(int j=0;j<7;j++)
            {
                var offset=side*(j%2==0?1:-1)*(0.031f-0.004f*j)*bloom;
                var stalk=Vector3.Lerp(tip,pendant,(j+0.65f)/7f);
                var at=stalk+offset-Vector3.up*0.009f*bloom;
                StemSegment(stalk,at,normal,0.0016f*bloom,0.0012f*bloom);
                Bell(at,side,normal,(0.042f-j*0.0038f)*bloom,(seed%3==0)?Rose:Lilac);
            }
        }
    }
    public void Rosette(Vector3 center,Vector3 normal,int seed,float size)
    {
        var x=Vector3.Cross(Vector3.up,normal).normalized;
        var y=Vector3.Cross(normal,x);
        Color32 hue=seed%3==0?Cream:Peach;
        for(int petal=0;petal<16;petal++)
        {
            int ring=petal<7?0:petal<12?1:2;
            int count=ring==0?7:ring==1?5:4;
            float phase=(seed%31)*0.1f+petal*2*Mathf.PI/count+ring*0.48f;
            var dir=x*Mathf.Cos(phase)+y*Mathf.Sin(phase);
            var side=Vector3.Cross(normal,dir);
            int first=vertices.Count;
            for(int row=0;row<4;row++)
            for(int col=0;col<7;col++)
            {
                float t=row/3f,s=(col-3)/3f;
                float width=(0.075f+Mathf.Sin(t*1.9f)*0.76f)*s;
                float lip=0.22f*s*s+0.043f*Mathf.Cos(col*2.6f+petal);
                var offset=(dir*(t*(1-0.30f*s*s))+side*width)*(1-ring*0.255f)
                    +normal*(ring*0.12f+t*(0.10f+lip+ring*0.08f)+0.008f*(petal%count));
                var tone=Color.Lerp(Rose,hue,Mathf.Clamp01(t*2.3f));
                tone=Color.Lerp(tone,Cream,Mathf.Clamp01(t*t*0.64f+0.08f*Mathf.Sin(col*3)));
                // UV.x in [2,3] selects petal edges and fine radial veins.
                Vertex(center+offset*size,(normal-side*s*0.54f-dir*t*0.18f).normalized,tone,2.5f+0.5f*s,t);
                if(row>0 && col>0)
                {
                    int p=first+row*7+col;
                    Quad(p-8,p-7,p,p-1);
                }
            }
        }
        int hub=Vertex(center+normal*size*0.48f,normal,Honey,-2,0);
        for(int i=0;i<16;i++)
        {
            float a=i*Mathf.PI/8;
            var radial=x*Mathf.Cos(a)+y*Mathf.Sin(a);
            Vertex(center+(radial*0.14f+normal*0.38f)*size,(normal+radial*0.6f).normalized,Umber,-2,0);
        }
        for(int i=0;i<16;i++) Triangle(hub,hub+1+i,hub+1+(i+1)%16);
    }
    private void Bell(Vector3 crown,Vector3 side,Vector3 front,float radius, Color32 tint)
    {
        int start=vertices.Count;
        for(int i=0;i<10;i++)
        {
            float angle=i*(Mathf.PI/5);
            var radial=(side*Mathf.Cos(angle)+front*Mathf.Sin(angle)).normalized;
            Vertex(crown+radial*radius*0.13f,(radial+Vector3.up).normalized,tint,-2,0);
            Vertex(crown+radial*radius*0.53f-Vector3.up*radius*0.5f,radial,tint,-2,0);
            float lip=i%2==0?1f:0.67f;
            Vertex(crown+radial*radius*lip-Vector3.up*radius*(0.68f+lip*0.56f),
                (radial-Vector3.up*0.25f).normalized,Color.Lerp(tint,Color.white,0.34f),-2,0);
        }
        for(int i=0;i<10;i++)
        {
            int a=start+3*i, b=start+3*((i+1)%10);
            Quad(a,b,b+1,a+1); Quad(a+1,b+1,b+2,a+2);
        }
        Petal(crown-Vector3.up*(radius*0.93f),side*radius*0.17f,Vector3.down*radius*0.56f,front*radius*0.11f,Honey);
    }
    private void Petal(Vector3 basePoint,Vector3 halfWidth,Vector3 axis,Vector3 arch,Color32 tint)
        => Leaf(basePoint,axis,halfWidth,arch,tint);
    public void FallingPetal(Vector3 p,Quaternion spin,float size,int seed)
        => Petal(p,spin*Vector3.right*(size*0.46f),spin*Vector3.up*size,spin*Vector3.forward*(size*0.23f),seed%2==0?Rose:Lilac);
}
