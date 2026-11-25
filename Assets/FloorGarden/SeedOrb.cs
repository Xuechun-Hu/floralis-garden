using UnityEngine;

internal sealed class SeedOrb
{
    private enum Motion { Waiting, Held, Flying, Rolling, Open }
    private const float Radius=.049f;
    private const float MinFloorRange=1f, Gravity=9.4f;
    private Motion motion;
    private readonly Transform orb;
    private readonly PortalSurfaces surfaces;
    private int owner;
    private Vector3 wallPoint,wallNormal;
    private const float WallSnapDistance=.80f;
    private bool bounced;
    private readonly Transform contact;
    private readonly Material contactInk;
    private Vector3 gripOffset, flightSpin, floorNormal=Vector3.up;
    private Vector3 velocity, releaseOrigin;
    private readonly Vector3[] gripSamples=new Vector3[12];
    private readonly float[] gripTimes=new float[12];
    private int nextSample,sampleCount,flightFrames,slowFrames;
    private float gripClock,maxFrame,lost,airtime,rolled,ground;
    private Vector3 rollAssistDirection,flightCorrection;
    private float flightDuration;
    public SeedOrb(PortalArt art,Transform parent,Transform eye,PortalSurfaces surfaces)
    {
        this.surfaces=surfaces;
        orb=PortalArt.Child("Meadow seed",parent);
        var sphere=art.SurfaceMesh("Garden inside seed",32,16,(u,v)=>{
            float a=Mathf.PI*2*u,b=(v-.5f)*Mathf.PI;
            return Radius*new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Sin(b),Mathf.Cos(a)*Mathf.Cos(b));
        });
        PortalArt.Show("Garden pearl",orb,sphere,art.Paint("PortalMeadow2K",Color.white));
        var ring=art.SurfaceMesh("Seed filigree",36,6,(u,v)=>{
            float a=u*2*Mathf.PI,b=v*2*Mathf.PI;
            float r=Radius+.0018f*Mathf.Cos(b);
            return new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,.0018f*Mathf.Sin(b));
        });
        var gilt=art.Paint(null,new Color(1,.78f,.36f));
        for(int i=0;i<3;i++)PortalArt.Show("Gold band",orb,ring,gilt).localRotation=Quaternion.Euler(48,60*i,20);
        contactInk=art.MaterialFor("SeedContact");
        contact=PortalArt.Show("Seed contact on floor",parent,art.SurfaceMesh("Contact patch",1,1,
            (u,v)=>new Vector3((u-.5f)*.17f,0,(v-.5f)*.17f)),contactInk);
        contact.gameObject.SetActive(false);
        orb.position=Hover(eye);
    }
    private static Vector3 Hover(Transform eye)=>eye.position+eye.forward*.40f-eye.up*.17f;
    private static Vector3 Horizontal(Vector3 v)=>new Vector3(v.x,0,v.z);
    private void SampleGrip(Vector3 position)
    {
        gripSamples[nextSample]=position;gripTimes[nextSample]=gripClock;
        nextSample=(nextSample+1)%gripSamples.Length;
        sampleCount=Mathf.Min(sampleCount+1,gripSamples.Length);
        // Fit a velocity over the last 75 ms, including the frame the pinch opens.
        // Timestamps also account for duplicate poses and irregular tracking intervals.
        float tSum=0,t2Sum=0;
        Vector3 pSum=Vector3.zero,tpSum=Vector3.zero;
        int count=0;
        for(int i=0;i<sampleCount;i++)
        {
            int index=(nextSample-1-i+gripSamples.Length)%gripSamples.Length;
            float t=gripTimes[index]-gripClock;
            if(t<-.075f)break;
            Vector3 delta=gripSamples[index]-position;
            count++;tSum+=t;t2Sum+=t*t;pSum+=delta;tpSum+=delta*t;
        }
        float variance=count*t2Sum-tSum*tSum;
        if(variance>1e-7f)velocity=Vector3.ClampMagnitude((count*tpSum-tSum*pSum)/variance,6.5f);
    }

    private void BeginFlight(Transform eye,float frameTime)
    {
        releaseOrigin=eye.position;
        ground=surfaces.TryLanding(orb.position+Vector3.up*.08f,out var hit,out _,out _)
            ?hit.y:surfaces.FloorHeight(eye.position);
        var horizontal=Horizontal(velocity);
        rollAssistDirection=horizontal.magnitude>.35f?horizontal.normalized:Horizontal(orb.position-releaseOrigin).normalized;
        if(rollAssistDirection.sqrMagnitude<.1f)rollAssistDirection=Horizontal(eye.forward).normalized;
        if(rollAssistDirection.sqrMagnitude<.1f)rollAssistDirection=Vector3.forward;
        float height=Mathf.Max(0,orb.position.y-ground-Radius);
        flightDuration=(velocity.y+Mathf.Sqrt(velocity.y*velocity.y+2*Gravity*height))/Gravity;
        var predicted=Horizontal(orb.position-releaseOrigin)+horizontal*flightDuration;
        float target=MinFloorRange+Radius+.05f;
        flightCorrection=Vector3.zero;
        if(predicted.sqrMagnitude<target*target && flightDuration>.05f)
        {
            float along=Vector3.Dot(predicted,rollAssistDirection);
            float distance=Mathf.Sqrt(along*along+target*target-predicted.sqrMagnitude)-along;
            // Low releases have little airtime. Leave the remaining travel to the ground roll.
            flightCorrection=rollAssistDirection*Mathf.Min(distance,4*flightDuration*flightDuration);
        }
        flightSpin=Vector3.Cross(Vector3.up,horizontal)/Radius*.22f;
        bounced=false;motion=Motion.Flying;airtime=0;
        flightFrames=1;maxFrame=frameTime;slowFrames=frameTime>.025f?1:0;
    }
    // A lost tracked hand is allowed to recover briefly, then the seed returns to reach.
    public bool Tick(Transform eye,Vector3? left,bool leftGrip,Vector3? right,bool rightGrip,float dt,
        out Vector3 center,out Vector3 outward,out float floorY)
    {
        center=outward=default;floorY=0;
        float frameTime=dt;
        if(motion==Motion.Flying || motion==Motion.Rolling)
        {
            flightFrames++;maxFrame=Mathf.Max(maxFrame,dt);
            if(dt>.025f)slowFrames++;
        }
        dt=Mathf.Clamp(dt,.001f,.1f);
        if(motion==Motion.Open)return false;
        if(motion==Motion.Waiting)
        {
            contact.gameObject.SetActive(false);
            orb.position=Vector3.Lerp(orb.position,Hover(eye),1-Mathf.Exp(-3*dt));
            orb.Rotate(12*dt,32*dt,0);
            bool l=leftGrip && left.HasValue && (left.Value-orb.position).sqrMagnitude<.0121f;
            bool r=rightGrip && right.HasValue && (right.Value-orb.position).sqrMagnitude<.0121f;
            if(l || r)
            {
                owner=l?0:1;motion=Motion.Held;velocity=Vector3.zero;
                gripOffset=orb.position-(l?left:right).Value;
                lost=gripClock=0;nextSample=sampleCount=0;
                SampleGrip(orb.position);
            }
            return false;
        }
        if(motion==Motion.Held)
        {
            var grip=owner==0?left:right;
            gripClock+=frameTime;
            if(!grip.HasValue)
            {
                // Bridge brief tracking gaps directly from the last measured pose.
                float coast=Mathf.Min(lost+dt,.045f)-Mathf.Min(lost,.045f);
                orb.position+=velocity*coast;
                lost+=dt;
                if(lost>.22f)motion=Motion.Waiting;
                return false;
            }
            if(lost>.075f){sampleCount=0;velocity=Vector3.zero;}
            lost=0;
            orb.position=grip.Value+gripOffset;
            SampleGrip(orb.position);
            if(owner==0?leftGrip:rightGrip)return false;
            BeginFlight(eye,frameTime);
            return false;
        }
        bool rolling=motion==Motion.Rolling;
        var from=orb.position;
        Vector3 to;
        if(rolling)
        {
            rolled+=dt;
            var tangent=Vector3.ProjectOnPlane(velocity,floorNormal);
            tangent+=Vector3.ProjectOnPlane(Vector3.down*Gravity,floorNormal)*(.71f*dt);
            float resistance=.45f+.32f*tangent.magnitude+.13f*tangent.sqrMagnitude;
            velocity=Vector3.MoveTowards(tangent,Vector3.zero,resistance*dt);
            // Only help a short drop after contact. Ease into the roll without an air impulse.
            float remaining=MinFloorRange+Radius+.05f-Clearance(from,eye.position);
            if(remaining>0)
            {
                var heading=Vector3.ProjectOnPlane(rollAssistDirection,floorNormal).normalized;
                float desired=Mathf.Min(.78f,Mathf.Sqrt(1.8f*remaining));
                float deficit=desired-Vector3.Dot(velocity,heading);
                if(deficit>0)velocity+=heading*Mathf.Min(deficit,1.6f*dt);
            }
            to=from+(tangent+velocity)*(.5f*dt);
        }
        else
        {
            float start=airtime;
            airtime+=dt;
            to=from+velocity*dt+Vector3.down*(.5f*Gravity*dt*dt);
            velocity+=Vector3.down*(Gravity*dt);
            if(flightCorrection.sqrMagnitude>0)
            {
                // Add displacement with zero initial velocity and acceleration, so release
                // preserves hand speed. Carry the accumulated velocity through contact.
                float t0=Mathf.Clamp01(start/flightDuration), t1=Mathf.Clamp01(airtime/flightDuration);
                float Path(float t)=>t*t*t*(6+t*(-8+3*t));
                float Speed(float t)=>t*t*(18+t*(-32+15*t))/flightDuration;
                float overrun=Mathf.Max(0,airtime-flightDuration);
                to+=flightCorrection*(Path(t1)-Path(t0)+Speed(t1)*overrun-Speed(t0)*dt);
                velocity+=flightCorrection*(Speed(t1)-Speed(t0));
                if(t1>=1)flightCorrection=Vector3.zero;
            }
        }
        if(surfaces.Hit(from,to,Radius,out var hit,out var n,out var floor))
        {
            if(!floor)
            {
                wallPoint=hit;
                wallNormal=Vector3.ProjectOnPlane(n,Vector3.up).normalized;
                if(Vector3.Dot(wallNormal,eye.position-hit)<0)wallNormal=-wallNormal;
                orb.position=hit+wallNormal*Radius;
                return OpenWall(eye,out center,out outward,out floorY);
            }
            if(!rolling)
            {
                flightCorrection=Vector3.zero;
                floorNormal=n.y<0?-n:n;
                ground=hit.y;
                float impact=-Vector3.Dot(velocity,floorNormal);
                var tangent=Vector3.ProjectOnPlane(velocity,floorNormal);
                to=hit+floorNormal*Radius;
                if(!bounced && impact>1.45f)
                {
                    // A short, damped first contact. Tangential motion carries through impact.
                    bounced=true;
                    velocity=tangent*.94f+floorNormal*Mathf.Min(.68f,impact*.20f);
                    flightSpin=Vector3.Cross(floorNormal,tangent)*(.58f/Radius);
                }
                else
                {
                    motion=Motion.Rolling;rolled=0;
                    velocity=tangent*.90f;
                }
            }
        }
        if(rolling)
        {
            // Check the new contact before advancing, including the last few cm at a scan edge.
            if(surfaces.TryLanding(to+Vector3.up*.10f,out var support,out var b,out var c)
               && Mathf.Abs(support.y-ground)<.10f)
            {
                var normal=Vector3.Cross(b-support,c-support).normalized;
                if(Mathf.Abs(normal.y)>.5f)floorNormal=normal.y<0?-normal:normal;
                ground=support.y;
                to.y=ground+Radius/Mathf.Max(.5f,floorNormal.y);
            }
            else {to=from;velocity=Vector3.zero;}
        }
        orb.position=to;
        if(rolling)
        {
            Vector3 travel=Vector3.ProjectOnPlane(to-from,floorNormal);
            orb.Rotate(Vector3.Cross(floorNormal,travel),travel.magnitude/Radius*Mathf.Rad2Deg,Space.World);
        }
        else orb.Rotate(flightSpin,flightSpin.magnitude*dt*Mathf.Rad2Deg,Space.World);

        if(motion==Motion.Rolling || bounced)
        {
            float clearance=Mathf.Max(0,to.y-ground-Radius);
            contact.gameObject.SetActive(true);
            contact.position=new Vector3(to.x,ground+.002f,to.z);
            contact.rotation=Quaternion.FromToRotation(Vector3.up,floorNormal);
            contactInk.SetFloat("_Contact",.38f*Mathf.Clamp01(1-clearance/.16f));
        }
        if(rolling && rolled>.22f && velocity.magnitude<.065f &&
            ((to-from).sqrMagnitude<1e-12f || Clearance(to,eye.position)>=MinFloorRange+Radius))
        {
            return OpenAtLanding(eye,to,out center,out outward,out floorY);
        }
        else if(!rolling && (to.y<ground-.18f || airtime>4f ||
                 (to-eye.position).sqrMagnitude>64f))
        {
            // Tracking may return walls before the first labelled floor patch.
            return OpenAtLanding(eye,to,out center,out outward,out floorY);
        }
        return false;
    }

    private bool OpenAtLanding(Transform eye,Vector3 landing,out Vector3 center,out Vector3 outward,out float floorY)
    {
        floorY=ground;
        center=outward=default;
        if(Clearance(landing,eye.position)<MinFloorRange+Radius)return Recall();
        var origin=new Vector3(landing.x,ground+.835f,landing.z);
        // Freeze the bearing from release to landing, even if the player turns or walks.
        var bearing=Horizontal(landing-releaseOrigin).normalized;
        if(bearing.sqrMagnitude>.5f)
        {
            // Sample the same vertical strip at two heights to tolerate a gap in the wall scan.
            for(int i=0;i<2;i++)
            {
                var start=origin+Vector3.up*(i*.32f);
                if(!surfaces.Hit(start,start+bearing*WallSnapDistance,0,out var hit,out var normal,out bool floor) || floor)
                    continue;
                normal=Horizontal(normal).normalized;
                if(Vector3.Dot(normal,-bearing)<.35f)continue;
                wallPoint=hit;wallNormal=normal;
                return OpenWall(eye,out center,out outward,out floorY);
            }
        }
        center=origin+Vector3.up*.12f;
        outward=Horizontal(eye.position-center).normalized;
        if(outward.sqrMagnitude<.1f)outward=bearing.sqrMagnitude>.5f?-bearing:Vector3.back;
        return Open("air",eye,center);
    }

    private bool OpenWall(Transform eye,out Vector3 center,out Vector3 outward,out float floorY)
    {
        outward=wallNormal;
        center=wallPoint+outward*.045f;
        // A small scan gap near the skirting board must not move the door off its wall.
        floorY=surfaces.FloorHeight(eye.position);
        for(int i=0;i<5;i++)
        {
            var probe=center+outward*(i*.075f);
            probe.y=Mathf.Max(eye.position.y,center.y)+.12f;
            if(!surfaces.TryLanding(probe,out var hit,out _,out _))continue;
            floorY=hit.y;break;
        }
        center.y=floorY+.835f;
        return Open("wall",eye,center);
    }
    public void Hide()=>orb.gameObject.SetActive(false);
    private float Clearance(Vector3 point,Vector3 eye)
    {
        return Mathf.Min(Horizontal(point-releaseOrigin).magnitude,Horizontal(point-eye).magnitude);
    }

    public bool CanReveal(Vector3 eye,Vector3 door)
    {
        return Clearance(orb.position,eye)>=MinFloorRange+Radius && Clearance(door,eye)>=MinFloorRange;
    }

    public bool Recall()
    {
        contact.gameObject.SetActive(false);
        motion=Motion.Waiting;
        return false;
    }

    private bool Open(string surface,Transform eye,Vector3 center)
    {
        if(!CanReveal(eye.position,center))return Recall();
        motion=Motion.Open;
        contact.gameObject.SetActive(false);
        Debug.Log($"[AngelWindow] Portal opened on {surface}; seed clearance={Clearance(orb.position,eye.position):F2}m, portal clearance={Clearance(center,eye.position):F2}m; throw frames={flightFrames}, over25ms={slowFrames}, worst={maxFrame*1000:F1}ms");
        return true;
    }
}
