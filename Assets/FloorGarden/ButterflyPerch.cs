using UnityEngine;

// One visitor per hand; free flight remains the destination when tracking drops.
internal sealed class ButterflyPerch
{
    private enum Flight { Roaming, Approaching, Resting, Leaving }
    private Flight flight;
    private Vector3 lastTip, position, departure;
    private bool hadTip;
    private float steadyTime, elapsed, speed;

    public void Animate(Vector3? tip, float dt, ref Vector3 point, ref Vector3 direction, ref float flap)
    {
        dt = Mathf.Clamp(dt, .001f, .1f);
        if (tip.HasValue)
        {
            float measured = hadTip ? Vector3.Distance(tip.Value, lastTip) / dt : 0;
            speed = Mathf.Lerp(speed, measured, 1 - Mathf.Exp(-12 * dt));
            steadyTime = speed < .20f && hadTip ? steadyTime + dt : 0;
            lastTip = tip.Value;
        }
        else { steadyTime = 0; speed = 0; }
        hadTip = tip.HasValue;
        elapsed += dt;

        if (flight == Flight.Roaming)
        {
            position = point;
            if (steadyTime < .45f) return;
            flight = Flight.Approaching;
            elapsed = 0;
        }
        var target = lastTip + Vector3.up * .013f;
        if ((flight == Flight.Approaching || flight == Flight.Resting) && (!hadTip || speed > .74f))
        {
            flight = Flight.Leaving;
            elapsed = 0;
            departure = position;
            steadyTime = 0;
        }
        var previous = position;
        switch (flight)
        {
            case Flight.Approaching:
                float distance = Vector3.Distance(target, position);
                float arc = Mathf.Clamp01(distance / .32f);
                var flutter = new Vector3(.045f*Mathf.Sin(5*elapsed), .06f+.03f*Mathf.Cos(4*elapsed), 0) * arc;
                position = Vector3.MoveTowards(position, target + flutter, dt * Mathf.Clamp(distance * 2.6f,.22f,1.8f));
                if (distance < .022f) { flight = Flight.Resting; elapsed = 0; }
                break;
            case Flight.Resting:
                position = target;
                flap = 54 + 11 * Mathf.Sin(elapsed * 2.7f);
                direction = Vector3.forward;
                break;
            case Flight.Leaving:
                float t = Mathf.Clamp01(elapsed / 1.15f);
                position = Vector3.Lerp(departure, point, Mathf.SmoothStep(0, 1, t)) +
                           Vector3.up * (.23f * Mathf.Sin(t * Mathf.PI));
                if (t >= 1) { flight = Flight.Roaming; steadyTime = 0; }
                break;
        }
        if (flight != Flight.Resting && (position - previous).sqrMagnitude > .000001f)
            direction = position - previous;
        point = position;
    }
}
