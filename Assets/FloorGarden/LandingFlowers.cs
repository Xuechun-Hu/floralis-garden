using System.Collections.Generic;
using UnityEngine;

internal sealed class LandingFlowers
{
    private readonly Transform[] flowers;
    public LandingFlowers(PortalArt art,List<Vector3> sites,PortalSurfaces scan,float floorHeight)
    {
        // A wall-only scan still has the seed's estimated floor height.
        if(sites.Count==0)
        {
            for(int i=0;i<8;i++)
            {
                float a=i*2.39996f;
                var p=art.Window.TransformPoint(new Vector3(Mathf.Cos(a)*.61f,0,-.78f+.4f*Mathf.Sin(a)));
                if(scan.TryLanding(p+Vector3.up,out var hit,out _,out _))sites.Add(hit);
            }
        }
        if(sites.Count==0)
        {
            var foot=art.Window.position;foot.y=floorHeight;sites.Add(foot);
        }
        flowers=new Transform[sites.Count];
        var plants=new GardenArt();var ink=art.Pigment();
        var cluster=PortalArt.Child("Flowers from fallen gold",art.Window.parent);
        for(int i=0;i<sites.Count;i++)
        {
            plants.Clear();float a=i*2.39996f;
            var facing=new Vector3(Mathf.Cos(a),.9f,Mathf.Sin(a)).normalized;
            var tip=new Vector3(.037f*facing.x,.18f+.025f*(i%4),.037f*facing.z);
            int seed=WallFlowers.Hash(8,i,3);
            plants.StemSegment(Vector3.zero,tip,Vector3.forward,.005f,.003f);
            plants.Branch(tip*.38f,facing,seed,2,1);
            plants.Rosette(tip,facing,seed,.059f+.008f*(i%3));
            // A taller main flower with two lower blooms sharing the same grounded stem.
            for(int side=-1;side<=1;side+=2)
            {
                float b=a+side*1.19f;
                var axis=new Vector3(Mathf.Sin(b),0,Mathf.Cos(b));
                var top=axis*(.082f+.012f*(i%3))+Vector3.up*(side<0?.126f:.172f);
                var joint=tip*(side<0?.24f:.36f);
                plants.StemSegment(joint,top,Vector3.forward,.0038f,.0022f);
                plants.Rosette(top,(axis+.96f*Vector3.up).normalized,
                    WallFlowers.Hash(i,side+2,17),side<0?.041f:.052f);
            }
            var mesh=art.Own(new Mesh{name="Gold-seeded flower"});plants.Upload(mesh);
            flowers[i]=PortalArt.Show("Landing bloom",cluster,mesh,ink);
            flowers[i].position=sites[i];flowers[i].localScale=Vector3.zero;
        }
    }
    public void Grow(float age)
    {
        for(int i=0;i<flowers.Length;i++)
        {
            float t=Mathf.SmoothStep(0,1,(age-2.5f-i*.060f)/1.03f);
            flowers[i].localScale=new Vector3(t,t*t,t);
        }
    }
}
