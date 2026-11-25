using System.IO;
using UnityEngine;

internal sealed class PortalGuide
{
    private const float VisitPause=1.15f;
    private float visitDuration, returnDuration;
    private readonly PortalArt art;
    private const float WelcomeDuration=1.75f, OutlineDuration=8f;
    private const float TextWidth=.48f;
    private static readonly Vector3 DoorPerch=new(.32f,.31f,-.10f);
    private readonly Transform window, root, plaque;
    private readonly Butterfly guide, writer, lowerWriter;
    private readonly Vector3[] outline;
    private readonly LineRenderer trail;
    private readonly MeshRenderer caption;
    private readonly Material captionInk;
    private readonly ButterflyWriting writing;
    private readonly ButterflyDust dust;
    private Vector3 visit, traceStart, jambAnchor;
    private bool started, puzzleHint;

    private float clock, puzzleStart, writerNextGrain, guideGrainCredit=1;
    private Vector3 lastGuideWorld;
    private int readingSlot=-1;
    private static Vector3 MeetPlayer(Vector3 view,float side)
    {
        Vector3 toDoor=Vector3.ProjectOnPlane(DoorPerch-view,Vector3.up);
        if(toDoor.magnitude<.95f)return DoorPerch;
        return view+toDoor.normalized*.82f+new Vector3(side*.15f,-.12f,0);
    }
    private float retreatStart=-1;
    private Vector3 retreatGuide, retreatWriter;

    private sealed class Butterfly
    {
        public readonly Transform body;
        private readonly Transform[] wings=new Transform[2];
        public Butterfly(string name,Transform root,Mesh wing,Mesh abdomen,Material ink,Material bronze,float size)
        {
            body=PortalArt.Child(name,root);
            body.localScale=Vector3.one*size;
            for(int side=0;side<2;side++)
            {
                wings[side]=PortalArt.Child("Wing hinge",body);
                float sign=side==0?-1:1;
                var upper=PortalArt.Show("Forewing",wings[side],wing,ink);
                upper.localScale=new Vector3(sign,1,1);
                var lower=PortalArt.Show("Hindwing",wings[side],wing,ink);
                lower.localRotation=Quaternion.Euler(0,0,-sign*52);
                lower.localScale=new Vector3(.64f*sign,.70f,1);
            }
            PortalArt.Show("Bronze body",body,abdomen,bronze);
        }
        public void Fly(Vector3 position,Transform eye,float beat,float bank=0)
        {
            body.localPosition=position;
            var away=body.position-eye.position;
            if(away.sqrMagnitude>.0001f)body.rotation=Quaternion.LookRotation(away,Vector3.up)*Quaternion.AngleAxis(bank,Vector3.forward);
            float flap=28+47*Mathf.Sin(beat);
            wings[0].localRotation=Quaternion.Euler(0,flap,0);
            wings[1].localRotation=Quaternion.Euler(0,-flap,0);
        }
    }

    public PortalGuide(PortalArt art,Transform window)
    {
        this.window=window;
        this.art=art;
        dust=art.Dust;
        root=PortalArt.Child("Butterfly puzzle guide",window);
        var wing=art.SurfaceMesh("Swallowtail",13,10,(u,v)=>{
            float span=.078f*v;
            float width=(.010f+.054f*Mathf.Sin(Mathf.PI*(.05f+.85f*v)))*(1+.15f*Mathf.Cos(v*8));
            return new Vector3(span,(u-.46f)*width+.032f*v,-.003f*Mathf.Sin(u*Mathf.PI));
        });
        var body=art.SurfaceMesh("Butterfly body",10,6,(u,v)=>{
            float a=u*Mathf.PI*2, b=(v-.5f)*Mathf.PI;
            return new Vector3(.004f*Mathf.Cos(a)*Mathf.Cos(b),.028f*Mathf.Sin(b),.004f*Mathf.Sin(a)*Mathf.Cos(b));
        });
        var bronze=art.Paint(null,new Color(.33f,.17f,.065f));
        guide=new Butterfly("Guiding golden butterfly",root,wing,body,art.ButterflyInk(new Color(1,.78f,.30f)),bronze,.88f);
        Material WriterInk(Material m)
        {
            m.renderQueue=3103;
            if(m.GetFloat("_WingPattern")>.5f)m.SetFloat("_WingPattern",2);
            return m;
        }
        var penBody=WriterInk(art.Paint(null,new Color(.34f,.19f,.08f)));
        writer=new Butterfly("Sage lettering butterfly",root,wing,body,WriterInk(art.ButterflyInk(new Color(.47f,.66f,.55f))),penBody,.38f);
        lowerWriter=new Butterfly("Rose lettering butterfly",root,wing,body,WriterInk(art.ButterflyInk(new Color(.78f,.51f,.55f))),penBody,.38f);
        plaque=PortalArt.Child("Flora writing plaque",root);
        var paper=art.MaterialFor("FloraPlaqueInk");
        paper.renderQueue=3100;
        paper.SetFloat("_DepthWrite",1);
        paper.mainTexture=Resources.Load<Texture2D>("FloraPlaque");
        PortalArt.Show("Lily lithograph",plaque,art.SurfaceMesh("Ivory paper",1,1,
            (u,v)=>new Vector3((u-.5f)*.80f,(v-.5f)*.286f,0)),paper);

        var bytes=Resources.Load<TextAsset>("ButterflyOutline").bytes;
        using(var reader=new BinaryReader(new MemoryStream(bytes)))
        {
            outline=new Vector3[bytes.Length/8];
            for(int i=0;i<outline.Length;i++)outline[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),-.063f);
        }
        trail=PortalArt.Child("Gold tracing line",root).gameObject.AddComponent<LineRenderer>();
        trail.useWorldSpace=false;trail.positionCount=22;
        trail.startWidth=.0012f;trail.endWidth=.010f;
        trail.sharedMaterial=art.Paint(null,new Color(1,.82f,.31f));
        trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;trail.receiveShadows=false;
        captionInk=art.MaterialFor("FloraPlaqueInk");
        captionInk.mainTexture=Resources.Load<Texture2D>("PortalGuideCaptions");
        captionInk.renderQueue=3102;
        caption=PortalArt.Show("Gesture feedback",plaque,art.SurfaceMesh("Feedback line",1,1,
            (u,v)=>new Vector3((u-.5f)*.68f,(v-.5f)*.24f,-.004f)),captionInk).GetComponent<MeshRenderer>();
        caption.enabled=false;
        writing=new ButterflyWriting(art,plaque,window,TextWidth);
        root.gameObject.SetActive(false);
    }

    private Vector3 Trace(float phase)
    {
        float at=Mathf.Repeat(phase,1)*outline.Length;
        int i=(int)at;
        return Vector3.Lerp(outline[i],outline[(i+1)%outline.Length],at-i);
    }

    private void PlacePlaque(Transform eye,Vector3 view,float dt)
    {
        var jamb=window.TransformPoint(jambAnchor);
        var at=eye.InverseTransformPoint(jamb);
        // As the jamb moves into peripheral vision, bring the sign below and beside the gaze.
        float near=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.9f,.98f,-view.z));
        float roomDepth=Mathf.Max(.16f,-view.z*.78f);
        float depth=Mathf.Min(Mathf.Clamp(at.z,.72f,1.25f),roomDepth);
        var faceMin=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
        var faceMax=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
        // Face bounds measured in SHARP asset space, including the nose and cheeks.
        for(int corner=0;corner<8;corner++)
        {
            var pt=new Vector3((corner&1)==0?-.078f:.080f,(corner&2)==0?.354f:.551f,(corner&4)==0?-.207f:-.157f);
            var pos=eye.InverseTransformPoint(art.Angel.TransformPoint(pt));
            var xy=new Vector2(pos.x,pos.y)/Mathf.Max(.035f,pos.z);
            faceMin=Vector2.Min(faceMin,xy);faceMax=Vector2.Max(faceMax,xy);
        }
        float preferred=Mathf.Sign(jambAnchor.x), best=float.PositiveInfinity;
        Vector2 slot=default;
        int selected=0;
        for(int i=0;i<4;i++)
        {
            var xy=new Vector2((i%2==0?1:-1)*preferred*.34f,i<2?-.32f:.30f);
            float overlapX=Mathf.Max(0,Mathf.Min(xy.x+.26f,faceMax.x)-Mathf.Max(xy.x-.26f,faceMin.x));
            float overlapY=Mathf.Max(0,Mathf.Min(xy.y+.095f,faceMax.y)-Mathf.Max(xy.y-.095f,faceMin.y));
            float score=overlapX*overlapY+i*.0009f+(i!=readingSlot?.0015f:0);
            if(score>=best)continue;
            best=score;selected=i;slot=xy;
        }
        var read=new Vector3(slot.x*depth,slot.y*depth,depth);
        float facing=Vector3.Dot(eye.forward,window.forward);
        float blend=near*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.06f,.35f,facing));
        var target=Vector3.Lerp(jamb,eye.TransformPoint(read),blend);
        float follow=started && selected==readingSlot?1-Mathf.Exp(-24*Mathf.Max(0,dt)):1;
        readingSlot=selected;
        plaque.position=Vector3.Lerp(plaque.position,target,follow);
        plaque.localScale=Vector3.one*Mathf.Lerp(1,depth*.65f,blend);
        plaque.rotation=eye.rotation;
        writer.body.localScale=lowerWriter.body.localScale=Vector3.one*(.38f*plaque.localScale.x);
    }

    private static Vector3 LeadFlight(Vector3 a,Vector3 b,float fraction,float side,out float bank)
    {
        float t=Mathf.SmoothStep(0,1,fraction);
        Vector3 span=Vector3.ProjectOnPlane(b-a,Vector3.up);
        var across=Vector3.Cross(Vector3.up,span.normalized);
        float amp=Mathf.Min(.11f,span.magnitude*.12f);
        float bow=Mathf.Sin(t*Mathf.PI);
        bank=side*5*Mathf.Sin(2*Mathf.PI*t);
        return Vector3.Lerp(a,b,t)+(across*side+Vector3.up*.45f)*(amp*bow);
    }

    private static Vector3 Arc(Vector3 a,Vector3 b,float fraction,float lift)
    {
        float t=Mathf.SmoothStep(0,1,fraction);
        return Vector3.Lerp(a,b,t)+Vector3.up*(lift*Mathf.Sin(Mathf.PI*t));
    }

    public void Tick(Transform eye,PortalGesture gesture,bool controller,float age,float dt)
    {
        Vector3 view=window.InverseTransformPoint(eye.position);
        if(!started)
        {
            root.gameObject.SetActive(true);
            float side=view.x<-.35f?-1:1;
            jambAnchor=new Vector3(side*1.12f,Mathf.Clamp(view.y-.17f,-.2f,.48f),-.048f);
            visit=MeetPlayer(view,side);
            float travel=Vector3.Distance(DoorPerch,visit);
            visitDuration=Mathf.Max(.85f,travel/.70f);
            returnDuration=Mathf.Max(1.3f,travel/.60f);
            guide.body.localPosition=DoorPerch;
            lastGuideWorld=window.TransformPoint(DoorPerch);

        }
        PlacePlaque(eye,view,dt);
        started=true;
        clock+=dt;
        float beyondJamb=Mathf.Max(Mathf.Abs(view.x)-.67f,0);
        float distanceSq=beyondJamb*beyondJamb+view.z*view.z;
        bool headAtDoor=view.z<=0 && distanceSq<=.30f*.30f;
        bool inReach=gesture.Near && (gesture.InReach || gesture.Placing);
        bool atDoor=headAtDoor || inReach;
        // A few centimetres of hysteresis keep small head movements from restarting the sentence.
        if(puzzleHint && !gesture.Near && (view.z>.02f || distanceSq>.38f*.38f))
        {
            puzzleHint=false;
            retreatStart=clock;
            retreatGuide=guide.body.localPosition;
            retreatWriter=writer.body.localPosition;
        }
        if(!puzzleHint && atDoor)
        {
            Debug.Log($"[AngelWindow] Puzzle instruction: door {Mathf.Sqrt(distanceSq):F2} m, hand in reach {inReach}");
            puzzleHint=true;
            puzzleStart=clock;
            traceStart=guide.body.localPosition;

        }

        float elapsed=puzzleHint?clock-puzzleStart:clock-visitDuration;
        caption.enabled=puzzleHint;
        float progress=(clock-visitDuration)/WelcomeDuration;
        var welcomeTip=writing.Welcome(progress,!puzzleHint);
        float penFollow=1-Mathf.Exp(-18*dt);
        lowerWriter.body.gameObject.SetActive(puzzleHint);
        if(puzzleHint)
        {
            int row=gesture.Placing ? (gesture.Aligned?2:1) : gesture.Inspecting ? (controller?4:3) : 0;
            captionInk.SetFloat("_Row",row);
            float phase=elapsed*.85f;
            void Circle(Butterfly butterfly,float angle,float beat)
            {
                // A rounded rectangular orbit follows the border, leaving the text clear.
                float x=Mathf.Cos(angle), y=Mathf.Sin(angle);
                var rim=new Vector3(.40f*Mathf.Sign(x)*Mathf.Sqrt(Mathf.Abs(x)),
                    .148f*Mathf.Sign(y)*Mathf.Sqrt(Mathf.Abs(y)),-.020f);
                var target=window.InverseTransformPoint(plaque.TransformPoint(rim));
                var position=Vector3.Lerp(butterfly.body.localPosition,target,penFollow);
                if(butterfly==lowerWriter && elapsed<=dt)position=target;
                butterfly.Fly(position,eye,beat,4*y);
            }
            Circle(writer,phase+.40f,age*17+.8f);
            Circle(lowerWriter,phase+.40f+Mathf.PI,age*18-.6f);
        }
        else
        {
            var pen=progress>=1?Arc(welcomeTip,Perch(1),(progress-1)*WelcomeDuration/.38f,.018f):welcomeTip;
            float retreat=(clock-retreatStart)/.36f;
            if(retreat<1)pen=Arc(retreatWriter,pen,retreat,.018f);
            writer.Fly(clock<=dt?pen:Vector3.Lerp(writer.body.localPosition,pen,penFollow),eye,age*17+.8f);
        }
        float bank=0;
        Vector3 destination;
        trail.enabled=false;
        if(puzzleHint)
        {
            const float approach=.70f;
            float phase=Mathf.Max(0,elapsed-approach)/OutlineDuration;
            destination=Arc(traceStart,Trace(phase),elapsed/approach,.045f);
            if(elapsed>=approach)
            {
                trail.enabled=true;
                for(int i=0;i<trail.positionCount;i++)
                {
                    float lag=(1-(float)i/(trail.positionCount-1))*.075f;
                    trail.SetPosition(i,Trace(Mathf.Max(0,phase-lag)));
                }
            }
        }
        else
        {
            float side=Mathf.Sign(jambAnchor.x);
            if(clock<visitDuration)destination=LeadFlight(DoorPerch,visit,clock/visitDuration,side,out bank);
            else destination=LeadFlight(visit,DoorPerch,(clock-visitDuration-VisitPause)/returnDuration,side,out bank);
        }
        if(!puzzleHint && clock-retreatStart<.85f)destination=Arc(retreatGuide,destination,(clock-retreatStart)/.85f,.035f);
        guide.Fly(destination,eye,age*17,bank);
        Vector3 here=guide.body.position;
        // Deposit along the whole wing path, including fast flights and slower render frames.
        guideGrainCredit+=Mathf.Max(Mathf.Min(dt,.10f)*18,Vector3.Distance(lastGuideWorld,here)/.075f);
        int drops=Mathf.Min(5,Mathf.FloorToInt(guideGrainCredit));
        if(drops>0)
        {
            for(int i=1;i<=drops;i++)
                dust.Drop(Vector3.Lerp(lastGuideWorld,here,(float)i/drops),true);
            guideGrainCredit-=Mathf.Floor(guideGrainCredit);
        }
        lastGuideWorld=here;
        if(age>=writerNextGrain && !puzzleHint && progress>=0 && progress<1)
        {
            dust.Drop(writer.body.position);
            writerNextGrain=age+.18f;
        }
    }
    private Vector3 Perch(float side)=>window.InverseTransformPoint(plaque.TransformPoint(new Vector3(.30f,side*.068f,-.014f)));
    public void Hide()=>root.gameObject.SetActive(false);
}
