using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.XR;

// Use the same tracking origin for PICO's skinned hand and all contact points.
internal sealed class GardenHand
{
    private readonly bool left;
    private readonly GameObject model;
    private readonly System.Collections.Generic.List<Transform> bones;
    private HandJointLocations joints;
    private bool bareHand;
    public Vector3? GripPoint { get; private set; }
    public bool UsingController => !bareHand && GripPoint.HasValue;
    public bool Gripping { get; private set; }
    public Vector3? Index { get; private set; }
    public GardenHand(bool left, Transform origin, Material finish)
    {
        this.left = left;
        var prefab = Resources.Load<GameObject>(left ? "Prefabs/HandLeft" : "Prefabs/HandRight");
        model = Object.Instantiate(prefab, origin, false);
        model.name = left ? "Tracked left hand" : "Tracked right hand";
        var rig=model.GetComponent<PXR_Hand>();
        bones=rig.handJoints;
        rig.enabled=false; // Drive the skin and hit testing from the same joint sample.
        var ray=model.transform.Find("RayPose");
        ray.gameObject.SetActive(false);
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (r.transform.IsChildOf(ray)) r.enabled = false;
            else r.sharedMaterial = finish;
        }
        model.SetActive(false);
    }

    public void Sample()
    {
        Index = null;
        GripPoint = null;
        bool received = false;
#if UNITY_ANDROID && !UNITY_EDITOR
        received = PXR_HandTracking.GetJointLocations(left ? HandType.HandLeft : HandType.HandRight, ref joints);
#endif
        if (ApplyJoints(received,ref joints)) return;
        var device = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
        if ((device.characteristics & InputDeviceCharacteristics.Controller) == 0 ||
            (device.characteristics & InputDeviceCharacteristics.HandTracking) != 0 ||
            !device.TryGetFeatureValue(CommonUsages.isTracked, out bool active) || !active ||
            !device.TryGetFeatureValue(CommonUsages.devicePosition, out var pos)) { Gripping=false;return; }
        if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rot)) pos += rot*Vector3.forward*.055f;
        Index = model.transform.TransformPoint(pos);
        GripPoint=Index;
        device.TryGetFeatureValue(CommonUsages.gripButton,out var grip);
        device.TryGetFeatureValue(CommonUsages.triggerButton,out var trigger);
        Gripping=grip || trigger;

    }

    private bool ApplyJoints(bool received,ref HandJointLocations sample)
    {
        int wrist = (int)HandJoint.JointWrist;
        bareHand = received && sample.isActive != 0 && sample.jointCount >= (int)HandJoint.JointMax &&
            sample.jointLocations?.Length >= (int)HandJoint.JointMax &&
            (sample.jointLocations[wrist].locationStatus & HandLocationStatus.PositionValid) != 0;
        bool visible=bareHand;
        if (model.activeSelf!=visible)model.SetActive(visible);
        if (!bareHand) return false;

        // PICO enumerates the palm before its parent wrist. Set the wrist first.
        for (int n=0;n<(int)HandJoint.JointMax;n++)
        {
            int i=n==0 ? wrist : n==1 ? (int)HandJoint.JointPalm : n;
            var bone=bones[i];
            if (!bone) continue;
            var data=sample.jointLocations[i];
            if ((data.locationStatus & HandLocationStatus.PositionValid)!=0)
                bone.position=model.transform.TransformPoint(data.pose.Position.ToVector3());
            if ((data.locationStatus & HandLocationStatus.OrientationValid)!=0)
                bone.rotation=model.transform.rotation*data.pose.Orientation.ToQuat();
        }

        var tip = sample.jointLocations[(int)HandJoint.JointIndexTip];
        if ((tip.locationStatus & HandLocationStatus.PositionValid) != 0)
            Index = model.transform.TransformPoint(tip.pose.Position.ToVector3());
        var thumb=sample.jointLocations[(int)HandJoint.JointThumbTip];
        if(Index.HasValue && (thumb.locationStatus & HandLocationStatus.PositionValid)!=0)
        {
            var thumbPoint=model.transform.TransformPoint(thumb.pose.Position.ToVector3());
            GripPoint=(thumbPoint+Index.Value)*.5f;
            Gripping=Vector3.Distance(thumbPoint,Index.Value)<(Gripping?.049f:.032f);
        }
        else Gripping=false;
        return true;
    }

    public void Dispose() => Object.Destroy(model);
}
