using System;
using UnityEngine;

namespace TutorialKit
{
    /// <summary>
    /// Per-tutorial playback configuration, stored on the <see cref="TutorialGraph"/> and edited in its
    /// inspector (or the graph editor's Tutorial Settings panel). Controls how often a tutorial may play,
    /// what happens if it's triggered while another one is running, and what the game does while it plays.
    /// Enforced by <see cref="TutorialDirector"/>, so every entry point (trigger, code, remote) obeys it.
    /// </summary>
    [Serializable]
    public sealed class TutorialSettings
    {
        [Tooltip("How often this tutorial may play.\n\n" +
                 "Single Use — plays once ever; completion is saved (onboarding step).\n" +
                 "Recurring — plays every time it's triggered; nothing is saved (a hint).\n" +
                 "Once Per Session — plays once per app run, again after a restart.")]
        [SerializeField] private TutorialPlayMode playMode = TutorialPlayMode.SingleUse;

        [Tooltip("Recurring only. Stop playing after this many completed plays. 0 = unlimited.")]
        [SerializeField, Min(0)] private int maxPlays = 0;

        [Tooltip("Recurring only. Minimum real-time seconds between plays; earlier triggers are ignored. " +
                 "Measured from the end of the last play and saved, so it survives a restart. 0 = no cooldown.")]
        [SerializeField, Min(0f)] private float cooldownSeconds = 0f;

        [Tooltip("What to do when this tutorial is triggered while another one is already playing.\n\n" +
                 "Interrupt — abort the other one and start now.\n" +
                 "Ignore — drop this request.\n" +
                 "Queue — start as soon as the other one finishes.")]
        [SerializeField] private TutorialBusyPolicy whenBusy = TutorialBusyPolicy.Interrupt;

        [Tooltip("Lock game input (via IInputLockService) for the whole tutorial, and unlock when it ends. " +
                 "Individual Lock Input nodes still work on top of this.")]
        [SerializeField] private bool lockInputWhilePlaying = false;

        [Tooltip("Which named lock group to hold. Empty = the global lock.")]
        [SerializeField] private string inputLockGroup = "";

        [Tooltip("Set Time.timeScale to 0 for the whole tutorial and restore it when the tutorial ends. " +
                 "Overlay animations are unscaled, so they still run.")]
        [SerializeField] private bool pauseGameWhilePlaying = false;

        [Tooltip("Allow TutorialHandle.Skip() to cut this tutorial short. Turn off for a mandatory tutorial.")]
        [SerializeField] private bool allowSkip = true;

        /// <summary>How often this tutorial may play.</summary>
        public TutorialPlayMode PlayMode { get => playMode; set => playMode = value; }

        /// <summary>Recurring only: stop after this many completed plays (0 = unlimited).</summary>
        public int MaxPlays { get => maxPlays; set => maxPlays = Mathf.Max(0, value); }

        /// <summary>Recurring only: minimum real seconds between plays (0 = none).</summary>
        public float CooldownSeconds { get => cooldownSeconds; set => cooldownSeconds = Mathf.Max(0f, value); }

        /// <summary>What to do when triggered while another tutorial is running.</summary>
        public TutorialBusyPolicy WhenBusy { get => whenBusy; set => whenBusy = value; }

        /// <summary>Hold an input lock for the whole tutorial.</summary>
        public bool LockInputWhilePlaying { get => lockInputWhilePlaying; set => lockInputWhilePlaying = value; }

        /// <summary>Lock group used by <see cref="LockInputWhilePlaying"/>. Null/empty = the global lock.</summary>
        public string InputLockGroup
        {
            get => string.IsNullOrEmpty(inputLockGroup) ? null : inputLockGroup;
            set => inputLockGroup = value;
        }

        /// <summary>Zero <see cref="Time.timeScale"/> for the duration of the tutorial.</summary>
        public bool PauseGameWhilePlaying { get => pauseGameWhilePlaying; set => pauseGameWhilePlaying = value; }

        /// <summary>Whether <see cref="TutorialHandle.Skip"/> is honoured.</summary>
        public bool AllowSkip { get => allowSkip; set => allowSkip = value; }

        /// <summary>True when completion should be written to persistence (Single Use only).</summary>
        public bool PersistsCompletion => playMode == TutorialPlayMode.SingleUse;

        public TutorialSettings Clone() => (TutorialSettings)MemberwiseClone();
    }
}
