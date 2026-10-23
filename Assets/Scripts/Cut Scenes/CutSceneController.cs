using UnityEngine;
using UnityEngine.Playables;

public class CutSceneController : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;

    private void OnEnable()
    {
        if (director == null) return;
        director.played += HandlePlayed;
        director.stopped += HandleStopped;
        if (director.state == PlayState.Playing) LockControls();
    }

    public void LockControls() => PlayerControlLocks.Acquire(this);
    public void UnlockControls() => PlayerControlLocks.Release(this);

    private void HandlePlayed(PlayableDirector source) => LockControls();
    private void HandleStopped(PlayableDirector source) => UnlockControls();

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
            director.played -= HandlePlayed;
            director.stopped -= HandleStopped;
            director.Stop();
        }
        UnlockControls();
    }
}
