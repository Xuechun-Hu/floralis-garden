using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Geometry is local to the window: +X right, +Y up, -Z into the room.
internal sealed class PortalArt
{
    private readonly List<Object> assets = new();
    public Transform Window { get; private set; }
    public Transform Angel { get; private set; }
    private GaussianAngel goddess;
    private Transform arch, figureLayer;
    private PortalBloom bloom;
    private PortalGuide guide;
    private PortalFrame frame;
    public ButterflyDust Dust {get;private set;}
    private LandingFlowers landingFlowers;
    private readonly List<Material> openingTiles=new();
    private Material socketInk;
    private Transform socket;
    private Vector3 figureOffset=PortalGesture.StartingOffset;
    private PortalComets comets;
    private float manualYaw, ambientYaw;
    private bool handling, crossingStarted;
    private AudioSource chimes;
    private AudioClip openingChime,crossing;
    public Material MaterialFor(string resource)
    {
        var material=new Material(Resources.Load<Shader>(resource));assets.Add(material);return material;
    }

    public Mesh Own(Mesh mesh) { assets.Add(mesh); return mesh; }
    public Material Paint(string texture,Color color)
    {
        var m=new Material(Resources.Load<Shader>("PortalIllustration"));
        if(texture!=null) m.SetTexture("_MainTex",Resources.Load<Texture2D>(texture));
        m.SetColor("_Tint",color);assets.Add(m);return m;
    }
    public Material MeadowTile(int column,int rowFromBottom)
    {
        var m=MaterialFor("PortalStream");
        var tile=Resources.Load<Texture2D>($"GardenSphere/row{1-rowFromBottom}-col{column}");
        m.SetTexture("_MainTex",tile);
        if(rowFromBottom==1)
            m.SetTexture("_CloudInterior",Resources.Load<Texture2D>("FloraCloudInterior"));
        m.SetTexture("_Flow",Resources.Load<Texture2D>("PortalWaterFlow"));
        m.SetVector("_Region",new Vector4(.25f,.5f,.25f*column,.5f*rowFromBottom));
        return m;
    }
    public Material ButterflyInk(Color tint)
    {
        var m=Paint(null,tint);
        m.SetFloat("_WingPattern",1);
        return m;
    }
    public Material Pigment()
    {
        var m=new Material(Resources.Load<Shader>("GardenInk")); assets.Add(m);return m;
    }
    public static Transform Child(string name, Transform parent)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;
    }
    public Mesh SurfaceMesh(string name,int columns,int rows,System.Func<float,float,Vector3> position,
        System.Func<float,float,Vector2> texcoords=null)
    {
        var v=new Vector3[(columns+1)*(rows+1)]; var uv=new Vector2[v.Length];
        var tris=new List<int>(columns*rows*6);
        for(int row=0;row<=rows;row++) for(int col=0;col<=columns;col++)
        {
            int i=row*(columns+1)+col;float u=(float)col/columns,h=(float)row/rows;
            v[i]=position(u,h);uv[i]=texcoords==null?new Vector2(u,h):texcoords(u,h);
            if(row==0 || col==0)continue;
            int a=i-columns-2;
            tris.Add(a);tris.Add(i-1);tris.Add(i);tris.Add(a);tris.Add(i);tris.Add(i-columns-1);
        }
        var mesh=Own(new Mesh{name=name});mesh.vertices=v;mesh.uv=uv;mesh.SetTriangles(tris,0);return mesh;
    }
    public static Transform Show(string name, Transform parent, Mesh mesh, Material material)
    {
        var t=Child(name,parent);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;
        return t;
    }

    public bool Ready {get;private set;}
    public System.Collections.IEnumerator Prepare(Transform parent,PortalSurfaces surfaces)
    {
        Window=Child("Lily window",parent);
        Window.gameObject.SetActive(false);
        var sharp=Resources.LoadAsync<TextAsset>("GardenAngelSharp");
        foreach(string name in new[]{"GardenAngelDetail1512","LilyWindow","AngelSocket","PortalWaterFlow","FloraPlaque","PortalGuideCaptions"})
            yield return Resources.LoadAsync<Texture2D>(name);
        yield return Resources.LoadAsync<Texture2D>("FloraCloudInterior");
        arch=Child("Portal arch and threshold",Window);
        for(int row=0;row<2;row++)
        for(int side=0;side<2;side++)
        {
            yield return Resources.LoadAsync<Texture2D>($"GardenSphere/row{1-row}-col{1+side}");
            var opening=SurfaceMesh("Arched meadow opening",20,12,(su,sv)=>{
                float u=(side+su)*.5f,v=(row+sv)*.5f;
                float top=.29f+.48f*Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow(2*u-1,2)));
                return new Vector3((u-.5f)*1.04f,Mathf.Lerp(-.79f,top,v),.07f);
            },(su,sv)=>new Vector2((side==0?.50f:0)+.5f*su,(row==0?.50f:0)+.50f*sv));
            var tile=MeadowTile(1+side,row);
            openingTiles.Add(tile);
            Show("View through the wall",arch,opening,tile);
        }
        Dust=Child("Butterfly gold on the floor",parent).gameObject.AddComponent<ButterflyDust>();
        Dust.Initialize(this,surfaces,0);
        yield return null;
        frame=new PortalFrame(this,Window);
        yield return null;
        guide=new PortalGuide(this,Window);
        socketInk=MaterialFor("AngelSocketInk");
        socketInk.SetTexture("_MainTex",Resources.Load<Texture2D>("AngelSocket"));
        var socketMesh=SurfaceMesh("Figure socket",1,1,(u,v)=>new Vector3((u-.5f)*1.3f,(v-.5f)*1.7f,-.03f));
        socket=Show("Gilded figure outline",arch,socketMesh,socketInk);
        figureLayer=Child("Figure and particle trails",Window);
        goddess=new GaussianAngel(this,figureLayer);
        Angel=goddess.Root;
        yield return sharp;
        yield return goddess.Prepare((TextAsset)sharp.asset);
        bloom=new PortalBloom(this,arch);
        comets=new PortalComets(this,figureLayer);
        chimes=Child("Window chimes",parent).gameObject.AddComponent<AudioSource>();
        var openingAudio=Resources.LoadAsync<AudioClip>("GardenAudio/LilyOpening");
        yield return openingAudio;
        openingChime=(AudioClip)openingAudio.asset;
        openingChime.LoadAudioData();
        yield return Resources.LoadAsync<AudioClip>("GardenAudio/PetalCrossing");
        chimes.playOnAwake=false;chimes.spatialBlend=.48f;chimes.minDistance=1.5f;
        chimes.dopplerLevel=0;
        crossing=Resources.Load<AudioClip>("GardenAudio/PetalCrossing");
        Ready=true;
    }
    public void Open(Vector3 center,Vector3 outward,float floorY)
    {
        Dust.SetFloorHeight(floorY);
        Window.SetPositionAndRotation(center,Quaternion.LookRotation(-outward,Vector3.up));
        Window.gameObject.SetActive(true);
        chimes.transform.position=center;
        chimes.PlayOneShot(openingChime,.50f);
    }
    public void Greet(Vector3 eye,float elapsed)
    {
        Window.localScale=Vector3.one*Mathf.Lerp(.045f,1,Mathf.SmoothStep(0,1,elapsed/1.05f));
        Angel.localPosition = new Vector3(0, -.1f, .18f);
        // Move the captured figure and its dissolve forward together as the frame opens.
        float emerge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,1.15f,elapsed));
        figureLayer.localPosition=figureOffset*emerge;
        float idle=Mathf.Lerp(-8f,0,Mathf.SmoothStep(0,1,elapsed/2.5f))
            +5.5f*Mathf.Sin(elapsed*.57f)*emerge;
        ambientYaw=crossingStarted?0:Mathf.Lerp(ambientYaw,handling?0:idle,1-Mathf.Exp(-7*Time.unscaledDeltaTime));
        Angel.localRotation=Quaternion.Euler(0,manualYaw+ambientYaw,0);
        if(elapsed>.75f)goddess.Sort(Angel.InverseTransformDirection((Angel.position-eye).normalized));
        bloom.Tick(elapsed);
    }
    public void PositionPortrait(PortalGesture gesture)
    {
        figureOffset=gesture.Offset;
        manualYaw=gesture.Angle;
        handling=gesture.Inspecting || gesture.Placing;
        socketInk.SetFloat("_Ready",gesture.Aligned?1:0);
    }
    public void GuidePlayer(Transform eye, PortalGesture gesture, bool controller, float age)
    {
        if(age>.7f)guide.Tick(eye,gesture,controller,age,Time.unscaledDeltaTime);
    }
    public void BeginCrossing(PortalSurfaces surfaces,float floorY)
    {
        crossingStarted=true;
        guide.Hide();
        landingFlowers=new LandingFlowers(this,frame.Begin(surfaces),surfaces,floorY);
        socket.gameObject.SetActive(false);
        chimes.PlayOneShot(crossing,.64f);
    }
    public void GrowLandingFlowers(float age)=>landingFlowers.Grow(age);
    public void Scatter(float age)
    {
        goddess.Scatter(age*.53f);
        comets.Tick(age*.50f);
        frame.Tick(age);
        float progress=Mathf.InverseLerp(1.05f,3.04f,age);
        foreach(var tile in openingTiles)tile.SetFloat("_OpeningBreak",progress);
        bloom.Disperse(progress);
    }
    public void FinishCrossing()
    {
        Window.gameObject.SetActive(false);
        // The 300k-splat mesh and its CPU sort buffers are only needed during the portal sequence.
        var mesh=Angel.GetComponent<MeshFilter>().sharedMesh;
        assets.Remove(mesh);
        Object.Destroy(mesh);
        goddess=null;
    }
    public void Dispose()
    {
        foreach(var a in assets) if(a!=null)Object.Destroy(a);
        assets.Clear();
    }
}
