using UnityEngine;
using StarterAssets;

public class FootstepAudioController : MonoBehaviour
{
        [SerializeField] private FirstPersonController firstPersonController;
		[SerializeField] private AudioSource footstepSource;
		[SerializeField] private AudioClip[] footstepClips;
		[Tooltip("Базовый интервал между шагами при обычной скорости движения")]
		[SerializeField] private float baseInterval = 0.45f;
		[Tooltip("Минимальный интервал между шагами при максимальной скорости")]
		[SerializeField] private float minInterval = 0.2f;

		private float _footstepTimer;

		private void Update()
		{
			if (!CanPlayFootstep())
			{
				_footstepTimer = 0f;
				return;
			}

			_footstepTimer += Time.deltaTime;
			if (_footstepTimer >= CurrentInterval)
			{
				PlayFootstep();
			}
		}

		private bool CanPlayFootstep()
		{
			return firstPersonController != null
				&& footstepSource != null
				&& footstepClips.Length > 0
				&& firstPersonController.Grounded
				&& firstPersonController.IsMoving;
		}

		private float CurrentInterval
		{
			get
			{
				float moveSpeed = Mathf.Max(0.1f, firstPersonController.MoveSpeed);
				float speedRatio = Mathf.Clamp01(firstPersonController.CurrentSpeed / moveSpeed);
				return Mathf.Lerp(baseInterval, minInterval, speedRatio);
			}
		}

		private void PlayFootstep()
		{
			_footstepTimer = 0f;
			int clipIndex = Random.Range(0, footstepClips.Length);
			footstepSource.PlayOneShot(footstepClips[clipIndex]);
		}
}
