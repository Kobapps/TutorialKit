namespace TutorialKit
{
    /// <summary>Shape of a vignette highlight hole.</summary>
    public enum HighlightShape
    {
        Circle = 0,
        Rectangle = 1,
    }

    /// <summary>Visual style of the pointer.</summary>
    public enum PointerKind
    {
        Hand = 0,
        Arrow = 1,
    }

    /// <summary>A tutorial's preferred graph-editor flow direction (editor-only; stored per graph).</summary>
    public enum TutorialLayoutDirection
    {
        /// <summary>Follow the project default (Settings ▸ TutorialKit).</summary>
        UseDefault = 0,
        /// <summary>Flow left → right.</summary>
        Horizontal = 1,
        /// <summary>Flow top → bottom (VFX-graph style).</summary>
        Vertical = 2,
    }

    /// <summary>Animated gesture a pointer performs.</summary>
    public enum PointerGesture
    {
        /// <summary>Idle bob over a single target.</summary>
        Point = 0,
        /// <summary>Repeated tap/press over a single target.</summary>
        Tap = 1,
        /// <summary>Quick flick from A to B.</summary>
        Swipe = 2,
        /// <summary>Press-and-move from A to B (slower, "held").</summary>
        Drag = 3,
        /// <summary>Two elements moving together into one point.</summary>
        Merge = 4,
    }

    public enum InputLockMode
    {
        Lock = 0,
        Unlock = 1,
    }

    /// <summary>How a text box is positioned on screen.</summary>
    public enum TextBoxPlacement
    {
        /// <summary>Use an explicit normalized screen position (0..1).</summary>
        Custom = 0,
        Top = 1,
        Center = 2,
        Bottom = 3,
        /// <summary>Above the resolved target.</summary>
        AboveTarget = 4,
        /// <summary>Below the resolved target.</summary>
        BelowTarget = 5,
    }

    /// <summary>Kinds of player input a wait node can block on.</summary>
    public enum WaitInputKind
    {
        AnyInput = 0,
        PointerDown = 1,
        TapOnTarget = 2,
    }

    /// <summary>Lifecycle state of a running tutorial.</summary>
    public enum TutorialStatus
    {
        Idle = 0,
        Running = 1,
        Completed = 2,
        Skipped = 3,
        Aborted = 4,
        Faulted = 5,
    }

    /// <summary>When a <see cref="TutorialKit.TutorialTrigger"/> starts its graph.</summary>
    public enum TutorialStartMode
    {
        OnEnable = 0,
        OnStart = 1,
        Manual = 2,
        OnSignal = 3,
    }

    /// <summary>How often a tutorial is allowed to play. See <see cref="TutorialKit.TutorialSettings"/>.</summary>
    public enum TutorialPlayMode
    {
        /// <summary>Plays once ever. Completion is saved, so it never plays again (classic onboarding step).</summary>
        SingleUse = 0,
        /// <summary>Plays every time it's triggered. Completion is never saved (a recurring hint/reminder).</summary>
        Recurring = 1,
        /// <summary>Plays once per app session, then again after a restart. Nothing is saved to disk.</summary>
        OncePerSession = 2,
    }

    /// <summary>What happens when a tutorial is started while another one is already playing.</summary>
    public enum TutorialBusyPolicy
    {
        /// <summary>Abort the running tutorial and start this one immediately.</summary>
        Interrupt = 0,
        /// <summary>Drop this request; the running tutorial keeps going.</summary>
        Ignore = 1,
        /// <summary>Wait in line and start as soon as the running tutorial finishes.</summary>
        Queue = 2,
    }
}
