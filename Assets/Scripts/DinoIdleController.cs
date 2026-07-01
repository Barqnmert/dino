using System.Collections;
using UnityEngine;

// Drives Dinos' idle behaviour purely through code-driven bone transforms and blend shapes -
// no external animation clips. Breathing runs continuously; stretch/wave/smile/jump fire on a
// random timer or immediately on tap, never overlapping each other.
//
// Bone rotations are applied in WORLD space, pivoting around the character root's own right/
// forward/up axes rather than each bone's local axes, because the Blender -> FBX -> Unity
// import pipeline does not preserve a predictable local axis convention per bone. The arm's
// "outward" side is also detected from its actual world position at startup (rather than
// assumed) so the wave always swings away from the body regardless of which way the FBX
// import happened to orient left/right.
public class DinoIdleController : MonoBehaviour
{
    [Header("Bone References (auto-found by name if left empty)")]
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform upperArmR;

    [Header("Face (auto-found if left empty)")]
    [SerializeField] private SkinnedMeshRenderer faceRenderer;

    [Header("Growth Stage")]
    [Tooltip("Level 1 baby ~0.3, Level 10 adult ~1.0. Swap this per growth stage later.")]
    [SerializeField] private float characterScale = 0.6f;

    [Header("Breathing")]
    [SerializeField] private float breatheDuration = 2.5f;
    [SerializeField] private float breatheScaleAmount = 0.05f;

    [Header("Random Idle Timing")]
    [SerializeField] private float idleIntervalMin = 5f;
    [SerializeField] private float idleIntervalMax = 12f;

    private Vector3 spineBaseScale = Vector3.one;
    private Quaternion headBaseWorldRot;
    private Quaternion armRBaseWorldRot;
    private Vector3 bodyBaseScale;
    private Vector3 bodyBaseLocalPos;
    private float armRSideSign = 1f; // +1 if the arm sits on the root's +right side, -1 otherwise

    private int smileBlendIndex = -1;
    private int eyesWideBlendIndex = -1;

    private bool isPlayingIdleAction;
    private Coroutine triggeredActionRoutine;

    private void Awake()
    {
        if (!spine) spine = FindDeepChild(transform, "Spine");
        if (!head) head = FindDeepChild(transform, "Head");
        if (!upperArmR) upperArmR = FindDeepChild(transform, "UpperArm_R");
        if (!faceRenderer) faceRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

        transform.localScale = Vector3.one * characterScale;
        bodyBaseScale = transform.localScale;
        bodyBaseLocalPos = transform.localPosition;

        if (spine) spineBaseScale = spine.localScale;
        if (head) headBaseWorldRot = head.rotation;
        if (upperArmR)
        {
            armRBaseWorldRot = upperArmR.rotation;
            Vector3 toArm = upperArmR.position - transform.position;
            armRSideSign = Mathf.Sign(Vector3.Dot(toArm, transform.right));
            if (armRSideSign == 0f) armRSideSign = 1f;
        }

        if (faceRenderer && faceRenderer.sharedMesh)
        {
            smileBlendIndex = faceRenderer.sharedMesh.GetBlendShapeIndex("Smile");
            eyesWideBlendIndex = faceRenderer.sharedMesh.GetBlendShapeIndex("EyesWide");
        }
    }

    private void Start()
    {
        StartCoroutine(BreathingLoop());
        StartCoroutine(RandomIdleLoop());
    }

    private void OnMouseDown()
    {
        TriggerIdleActionNow();
    }

    public void TriggerIdleActionNow()
    {
        if (isPlayingIdleAction) return;
        if (triggeredActionRoutine != null) StopCoroutine(triggeredActionRoutine);
        triggeredActionRoutine = StartCoroutine(PlayRandomIdleAction());
    }

    private IEnumerator BreathingLoop()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            if (spine && !isPlayingIdleAction)
            {
                float phase = (t / breatheDuration) * Mathf.PI * 2f;
                float scaleOffset = (Mathf.Sin(phase) * 0.5f + 0.5f) * breatheScaleAmount;
                spine.localScale = spineBaseScale * (1f + scaleOffset);
            }
            yield return null;
        }
    }

    private IEnumerator RandomIdleLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(idleIntervalMin, idleIntervalMax));
            if (!isPlayingIdleAction)
                yield return StartCoroutine(PlayRandomIdleAction());
        }
    }

    private IEnumerator PlayRandomIdleAction()
    {
        isPlayingIdleAction = true;
        if (spine) spine.localScale = spineBaseScale; // clean baseline before an action takes over

        switch (Random.Range(0, 4))
        {
            case 0: yield return Stretch(); break;
            case 1: yield return Wave(); break;
            case 2: yield return Smile(); break;
            default: yield return Jump(); break;
        }
        isPlayingIdleAction = false;
    }

    private void SetBlendShape(int index, float weight01)
    {
        if (faceRenderer && index >= 0)
            faceRenderer.SetBlendShapeWeight(index, Mathf.Clamp01(weight01) * 100f);
    }

    private IEnumerator Stretch()
    {
        if (!head) yield break;
        const float duration = 1.4f;
        const float maxTiltBackDegrees = 30f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float easeOutAndBack = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            head.rotation = Quaternion.AngleAxis(-easeOutAndBack * maxTiltBackDegrees, transform.right) * headBaseWorldRot;
            yield return null;
        }
        head.rotation = headBaseWorldRot;
    }

    private IEnumerator Wave()
    {
        if (!upperArmR) yield break;
        const float raiseDuration = 0.4f;
        const float waveDuration = 1.2f;
        const float lowerDuration = 0.4f;
        const float raisedAngle = 100f;
        const float headTurnDegrees = 18f;

        float t = 0f;
        while (t < raiseDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / raiseDuration);
            ApplyArmRaise(Mathf.Lerp(0f, raisedAngle, p), 0f);
            ApplyHeadTowardArm(p);
            SetBlendShape(smileBlendIndex, p);
            yield return null;
        }

        t = 0f;
        while (t < waveDuration)
        {
            t += Time.deltaTime;
            float wiggle = Mathf.Sin(t * 10f) * 15f;
            ApplyArmRaise(raisedAngle, wiggle);
            ApplyHeadTowardArm(1f);
            yield return null;
        }

        t = 0f;
        while (t < lowerDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / lowerDuration);
            ApplyArmRaise(Mathf.Lerp(raisedAngle, 0f, p), 0f);
            ApplyHeadTowardArm(1f - p);
            SetBlendShape(smileBlendIndex, 1f - p);
            yield return null;
        }
        upperArmR.rotation = armRBaseWorldRot;
        if (head) head.rotation = headBaseWorldRot;
        SetBlendShape(smileBlendIndex, 0f);
    }

    // Raises the arm out to the character's own side (using the pre-computed side sign so it
    // always swings away from the body, never into the head) then wiggles it for the wave.
    private void ApplyArmRaise(float raiseDegrees, float wiggleDegrees)
    {
        Quaternion raise = Quaternion.AngleAxis(armRSideSign * raiseDegrees, transform.forward);
        Quaternion wiggle = Quaternion.AngleAxis(wiggleDegrees, transform.up);
        upperArmR.rotation = wiggle * raise * armRBaseWorldRot;
    }

    // Turns/tilts the head toward the raised arm so the arm visually reads as a greeting
    // gesture rather than clipping into the face.
    private void ApplyHeadTowardArm(float p)
    {
        if (!head) return;
        Quaternion turn = Quaternion.AngleAxis(armRSideSign * 18f * p, transform.up);
        Quaternion tiltDown = Quaternion.AngleAxis(-8f * p, transform.right);
        head.rotation = turn * tiltDown * headBaseWorldRot;
    }

    private IEnumerator Smile()
    {
        const float inDuration = 0.35f;
        const float holdDuration = 1.0f;
        const float outDuration = 0.4f;
        float t = 0f;
        while (t < inDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / inDuration);
            SetBlendShape(smileBlendIndex, p);
            SetBlendShape(eyesWideBlendIndex, p * 0.6f);
            yield return null;
        }
        yield return new WaitForSeconds(holdDuration);
        t = 0f;
        while (t < outDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / outDuration);
            SetBlendShape(smileBlendIndex, 1f - p);
            SetBlendShape(eyesWideBlendIndex, (1f - p) * 0.6f);
            yield return null;
        }
        SetBlendShape(smileBlendIndex, 0f);
        SetBlendShape(eyesWideBlendIndex, 0f);
    }

    // A proper squash-and-stretch jump: crouch down to gather force (anticipation), spring
    // up off the ground with a stretched pose, hang briefly with wide excited eyes, then land
    // and squash back down before settling.
    private IEnumerator Jump()
    {
        const float crouchDuration = 0.18f;
        const float launchDuration = 0.16f;
        const float airDuration = 0.35f;
        const float landDuration = 0.22f;
        const float settleDuration = 0.15f;

        const float crouchSquash = 0.22f;   // wider + shorter while gathering force
        const float launchStretch = 0.28f;  // taller + thinner while springing up
        const float jumpHeight = 0.18f;
        const float landSquash = 0.18f;

        float t = 0f;

        // Anticipation: squash down.
        while (t < crouchDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / crouchDuration);
            ApplyBodySquashStretch(-crouchSquash * p, 0f);
            yield return null;
        }

        // Launch: stretch tall and lift off.
        t = 0f;
        while (t < launchDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / launchDuration);
            ApplyBodySquashStretch(Mathf.Lerp(-crouchSquash, launchStretch, p), jumpHeight * p);
            SetBlendShape(eyesWideBlendIndex, p);
            yield return null;
        }

        // Airborne hang.
        t = 0f;
        while (t < airDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / airDuration);
            float hangHeight = jumpHeight + Mathf.Sin(p * Mathf.PI) * 0.05f;
            ApplyBodySquashStretch(Mathf.Lerp(launchStretch, 0.05f, p), hangHeight);
            yield return null;
        }

        // Landing: squash down on impact.
        t = 0f;
        while (t < landDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / landDuration);
            ApplyBodySquashStretch(Mathf.Lerp(0.05f, -landSquash, p), Mathf.Lerp(jumpHeight * 0.2f, 0f, p));
            SetBlendShape(eyesWideBlendIndex, 1f - p);
            yield return null;
        }

        // Settle back to normal.
        t = 0f;
        while (t < settleDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / settleDuration);
            ApplyBodySquashStretch(Mathf.Lerp(-landSquash, 0f, p), 0f);
            yield return null;
        }

        transform.localScale = bodyBaseScale;
        transform.localPosition = bodyBaseLocalPos;
        SetBlendShape(eyesWideBlendIndex, 0f);
    }

    // squashStretch > 0 stretches taller/thinner, < 0 squashes wider/shorter. height lifts the
    // whole character off the ground for the jump arc.
    private void ApplyBodySquashStretch(float squashStretch, float height)
    {
        float vertical = 1f + squashStretch;
        float horizontal = 1f - squashStretch * 0.6f;
        transform.localScale = new Vector3(bodyBaseScale.x * horizontal, bodyBaseScale.y * vertical, bodyBaseScale.z * horizontal);
        transform.localPosition = bodyBaseLocalPos + new Vector3(0f, height, 0f);
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDeepChild(child, name);
            if (found) return found;
        }
        return null;
    }
}
