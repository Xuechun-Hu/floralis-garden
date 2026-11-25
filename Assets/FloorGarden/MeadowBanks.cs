using UnityEngine;

// This small CPU copy of the painted stream mask uses the same crop as PortalStream.shader.
internal sealed class MeadowBanks
{
    private const int Width=1024, Height=144;
    private readonly byte[] water=Resources.Load<TextAsset>("MeadowWaterFootprint").bytes;
    private readonly float eyeHeight;
    public MeadowBanks(float eyeHeight) => this.eyeHeight=eyeHeight;

    private bool Wet(Vector3 p)
    {
        float u=Mathf.Atan2(p.x,p.z)/(2*Mathf.PI)+.5f;
        float fromTop=.5f+Mathf.Atan2(eyeHeight,new Vector2(p.x,p.z).magnitude)/Mathf.PI;
        int x=Mathf.FloorToInt(Mathf.Repeat(u,1)*Width);
        int y=Mathf.FloorToInt((fromTop-.46875f)*Height/.28125f);
        return x>=0 && x<Width && y>=0 && y<Height && water[y*Width+x]!=0;
    }

    public bool Dry(Vector3 point,float radius)
    {
        if(Wet(point))return false;
        for(int i=0;i<8;i++)
        {
            float a=i*Mathf.PI/4;
            if(Wet(point+new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius)))return false;
        }
        return true;
    }

    // UVs use a top-left origin. ZW is the outward image normal at each painted bank.
    private static readonly Vector4[] Shore =
    {
        new Vector4(0.070679f,0.546631f,-0.0960f,-0.9954f),
        new Vector4(0.128784f,0.643799f,-0.2699f,0.9629f),
        new Vector4(0.269653f,0.605713f,0.0154f,-0.9999f),
        new Vector4(0.298706f,0.704346f,0.1497f,0.9887f),
        new Vector4(0.237915f,0.708252f,-0.0588f,-0.9983f),
        new Vector4(0.494263f,0.531494f,0.3382f,0.9411f),
        new Vector4(0.613403f,0.560303f,-0.3355f,0.9420f),
        new Vector4(0.642700f,0.566650f,-0.0278f,-0.9996f),
        new Vector4(0.699341f,0.572510f,0.0934f,-0.9956f),
        new Vector4(0.743042f,0.559326f,0.3858f,-0.9226f),
        new Vector4(0.800903f,0.527100f,0.3067f,-0.9518f),
        new Vector4(0.858521f,0.539307f,0.1869f,-0.9824f),
        new Vector4(0.923218f,0.554443f,-0.1826f,-0.9832f),
    };
    public static readonly int[] PlantSites={0,2,4,6,8,9,10,11,12};

    private Vector3 FloorPoint(float u,float v)
    {
        float lon=(u-.5f)*2*Mathf.PI;
        float distance=eyeHeight/Mathf.Tan((v-.5f)*Mathf.PI);
        return new Vector3(Mathf.Sin(lon)*distance,0,Mathf.Cos(lon)*distance);
    }
    private Vector3 PaintedEdge(Vector4 s,out Vector3 inland)
    {
        var edge=FloorPoint(s.x,s.y);
        // Project the shoreline tangent first: panorama pixels span very different
        // distances along and across the view at grazing angles.
        var tangent=FloorPoint(s.x-s.w/4096,s.y+s.z/2048)-edge;
        inland=Vector3.Cross(Vector3.up,tangent).normalized;
        var drySide=FloorPoint(s.x+s.z/4096,s.y+s.w/2048)-edge;
        if(Vector3.Dot(inland,drySide)<0)inland=-inland;
        return edge;
    }

    public Vector3 Bank(int index,out Vector3 inland)
    {
        var edge=PaintedEdge(Shore[index],out inland);
        float stride=Mathf.Clamp(edge.magnitude*.016f,.04f,.28f);
        for(int n=0;n<12 && !Dry(edge,.055f);n++)edge+=inland*stride;
        return edge;
    }
    public void AddStones(GardenArt art)
    {
        // Place the larger rocks on the bank, and the small companions at the
        // painted waterline. Bank() includes extra dry clearance for foliage.
        for(int group=0;group<Shore.Length;group++)
            RockGroup(art,Shore[group],group);

        // Two prominent bends of the middle stream face the garden entrance.
        RockGroup(art,new Vector4(0.506470f,0.549072f,-0.0499f,0.9988f),Shore.Length);
        RockGroup(art,new Vector4(0.547241f,0.552002f,-0.1073f,0.9942f),Shore.Length+1);
    }

    private void RockGroup(GardenArt art,Vector4 shore,int group)
    {
        var edge=PaintedEdge(shore,out var inland);
        var along=Vector3.Cross(Vector3.up,inland)*(group%2==0?1:-1);
        float size=Mathf.Clamp(edge.magnitude*.052f,.17f,.60f);
        var slate=new Color32(108,123,124,255);
        var grey=new Color32(158,153,140,255);
        var sand=new Color32(194,181,151,255);
        var anchor=edge+inland*(size*.84f);
        art.Pebble(anchor,size,group*53,group%3==0?grey:slate);
        art.Pebble(edge+along*(size*1.45f)+inland*(size*.44f),size*.66f,group*71+27,sand);
        art.Pebble(edge-along*(size*1.48f)-inland*(size*.16f),size*.49f,group*37+81,grey);
        if(group%3==1)
            art.Pebble(edge-along*(size*2.6f),size*.32f,group*89,slate);
    }
}
