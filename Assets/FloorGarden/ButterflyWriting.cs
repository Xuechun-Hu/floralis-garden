using System;
using System.Collections.Generic;
using UnityEngine;

// The same timed pen path drives the insect and reveals the deposited grains.
internal sealed class ButterflyWriting
{
    private sealed class Phrase
    {
        public Transform panel;
        public Material ink;
        public readonly List<Vector3> pen = new();
        public readonly List<float> times = new();
        public float end;
        public Vector3 Sample(float progress)
        {
            float t=Mathf.Clamp01(progress)*end;
            int i=times.BinarySearch(t);
            if(i>=0)return pen[i];
            i=~i;
            return Vector3.Lerp(pen[i-1],pen[i],Mathf.InverseLerp(times[i-1],times[i],t));
        }
    }
    private readonly Phrase welcome;
    private readonly Transform window;
    public ButterflyWriting(PortalArt art,Transform plaque,Transform window,float width)
    {
        this.window=window;
        var strokes=new Dictionary<char,string[]>();
        foreach(var line in Resources.Load<TextAsset>("FloraWriting").text.Split('\n'))
            if(line.Length>2)strokes[line[0]]=line.Substring(2).Trim().Split(new[]{" / "},StringSplitOptions.None);
        welcome=Build(art,plaque,strokes,"走近芙洛拉",0,width);
    }

    private static Phrase Build(PortalArt art,Transform plaque,Dictionary<char,string[]> data,string text,float y,float width)
    {
        var p=new Phrase { ink=art.MaterialFor("ButterflyGold") };
        p.ink.SetFloat("_LetterInk",1);
        p.ink.renderQueue=3101;
        var verts=new List<Vector3>();var uv=new List<Vector2>();var times=new List<Vector2>();var tris=new List<int>();
        var rng=new System.Random(91);
        const float em=.20f;
        for(int col=0;col<text.Length;col++)
        {
            foreach(string stroke in data[text[col]])
            {
                bool lift=true;
                foreach(string pair in stroke.Split(' '))
                {
                    var xy=pair.Split(',');
                    var point=new Vector3((col-text.Length*.5f+int.Parse(xy[0])/1024f)*em,
                        (int.Parse(xy[1])-380)/1024f*em,0);
                    var previous=p.pen.Count>0?p.pen[p.pen.Count-1]:point;
                    float distance=Vector3.Distance(previous,point);
                    if(distance<.00001f && p.pen.Count>0)continue;
                    float start=p.end;
                    p.end+=distance*(lift?.52f:1f);
                    p.pen.Add(point);p.times.Add(p.end);
                    if(!lift)
                    {
                        int grains=Mathf.Max(1,Mathf.CeilToInt(distance/.0036f));
                        for(int j=1;j<=grains;j++)
                        {
                            float f=(float)j/grains;
                            var c=Vector3.Lerp(previous,point,f);
                            float radius=.0063f+(float)rng.NextDouble()*.0018f;
                            int k=verts.Count;
                            var stamp=new Vector2(Mathf.Lerp(start,p.end,f),(float)rng.NextDouble());
                            for(int corner=0;corner<4;corner++)
                            {
                                var st=new Vector2(corner&1,corner>>1);
                                verts.Add(c+(Vector3)((2*st-Vector2.one)*radius));
                                uv.Add(st);times.Add(stamp);
                            }
                            tris.Add(k);tris.Add(k+2);tris.Add(k+1);tris.Add(k+1);tris.Add(k+2);tris.Add(k+3);
                        }
                    }
                    lift=false;
                }
            }
        }
        var mesh=art.Own(new Mesh{name=text});
        mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetUVs(1,times);mesh.SetTriangles(tris,0);
        p.panel=PortalArt.Show(mesh.name,plaque,mesh,p.ink);
        p.panel.localPosition=new Vector3(0,y*width,-.003f);
        p.panel.localScale=Vector3.one*width;
        p.panel.gameObject.SetActive(false);
        return p;
    }

    public Vector3 Welcome(float progress,bool visible)
    {
        var line=welcome;
        line.panel.gameObject.SetActive(visible);
        if(!visible)return default;
        line.ink.SetFloat("_Written",progress<0?-1:Mathf.Clamp01(progress)*line.end);
        var tip=line.panel.TransformPoint(line.Sample(progress)+Vector3.back*.012f);
        return window.InverseTransformPoint(tip);
    }
}
