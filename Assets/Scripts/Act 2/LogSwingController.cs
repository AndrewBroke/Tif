
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class LogSwingController : MonoBehaviour
{
    [System.Serializable]
    public class SwingSound
    {
        [Range(0f, 100f)] public float percent;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Header("Swing sounds")]
    public AudioSource soundSource;
    public SwingSound[] swingSounds = new SwingSound[0];

    [Header("Время анимации")]
    [Min(0.01f)] public float windupTime = 0.6f;
    [Min(0.01f)] public float hitTime = 0.15f;
    [Min(0.01f)] public float returnTime = 0.5f;

    [Header("Паузы")]
    [Min(0f)] public float pauseBeforeHit = 0.1f;
    [Min(0f)] public float pauseAfterHit = 0.1f;

    [Header("Поворот полена")]
    public Vector3 windupRotation = new Vector3(-60, 0, 15);
    public Vector3 hitRotation = new Vector3(50, 0, -10);

    [Header("Смещение полена")]
    public Vector3 windupOffset = new Vector3(0, 0, -0.1f);
    public Vector3 hitOffset = new Vector3(0, 0, 0.2f);

    [Header("Плавность движения")]
    public AnimationCurve windupCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    public AnimationCurve hitCurve =
        AnimationCurve.Linear(0, 0, 1, 1);

    public AnimationCurve returnCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("События")]
    public UnityEvent onHit;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private Coroutine currentAnimation;
    private bool[] playedSounds;
    private float swingElapsed;
    private float swingDuration;

    public bool IsPlaying => currentAnimation != null;
    public event System.Action<bool> Finished;

    private void Awake()
    {
        startPosition = transform.localPosition;
        startRotation = transform.localRotation;
        if (soundSource == null)
            soundSource = GetComponent<AudioSource>();
    }

    public void PlaySwing()
    {
        if (currentAnimation != null)
        {
            StopSwing();
        }

        ResetPose();
        currentAnimation = StartCoroutine(SwingRoutine());
    }

    private IEnumerator SwingRoutine()
    {
        swingElapsed = 0f;
        swingDuration = Mathf.Max(0f, windupTime) + Mathf.Max(0f, pauseBeforeHit)
            + Mathf.Max(0f, hitTime) + Mathf.Max(0f, pauseAfterHit)
            + Mathf.Max(0f, returnTime);
        playedSounds = new bool[swingSounds.Length];
        PlaySoundsAtPercent(0f);

        Quaternion windupRot = startRotation *
            Quaternion.Euler(windupRotation);

        Quaternion hitRot = startRotation *
            Quaternion.Euler(hitRotation);

        Vector3 windupPos = startPosition + windupOffset;
        Vector3 hitPos = startPosition + hitOffset;

        // 1. Замах
        yield return Animate(
            startPosition, windupPos,
            startRotation, windupRot,
            windupTime, windupCurve
        );

        // Пауза перед ударом
        if (pauseBeforeHit > 0)
            yield return Pause(pauseBeforeHit);

        // 2. Удар
        yield return Animate(
            windupPos, hitPos,
            windupRot, hitRot,
            hitTime, hitCurve
        );

        // Момент удара
        onHit?.Invoke();

        // Пауза после удара
        if (pauseAfterHit > 0)
            yield return Pause(pauseAfterHit);

        // 3. Возврат
        yield return Animate(
            hitPos, startPosition,
            hitRot, startRotation,
            returnTime, returnCurve
        );

        PlaySoundsAtPercent(100f);
        ResetPose();
        currentAnimation = null;
        Finished?.Invoke(true);
    }

    private IEnumerator Animate(
        Vector3 fromPos,
        Vector3 toPos,
        Quaternion fromRot,
        Quaternion toRot,
        float duration,
        AnimationCurve curve)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float step = Mathf.Min(Time.deltaTime, duration - elapsed);
            elapsed += step;

            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = curve.Evaluate(t);

            transform.localPosition =
                Vector3.LerpUnclamped(fromPos, toPos, smoothT);

            transform.localRotation =
                Quaternion.SlerpUnclamped(fromRot, toRot, smoothT);

            AdvanceSoundTimeline(step);
            yield return null;
        }

        transform.localPosition = toPos;
        transform.localRotation = toRot;
    }

    private IEnumerator Pause(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float step = Mathf.Min(Time.deltaTime, duration - elapsed);
            elapsed += step;
            AdvanceSoundTimeline(step);
            yield return null;
        }
    }

    private void AdvanceSoundTimeline(float deltaTime)
    {
        swingElapsed += deltaTime;
        float percent = swingDuration > 0f ? swingElapsed / swingDuration * 100f : 100f;
        PlaySoundsAtPercent(percent);
    }

    private void PlaySoundsAtPercent(float percent)
    {
        for (int i = 0; i < playedSounds.Length; i++)
        {
            SwingSound sound = swingSounds[i];
            if (playedSounds[i] || sound == null || percent < sound.percent)
                continue;

            playedSounds[i] = true;
            if (soundSource != null && sound.clip != null)
                soundSource.PlayOneShot(sound.clip, sound.volume);
        }
    }

    public void ResetPose()
    {
        transform.localPosition = startPosition;
        transform.localRotation = startRotation;
    }

    public void StopSwing()
    {
        bool wasPlaying = currentAnimation != null;
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }

        ResetPose();
        if (wasPlaying)
            Finished?.Invoke(false);
    }

    private void OnDisable()
    {
        StopSwing();
    }

    void Update()
    {
        // if(Keyboard.current.pKey.wasPressedThisFrame)
        // {
        //     PlaySwing();
        // }
    }
}
