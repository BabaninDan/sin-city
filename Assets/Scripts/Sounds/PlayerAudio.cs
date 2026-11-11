using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerAudio : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private AudioClip[] footstepSounds;

    [Header("Actions")]
    [SerializeField] private AudioClip dashSound;
    [SerializeField] private AudioClip doubleJumpSound;
    [SerializeField] private AudioClip landingSound;
    [SerializeField] private AudioClip shootSound;

    [Header("Settings")]
    [SerializeField] private float footstepVolume = 0.5f;
    [SerializeField] private float dashVolume = 0.8f;
    [SerializeField] private float doubleJumpVolume = 0.7f;
    [SerializeField] private float landingVolume = 0.7f;
    [SerializeField] private float shootVolume = 0.8f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
    }

    public void PlayFootstep()
    {
        if (footstepSounds == null || footstepSounds.Length == 0)
            return;

        AudioClip clip = footstepSounds[
            Random.Range(0, footstepSounds.Length)
        ];

        audioSource.PlayOneShot(clip, footstepVolume);
    }

    public void PlayDash()
    {
        PlaySound(dashSound, dashVolume);
    }

    public void PlayDoubleJump()
    {
        PlaySound(doubleJumpSound, doubleJumpVolume);
    }

    public void PlayLanding()
    {
        PlaySound(landingSound, landingVolume);
    }

    public void PlayShoot()
    {
        PlaySound(shootSound, shootVolume);
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        audioSource.PlayOneShot(clip, volume);
    }
}