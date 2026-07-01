using System.Collections;
using UnityEngine;

// Drives Dinos' idle behaviour purely through code-driven bone transforms - no external
// animation clips. Breathing runs continuously; stretch/wave/happy-bounce fire on a random
// timer or immediately on tap, never overlapping each other.
//
// Rotations are applied in WORLD space, pivoting around the character root's own right/
// forward/up axes (transform.right/forward/up) rather than each bone's local axes. The
// Blender -> FBX -> Unity import pipeline does not preserve a predictable local axis
// convention per bone, so local-space Euler rotations produced wrong-looking results
// (arms swinging backward/inward, head "stretching" instead of tilting). World-space
// rotation relative to the character root's own orientation sidesteps that entirely.
public class DinoIdleController : MonoBehaviour
{
    [Header("Bone References (auto-found by name if left empty)")]
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform upperArmR;

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

    private bool isPlayingIdleAction;
    private Coroutine triggeredActionRoutine;

    private void Awake()
    {
        if (!spine) spine = FindDeepChild(transform, "Spine");
        if (!head) head = FindDeepChild(transform, "Head");
        if (!upperArmR) upperArmR = FindDeepChild(transform, "UpperArm_R");

        transform.localScale = Vector3.one * characterScale;
        bodyBaseScale = transform.localScale;

        if (spine) spineBaseScale = spine.localScale;
        if (head) headBaseWorldRot = head.rotation;
        if (upperArmR) armRBaseWorldRot = upperArmR.rotation;
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
        if (spine) spine.localScale = spineBaseScale; // clean baseline before overriding with an action

        switch (Random.Range(0, 3))
        {
            case 0: yield return Stretch(); break;
            case 1: yield return Wave(); break;
            default: yield return HappyBounce(); break;
        }
        isPlayingIdleAction = false;
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
            // Tilt the head backward around the character's own left-right axis.
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

        float t = 0f;
        while (t < raiseDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / raiseDuration);
            ApplyArmRaise(Mathf.Lerp(0f, raisedAngle, p), 0f);
            yield return null;
        }

        t = 0f;
        while (t < waveDuration)
        {
            t += Time.deltaTime;
            float wiggle = Mathf.Sin(t * 10f) * 15f;
            ApplyArmRaise(raisedAngle, wiggle);
            yield return null;
        }

        t = 0f;
        while (t < lowerDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / lowerDuration);
            ApplyArmRaise(Mathf.Lerp(raisedAngle, 0f, p), 0f);
            yield return null;
        }
        upperArmR.rotation = armRBaseWorldRot;
    }

    // Raises the right arm out to the side by rotating around the character's own forward
    // axis (lifts the arm within the left-right/up-down plane), then wiggles it around the
    // character's up axis for the side-to-side wave motion.
    private void ApplyArmRaise(float raiseDegrees, float wiggleDegrees)
    {
        Quaternion raise = Quaternion.AngleAxis(-raiseDegrees, transform.forward);
        Quaternion wiggle = Quaternion.AngleAxis(wiggleDegrees, transform.up);
        upperArmR.rotation = wiggle * raise * armRBaseWorldRot;
    }

    private IEnumerator HappyBounce()
    {
        const float duration = 0.5f;
        const float bounceAmount = 0.12f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float curve = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI) * bounceAmount;
            transform.localScale = bodyBaseScale * (1f + curve);
            yield return null;
        }
        transform.localScale = bodyBaseScale;
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
