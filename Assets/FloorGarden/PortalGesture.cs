using UnityEngine;

// A distant pinch turns the figure. Up close, move it into the outline and release.
internal sealed class PortalGesture
{
    public static readonly Vector3 StartingOffset = new(-.08f,.025f,-.22f);
    public Vector3 Offset { get; private set; } = StartingOffset;
    public float Angle { get; private set; }
    public bool Aligned { get; private set; }
    public bool Near { get; private set; }
    public bool InReach { get; private set; }
    public bool Placing => placing && owner>=0;
    public bool Inspecting => !placing && owner>=0;
    private bool Reachable(Vector3 relative) =>
        Mathf.Abs(relative.x)<.40f && Mathf.Abs(relative.y)<.67f && relative.z>-.40f && relative.z<.22f;
    private int owner = -1;
    private bool leftWasHeld, rightWasHeld, placing;
    private Vector3 startHand, startOffset;
    private float startAngle;

    public bool Tick(Transform window, Transform eye, Vector3? left, bool leftHeld,
                     Vector3? right, bool rightHeld)
    {
        bool leftDown=leftHeld && !leftWasHeld, rightDown=rightHeld && !rightWasHeld;
        leftWasHeld=leftHeld;rightWasHeld=rightHeld;
        var viewer=window.InverseTransformPoint(eye.position);
        bool near=viewer.z<0 && viewer.z> -1.15f && Mathf.Abs(viewer.x)<.95f;
        Near=near;
        InReach=near && ((left.HasValue && Reachable(window.InverseTransformPoint(left.Value)-Offset))
                     || (right.HasValue && Reachable(window.InverseTransformPoint(right.Value)-Offset)));
        if (owner<0)
        {
            if (rightDown && right.HasValue) owner=1;
            else if (leftDown && left.HasValue) owner=0;
            else return false;
            startHand=window.InverseTransformPoint((owner==0?left:right).Value);
            Vector3 relative=startHand-Offset;
            placing=near && Reachable(relative);
            if (!placing && !LooksIntoWindow(window,eye)) { owner=-1;return false; }
            startOffset=Offset;startAngle=Angle;
        }
        var hand=owner==0?left:right;
        if (!hand.HasValue) { owner=-1;Aligned=false;return false; }
        Vector3 delta=window.InverseTransformPoint(hand.Value)-startHand;
        if (placing)
        {
            Vector3 p=startOffset+1.25f*delta;
            Offset=new Vector3(Mathf.Clamp(p.x,-.36f,.36f),Mathf.Clamp(p.y,-.32f,.32f),Mathf.Clamp(p.z,-.50f,.035f));
            // A magnetic socket brings the yaw into register as the figure approaches the frame.
            Angle=startAngle*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.055f,.18f,Offset.magnitude));
            Aligned=near && Offset.sqrMagnitude<.075f*.075f && Mathf.Abs(Angle)<9;
        }
        // The portrait faces local -Z, so dragging its face right needs negative yaw.
        else Angle=Mathf.Clamp(startAngle-delta.x*120f,-23f,23f);
        if (owner==0?leftHeld:rightHeld) return false;
        owner=-1;
        bool solved=placing && Aligned;
        Aligned=false;
        if (solved) {Offset=Vector3.zero;Angle=0;}
        return solved;
    }

    private static bool LooksIntoWindow(Transform window, Transform eye)
    {
        var ray=new Ray(eye.position,eye.forward);
        if(Vector3.Dot(eye.forward,window.forward)<=0 ||
           !new Plane(-window.forward,window.position).Raycast(ray,out float d))return false;
        var p=window.InverseTransformPoint(ray.GetPoint(d));
        return p.x*p.x/(.53f*.53f)+p.y*p.y/(.78f*.78f)<=1;
    }
}
