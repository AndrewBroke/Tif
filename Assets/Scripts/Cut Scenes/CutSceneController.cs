using UnityEngine;
using UnityEngine.Playables;
using StarterAssets;

public class CutSceneController : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;

    // Optional Timeline rig. Existing cutscenes leave these references empty.
    [SerializeField] private Transform cutsceneOrigin;
    [SerializeField] private Transform cutsceneCamera;
    [SerializeField] private Transform cutsceneLookTarget;
    [SerializeField] private FirstPersonController player;
    private bool gameplayCameraAligned;

    private void OnEnable()
    {
        if (director == null) return;
        director.played += HandlePlayed;
        director.stopped += HandleStopped;
        if (director.state == PlayState.Playing) LockControls();
    }

    public void LockControls() => PlayerControlLocks.Acquire(this);
    public void UnlockControls() => PlayerControlLocks.Release(this);

    private void HandlePlayed(PlayableDirector source)
    {
        LockControls();
        gameplayCameraAligned = false;
        if (cutsceneOrigin == null || cutsceneLookTarget == null || Camera.main == null) return;
        Vector3 position = Camera.main.transform.position;
        Vector3 direction = Vector3.ProjectOnPlane(cutsceneLookTarget.position - position, Vector3.up);
        cutsceneOrigin.SetPositionAndRotation(position,
            direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction) : Quaternion.identity);
    }

    // Called by a Timeline signal before the Cinemachine shot blends out.
    public void AlignGameplayCamera()
    {
        if (gameplayCameraAligned || player == null || cutsceneCamera == null) return;
        player.SetLookRotation(cutsceneCamera.rotation);
        gameplayCameraAligned = true;
    }

    private void HandleStopped(PlayableDirector source)
    {
        AlignGameplayCamera();
        UnlockControls();
    }

    private void Update()
    {
        // Hold timelines can keep their last frame without a stopped notification.
        if (director == null ||
            (director.extrapolationMode == DirectorWrapMode.Hold && director.time >= director.duration))
            UnlockControls();
    }

    public void StartCutScene(PlayableAsset cutScene)
    {
        if (!isActiveAndEnabled || director == null || cutScene == null) return;
        // Replacing a running timeline may invoke stopped; acquire after replacement.
        director.Stop();
        director.playableAsset = cutScene;
        LockControls();
        try
        {
            director.Play();
        }
        catch
        {
            UnlockControls();
            throw;
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            if (director.state == PlayState.Playing) AlignGameplayCamera();
            director.played -= HandlePlayed;
            director.stopped -= HandleStopped;
            director.Stop();
        }
        UnlockControls();
    }
}
