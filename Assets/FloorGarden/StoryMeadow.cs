using UnityEngine;
using UnityEngine.Rendering;

internal sealed class StoryMeadow
{
    private Transform root;
    private readonly System.Collections.Generic.List<Material> panoramaPaint=new(8);
    public bool Ready {get;private set;}
    private readonly Transform[] beds=new Transform[14];
    private const int VineSteps=24;
    private readonly Mesh[] bedMeshes=new Mesh[14];
    private readonly int[,] bedIndexCounts=new int[14,VineSteps+1];
    private readonly int[] visibleVineSteps=new int[14];
    private Transform riverStones;
    private readonly Transform[] butterflies=new Transform[12], left=new Transform[12], right=new Transform[12];
    private readonly ButterflyPerch[] visitors = { new ButterflyPerch(), new ButterflyPerch() };
    private readonly Transform[] fingertipLights = new Transform[2];
    private float born,nextGrain;
    private int pollenVisitor;
    private ButterflyDust dust;
    private AudioSource stream;
    public static System.Collections.IEnumerator WarmPanorama()
    {
        for(int row=0;row<2;row++)
        for(int col=0;col<4;col++)
            yield return Resources.LoadAsync<Texture2D>($"GardenSphere/row{row}-col{col}");
    }
    public System.Collections.IEnumerator Prepare(PortalArt art,Transform parent,Vector3 center,Vector3 forward,float eyeHeight)
    {
        dust=art.Dust;
        root=PortalArt.Child("Beyond the lily window",parent);
        root.gameObject.SetActive(false);
        root.SetPositionAndRotation(center,Quaternion.LookRotation(forward,Vector3.up));
        yield return WarmPanorama();
        var strokes=Resources.LoadAsync<Texture2D>("MeadowGrassStrokes");
        yield return strokes;
        for(int row=0;row<2;row++)
        for(int col=0;col<4;col++)
        {
            bool floor=row==0;
            var mesh=art.SurfaceMesh(floor?"Grass floor sector":"Distant hemisphere sector",72,48,(su,sv)=>{
                float lon=((col+su)*.25f-.5f)*2*Mathf.PI;
                float lat=((row+sv)*.5f-.5f)*Mathf.PI;
                var bearing=new Vector3(Mathf.Sin(lon),0,Mathf.Cos(lon));
                if(floor)
                {
                    // The last ring supplies a horizontal direction at infinity in the shader.
                    float radius=sv>=1?2048f:Mathf.Max(0,eyeHeight*Mathf.Cos(lat)/-Mathf.Sin(lat));
                    return bearing*radius;
                }
                return 32f*(bearing*Mathf.Cos(lat)+Vector3.up*Mathf.Sin(lat));
            });
            mesh.bounds=new Bounds(Vector3.zero,Vector3.one*4096f);
            var paint=art.MeadowTile(col,row);
            paint.EnableKeyword("GARDEN_SURROUND");
            paint.SetFloat("_GroundEyeHeight",eyeHeight);
            paint.SetFloat("_FloorSector",floor?1:0);
            paint.SetTexture("_GrassStrokes",strokes.asset as Texture2D);
            paint.renderQueue=(int)RenderQueue.Background;
            panoramaPaint.Add(paint);
            var tile=PortalArt.Show(floor?"Grass floor":"Panorama sky",root,mesh,paint);
            tile.GetComponent<MeshRenderer>().allowOcclusionWhenDynamic=false;
            yield return null;
        }

        var pollenMesh=art.SurfaceMesh("Finger pollen",12,8,(u,v)=>{
            float a=u*2*Mathf.PI, b=(v-.5f)*Mathf.PI;
            return new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Sin(b),Mathf.Cos(a)*Mathf.Cos(b))*.008f;
        });
        var pollen=art.Paint(null,new Color(1,.81f,.42f));
        for(int i=0;i<fingertipLights.Length;i++)
        {
            fingertipLights[i]=PortalArt.Show("Fingertip",root,pollenMesh,pollen);
            fingertipLights[i].gameObject.SetActive(false);
        }
        var plants=art.Pigment();
        var plantArt=new GardenArt();
        var banks=new MeadowBanks(eyeHeight);
        banks.AddStones(plantArt);
        var stoneMesh=art.Own(new Mesh{name="Stream bank pebbles"});
        plantArt.Upload(stoneMesh);
        riverStones=PortalArt.Show("River stones",root,stoneMesh,plants);
        var rng=new System.Random(7919);
        float Rand()=>(float)rng.NextDouble();
        for(int i=0;i<beds.Length;i++)
        {
            float bearing=i*2.39996f;
            float range=i<5?1.15f+i*.19f:2.35f+(i%4)*.54f;
            var location=new Vector3(Mathf.Sin(bearing)*range,0,Mathf.Cos(bearing)*range);
            if(i>=5)
            {
                int bank=MeadowBanks.PlantSites[i-5];
                location=banks.Bank(bank,out var inland)+inland*.43f;
                bearing=Mathf.Atan2(inland.x,inland.z);
            }
            var rotation=Quaternion.Euler(0,bearing*Mathf.Rad2Deg,0);
            plantArt.Clear();
            for(int j=0;j<30;j++)
            {
                float a=Rand()*2*Mathf.PI,r=Mathf.Sqrt(Rand());
                var p=new Vector3(.90f*r*Mathf.Cos(a),0,.66f*r*Mathf.Sin(a));
                var key=new Vector2Int(i*71+j,13+i);
                if(!banks.Dry(location+rotation*p,.14f))continue;
                if(j%4==1)plantArt.Sedge(p,a);
                else plantArt.Tuft(p,key);
                if(j%3==0 && (location+rotation*p).sqrMagnitude>2.2f*2.2f)
                {
                    int seed=WallFlowers.Hash(i,j,7);
                    float height=.16f+.29f*Rand();
                    var tip=p+new Vector3(.05f*Mathf.Sin(a),height,.05f*Mathf.Cos(a));
                    var facing=new Vector3(Mathf.Sin(a),.8f,Mathf.Cos(a)).normalized;
                    plantArt.StemSegment(p,tip,Vector3.forward,.006f,.003f);
                    if (j%6==0) plantArt.Branch(Vector3.Lerp(p,tip,.37f),facing,seed,2,1);
                    plantArt.Rosette(tip,facing,seed,.055f+.035f*Rand());
                }
            }
            bedIndexCounts[i,0]=plantArt.IndexCount;
            // Append the runner in growth order. Each new section and its leaf can
            // be revealed by extending the existing flower bed's index range.
            var last=new Vector3(-.47f,.007f,-.12f);
            bool growing=banks.Dry(location+rotation*last,.10f);
            for(int k=1;k<=VineSteps;k++)
            {
                float t=(float)k/VineSteps;
                var tip=new Vector3(last.x,.007f+.018f*t*t,last.z);
                tip.x=-.47f+.94f*t;
                tip.z=-.12f+.17f*Mathf.Sin(t*Mathf.PI*2)+.065f*Mathf.Sin(t*Mathf.PI*4);
                growing=growing && banks.Dry(location+rotation*tip,.11f);
                if(growing)plantArt.VineSegment(last,tip,k,VineSteps);
                last=tip;
                bedIndexCounts[i,k]=plantArt.IndexCount;
            }
            var mesh=art.Own(new Mesh{name="Flowering meadow patch",indexFormat=IndexFormat.UInt32});
            plantArt.Upload(mesh);
            bedMeshes[i]=mesh;
            visibleVineSteps[i]=-1;
            beds[i]=PortalArt.Show("Meadow in bloom",root,mesh,plants);
            beds[i].localPosition=location;beds[i].localRotation=Quaternion.Euler(0,bearing*Mathf.Rad2Deg,0);
            beds[i].localScale=Vector3.zero;
            yield return null;
        }
        // A pair of lobed wings shares a hinge. The ink pattern is evaluated in wing UV space.
        var wing=art.SurfaceMesh("Scalloped butterfly wing",12,6,(u,v)=>{
            float span=.06f*Mathf.Pow(v,.85f);
            float chord=(.012f+.042f*Mathf.Sin(v*Mathf.PI*.86f))*(1+.23f*Mathf.Cos(u*2*Mathf.PI));
            return new Vector3(span,.006f*Mathf.Sin(v*Mathf.PI), (u-.42f)*chord+.016f*v*Mathf.Sin((u-.4f)*Mathf.PI));
        });
        var amber=art.ButterflyInk(new Color(.99f,.64f,.28f));
        var blue=art.ButterflyInk(new Color(.44f,.76f,.85f));
        for(int i=0;i<butterflies.Length;i++)
        {
            butterflies[i]=PortalArt.Child("Butterfly",root);
            left[i]=PortalArt.Show("Left wing",butterflies[i],wing,i%3==0?blue:amber);
            left[i].localScale=new Vector3(-1,1,1);
            right[i]=PortalArt.Show("Right wing",butterflies[i],wing,i%3==0?blue:amber);
        }
        var river=PortalArt.Child("Brook at the near bend",root);
        river.localPosition=banks.Bank(3,out _)+Vector3.up*.05f;
        stream=river.gameObject.AddComponent<AudioSource>();
        var brook=Resources.LoadAsync<AudioClip>("GardenAudio/PaintedBrook");
        yield return brook;
        stream.clip=(AudioClip)brook.asset;
        stream.playOnAwake=false;stream.loop=true;stream.spatialBlend=.88f;
        stream.dopplerLevel=0;stream.minDistance=1.8f;stream.maxDistance=15;stream.volume=0;
        SetOpacity(0);
        Ready=true;
    }
    public void SetOpacity(float value)
    {
        foreach(var material in panoramaPaint)material.SetFloat("_MeadowOpacity",value);
    }
    public void Enter(float now) {dust.UseGardenFloor();root.gameObject.SetActive(true);born=now;stream.Play();Tick(now);}
    public void Tick(float now, Vector3? leftTip = null, Vector3? rightTip = null)
    {
        float age=now-born;
        riverStones.localScale=new Vector3(1,Mathf.SmoothStep(0,1,age/.65f),1);
        stream.volume=.54f*Mathf.SmoothStep(0,1,age/1.4f);
        for(int i=0;i<beds.Length;i++)
        {
            float t=Mathf.SmoothStep(0,1,(age-.12f*i)/1.65f);
            beds[i].localScale=new Vector3(1,t,1);
            int step=Mathf.Clamp(Mathf.FloorToInt((age-.12f*i)*VineSteps/2.8f),0,VineSteps);
            if(visibleVineSteps[i]!=step)
            {
                visibleVineSteps[i]=step;
                var mesh=bedMeshes[i];
                var sub=mesh.GetSubMesh(0);
                sub.indexCount=bedIndexCounts[i,step];
                mesh.SetSubMesh(0,sub,MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);
            }
        }
        for(int i=0;i<butterflies.Length;i++)
        {
            float phase=i*2.39996f, t=age*(.38f+i%3*.06f)+phase;
            float r=.85f+i%5*.42f;
            var point=new Vector3(r*Mathf.Cos(t),.79f+.46f*Mathf.Sin(1.7f*t+phase),r*Mathf.Sin(t)+.32f*Mathf.Sin(2.2f*t));
            var dir=new Vector3(-r*Mathf.Sin(t),.78f*Mathf.Cos(1.7f*t+phase),r*Mathf.Cos(t)+.70f*Mathf.Cos(2.2f*t));
            float flap=20+48*Mathf.Sin(age*(18+i%4)+phase);
            if(i<visitors.Length)
            {
                var worldTip=i==0?leftTip:rightTip;
                Vector3? localTip=worldTip.HasValue?root.InverseTransformPoint(worldTip.Value):null;
                var light=fingertipLights[i];
                if(light.gameObject.activeSelf!=localTip.HasValue) light.gameObject.SetActive(localTip.HasValue);
                if(localTip.HasValue) light.localPosition=localTip.Value;
                visitors[i].Animate(localTip,Time.unscaledDeltaTime,ref point,ref dir,ref flap);
            }
            butterflies[i].localPosition=point;
            butterflies[i].localRotation=Quaternion.LookRotation(dir,Vector3.up)*Quaternion.Euler(0,0,13*Mathf.Sin(t*2));
            butterflies[i].localScale=Vector3.one*Mathf.SmoothStep(0,1,(age-.065f*i)/.8f);
            left[i].localRotation=Quaternion.Euler(0,0,-flap);right[i].localRotation=Quaternion.Euler(0,0,flap);
        }
        if(age>.7f && now>=nextGrain)
        {
            dust.Drop(butterflies[pollenVisitor].position);
            pollenVisitor=(pollenVisitor+5)%butterflies.Length;
            nextGrain=now+.075f;
        }
    }
}
