using UnityEngine;

// Small foreground details arrive in sequence as the window opens.
internal sealed class PortalBloom
{
    private readonly Transform[] flowers=new Transform[5], flies=new Transform[3], wings=new Transform[6];
    private readonly Material glow;
    public PortalBloom(PortalArt art,Transform parent)
    {
        glow=art.MaterialFor("PortalLight");
        var halo=art.SurfaceMesh("Diffuse arch light",1,1,(u,v)=>new Vector3((u-.5f)*1.61f,(v-.5f)*1.88f,-.023f));
        PortalArt.Show("Light through arch",parent,halo,glow);
        var drawing=new GardenArt();
        var petals=art.Pigment();
        for(int i=0;i<flowers.Length;i++)
        {
            drawing.Clear();
            drawing.Rosette(Vector3.zero,Vector3.back,i*31+9,.056f+i%2*.014f);
            var mesh=art.Own(new Mesh{name="Portal flower"});drawing.Upload(mesh);
            var flower=flowers[i]=PortalArt.Show("Unfolding flower",parent,mesh,petals);
            flower.localPosition=new Vector3((i%2==0?-1:1)*(.48f+.036f*Mathf.Sin(i)),i*.24f-.55f,-.06f);
            flower.localScale=Vector3.zero;
        }
        var wing=art.SurfaceMesh("Portal butterfly wing",10,7,(u,v)=>{
            float chord=.053f*Mathf.Sin(Mathf.PI*(v*.85f+.06f))*(1+.20f*Mathf.Cos(u*2*Mathf.PI));
            return new Vector3(.049f*v,.003f*Mathf.Sin(v*Mathf.PI),chord*(u-.38f)+.007f*v);
        });
        var amber=art.ButterflyInk(new Color(.99f,.71f,.32f));
        for(int i=0;i<flies.Length;i++)
        {
            flies[i]=PortalArt.Child("Window butterfly",parent);
            for(int j=0;j<2;j++)
            {
                var w=wings[2*i+j]=PortalArt.Show("Wing",flies[i],wing,amber);
                w.localScale=new Vector3(j==0?-1:1,1,1);
            }
        }
    }
    public void Disperse(float progress)
    {
        for(int i=0;i<flowers.Length;i++)
            flowers[i].localScale=Vector3.one*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(i*.1f,.54f+i*.1f,progress)));
        foreach(var fly in flies)fly.localScale=Vector3.one*(1-Mathf.SmoothStep(0,1,progress));
        glow.SetFloat("_Glow",.85f*(1-progress));
    }

    public void Tick(float age)
    {
        glow.SetFloat("_Glow",Mathf.SmoothStep(0,.85f,age/1.5f)*(.87f+.13f*Mathf.Sin(age*2)));
        for(int i=0;i<flowers.Length;i++) flowers[i].localScale=Vector3.one*Mathf.SmoothStep(0,1,(age-.3f-.24f*i)/1.05f);
        for(int i=0;i<flies.Length;i++)
        {
            float t=age*.49f+i*2.08f;
            float emerge=Mathf.SmoothStep(0,1,(age-1.1f-i*.45f)/1.3f);
            flies[i].localScale=Vector3.one*emerge;
            var flower=flowers[i+1].localPosition;
            flies[i].localPosition=flower+new Vector3(.045f*Mathf.Cos(t),.046f+.028f*Mathf.Sin(t),-.034f-.017f*Mathf.Cos(t*.78f));
            flies[i].localRotation=Quaternion.Euler(26,24*Mathf.Sin(t),6*Mathf.Cos(t));
            float flap=38+29*Mathf.Sin(age*15+i);
            wings[2*i].localRotation=Quaternion.Euler(0,0,-flap);
            wings[2*i+1].localRotation=Quaternion.Euler(0,0,flap);
        }
    }
}
