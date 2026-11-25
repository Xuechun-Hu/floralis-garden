using System.Collections.Generic;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.SceneManagement;

public sealed class AngelWindowExperience : MonoBehaviour
{
    private enum Stage { Seed, Greeting, Entering, Garden }
    private Stage stage;
    private readonly PortalSurfaces surfaces = new();
    private SeedOrb seed;
    private readonly List<XRMeshSubsystem> scanners = new();
    private XRMeshSubsystem scanner;
    private bool permission, startedScanner, entered;
    private Transform content;
    private PortalArt art;
    private StoryMeadow meadow;
    private static readonly int WeatherTime=Shader.PropertyToID("_FloraWeatherTime");
    private double weatherStart;
    private float nextCheck, since, floorY;
    private GardenHand leftHand, rightHand;
    private Material handFinish;
    private PortalGesture gesture;
    private bool pendingPortal;
    private float portraitSeconds,portraitWorst;
    private int portraitFrames,portraitSlowFrames;
    private Vector3 pendingCenter,pendingNormal;
    private readonly List<XRInputSubsystem> inputs = new();
    private XRInputSubsystem input;
    private bool paused,resetPending,reloading,sawUserPresent,havePose;
    private Vector3 previousHead;
    private double previousSample,trackingSince=-1;
    private System.Collections.IEnumerator PreparePortal()
    {
        // Give XR and the first interactive seed frame priority on a cold start.
        yield return new WaitUntil(()=>seed!=null);
        yield return new WaitForSecondsRealtime(.30f);
        yield return art.Prepare(content,surfaces);
        Debug.Log($"[AngelWindow] Portal assets ready, {art.Angel.GetComponent<MeshFilter>().sharedMesh.vertexCount/4} splats");
    }

    private void OnEnable()
    {
        paused=resetPending=reloading=havePose=sawUserPresent=false;
        trackingSince=-1;
#if UNITY_ANDROID && !UNITY_EDITOR
        PXR_Manager.EnableVideoSeeThrough = true;
        PXR_PermissionRequest.RequestUserPermissionMR(null, _ => permission = true);
#else
        permission = true;
#endif
    }

    private void BeginExperience()
    {
        gesture = new PortalGesture();
        stage = Stage.Seed;
        entered = false;pendingPortal=false;
        art = new PortalArt();
        content = PortalArt.Child("Angel window experience", transform);
        PXR_Manager.SpatialMeshDataUpdated += QueueBlocks;
        StartCoroutine(PreparePortal());
        Debug.Log("[AngelWindow] Pinch the seed and release it towards a scanned wall or floor");
    }

    private void QueueBlocks(List<PxrSpatialMeshInfo> updates)
    {
        if(art!=null && (stage==Stage.Seed || stage==Stage.Greeting) && surfaces.Update(updates))
            InvalidateSpace("previous floor and wall mesh removed");
    }

    private void Update()
    {
        if(paused || reloading || !TrackingReady())return;
        if(resetPending)
        {
            // Replace the scene's PXR_Manager too. Mesh-query coroutines started in
            // the previous space must not deliver their results to the new session.
            reloading=true;
            SceneManager.LoadSceneAsync(gameObject.scene.buildIndex,LoadSceneMode.Single);
            return;
        }
        var camera = Camera.main;
        if (!camera) return;
        if(art==null)BeginExperience();
        var eye = camera.transform;
        if (leftHand == null)
        {
            handFinish = new Material(Resources.Load<Shader>("GardenHands"));
            leftHand = new GardenHand(true,eye.parent,handFinish);
            rightHand = new GardenHand(false,eye.parent,handFinish);
        }
        leftHand.Sample();
        rightHand.Sample();
        float now=Time.unscaledTime, age=now-since;
        if(stage!=Stage.Seed)
            Shader.SetGlobalFloat(WeatherTime,(float)(Time.unscaledTimeAsDouble-weatherStart));
        if (stage==Stage.Seed)
        {
            if(permission && now>=nextCheck && (scanner==null || !scanner.running))
            {
                nextCheck=now+1;
                SubsystemManager.GetSubsystems(scanners);
                if(scanners.Count>0)
                {
                    scanner=scanners[0];
                    if(!scanner.running){scanner.Start();startedScanner=true;}
                }
            }
            AdvanceSeed(eye,leftHand.GripPoint,leftHand.Gripping,rightHand.GripPoint,rightHand.Gripping,Time.unscaledDeltaTime);
        }
        else if (stage==Stage.Greeting)
        {
            AdvanceGreeting(eye, leftHand.GripPoint, leftHand.Gripping, rightHand.GripPoint, rightHand.Gripping, now);
        }
        else if(stage==Stage.Entering)
        {
            art.GrowLandingFlowers(age);
            if(art.Window.gameObject.activeSelf)
            {
                if(age<5.5f)art.Scatter(age);
                else art.FinishCrossing();
            }
            const float switchTime=6.4f;
            if(age>=switchTime && !entered && meadow.Ready)
            {
                meadow.Enter(now);
                entered=true;
                since=now-switchTime; age=switchTime;
            }
            if(entered)
            {
                const float fadeDuration=2.0f;
                float t=Mathf.Clamp01((age-switchTime)/fadeDuration);
                meadow.SetOpacity(Mathf.SmoothStep(0,1,t));
                meadow.Tick(now,leftHand.Index,rightHand.Index);
                if(t>=1)
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                    PXR_Manager.EnableVideoSeeThrough=false;
#endif
                    stage=Stage.Garden;
                    Debug.Log("[AngelWindow] Garden crossfade complete");
                }
            }

        }
        else if(stage==Stage.Garden) meadow.Tick(now,leftHand.Index,rightHand.Index);
    }

    private void AdvanceSeed(Transform eye,Vector3? left,bool leftGrip,Vector3? right,bool rightGrip,float dt)
    {
        if(seed==null)
        {
            seed=new SeedOrb(art,content,eye,surfaces);
            Debug.Log("[AngelWindow] Interactive seed ready");
        }
        if(!pendingPortal)
            pendingPortal=seed.Tick(eye,left,leftGrip,right,rightGrip,dt,out pendingCenter,out pendingNormal,out floorY);
        if(!pendingPortal || !art.Ready)return;
        if(!seed.CanReveal(eye.position,pendingCenter))
        {
            seed.Recall();pendingPortal=false;return;
        }
        double begin=Time.realtimeSinceStartupAsDouble;
        seed.Hide();
        weatherStart=Time.unscaledTimeAsDouble;
        Shader.SetGlobalFloat(WeatherTime,0);
        art.Open(pendingCenter,pendingNormal,floorY);
        art.Greet(eye.position,0);
        portraitSeconds=portraitWorst=0;portraitFrames=portraitSlowFrames=0;
        stage=Stage.Greeting;since=Time.unscaledTime;
        Debug.Log($"[AngelWindow] Portal at {pendingCenter}, outward {pendingNormal}, floor {floorY}, reveal {(Time.realtimeSinceStartupAsDouble-begin)*1000:F1} ms");
    }

    private void AdvanceGreeting(Transform eye, Vector3? left, bool leftHeld, Vector3? right, bool rightHeld, float now)
    {
        float age=now-since;
        float frame=Time.unscaledDeltaTime;
        if(age>=1.2f && portraitSeconds<3 && frame>0 && frame<.5f)
        {
            portraitSeconds+=frame;portraitFrames++;
            portraitWorst=Mathf.Max(portraitWorst,frame);
            if(frame>.025f)portraitSlowFrames++;
            if(portraitSeconds>=3)
                Debug.Log($"[AngelWindow] Visible Flora: {portraitFrames/portraitSeconds:F1} app FPS, {portraitSlowFrames} frames over 25 ms, worst {portraitWorst*1000:F1} ms");
        }
        if (age>=1 && gesture.Tick(art.Window,eye,left,leftHeld,right,rightHeld))
        {
            since=now;
            stage=Stage.Entering;
            StopScan();
            art.BeginCrossing(surfaces,floorY);
            float playerFloor=surfaces.TryLanding(eye.position,out var foot,out _,out _)?foot.y:floorY;
            var location=new Vector3(eye.position.x,playerFloor,eye.position.z);
            var heading=Vector3.ProjectOnPlane(art.Window.forward,Vector3.up).normalized;
            art.Dust.SetFloorHeight(playerFloor);
            meadow=new StoryMeadow();
            StartCoroutine(meadow.Prepare(art,content,location,heading,Mathf.Max(.20f,eye.position.y-playerFloor)));
            Debug.Log("[AngelWindow] Entering meadow after seating the figure");
        }
        art.PositionPortrait(gesture);
        art.Greet(eye.position,age);
        if(stage==Stage.Greeting)
            art.GuidePlayer(eye,gesture,leftHand.UsingController || rightHand.UsingController,age);
    }

    private void StopScan()
    {
        if(startedScanner && scanner!=null && scanner.running) scanner.Stop();
        startedScanner=false;
    }

    private void OnApplicationPause(bool value)
    {
        paused=value;
        if(value)InvalidateSpace("application paused");
    }

    private void OnOriginUpdated(XRInputSubsystem changed)
    {
        if(changed==input)InvalidateSpace("tracking origin updated");
    }

    private bool TrackingReady()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if(input==null || !input.running)
        {
            if(input!=null)input.trackingOriginUpdated-=OnOriginUpdated;
            SubsystemManager.GetSubsystems(inputs);
            input=inputs.Find(subsystem=>subsystem.running);
            if(input!=null)input.trackingOriginUpdated+=OnOriginUpdated;
        }
        var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
        bool absent=false;
        if(head.TryGetFeatureValue(CommonUsages.userPresence,out var present))
        {
            sawUserPresent|=present;
            absent=sawUserPresent && !present;
        }
        bool tracked=head.TryGetFeatureValue(CommonUsages.trackingState,out var state)
            ? (state & InputTrackingState.Position)!=0
            : head.TryGetFeatureValue(CommonUsages.isTracked,out var valid) && valid;
        if(input==null || absent || !tracked || !head.TryGetFeatureValue(CommonUsages.devicePosition,out var position))
        {
            InvalidateSpace(absent?"headset removed":"head position unavailable");
            return false;
        }
        return AcceptPose(position,Time.realtimeSinceStartupAsDouble);
#else
        return true;
#endif
    }

    private bool AcceptPose(Vector3 position,double now)
    {
        // A large discontinuity in tracking space can accompany a silent relocalization.
        if(havePose && now-previousSample<.12 && (position-previousHead).sqrMagnitude>.8f*.8f)
            InvalidateSpace("head position discontinuity");
        havePose=true;previousHead=position;previousSample=now;
        if(trackingSince<0)trackingSince=now;
        return now-trackingSince>=.25;
    }

    private void InvalidateSpace(string reason)
    {
        havePose=false;trackingSince=-1;
        if(art==null)return;
        resetPending=true;
        Debug.Log($"[AngelWindow] Discarding spatial session: {reason}");
        EndExperience();
    }

    private void EndExperience()
    {
        if(art==null)return;
        StopAllCoroutines();
        PXR_Manager.SpatialMeshDataUpdated-=QueueBlocks;
        StopScan();
        surfaces.Clear();
        seed=null;
#if UNITY_ANDROID && !UNITY_EDITOR
        if(entered) PXR_Manager.EnableVideoSeeThrough=true;
#endif
        content.gameObject.SetActive(false);
        Destroy(content.gameObject);
        leftHand?.Dispose();
        rightHand?.Dispose();
        leftHand=rightHand=null;
        Destroy(handFinish);
        art.Dispose();art=null;meadow=null;gesture=null;content=null;
    }

    private void OnDisable()
    {
        if(input!=null)input.trackingOriginUpdated-=OnOriginUpdated;
        input=null;
        EndExperience();
    }
}
