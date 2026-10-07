namespace Inferno.Core.Commands;

/// <summary>The <c>!fires</c> sub-commands.</summary>
public enum CommandKind
{
    /// <summary><c>!fires help</c></summary>
    Help,

    /// <summary><c>!fires status</c> — is Inferno running, and what does it see (for remote testing).</summary>
    Status,

    /// <summary><c>!fires list [all|lights|stations]</c></summary>
    List,

    /// <summary><c>!fires show &lt;target&gt;</c></summary>
    Show,

    /// <summary><c>!fires alwayson &lt;target&gt; on|off</c></summary>
    AlwaysOn,

    /// <summary><c>!fires burnrate &lt;target&gt; &lt;-10..10&gt;</c></summary>
    BurnRate,

    /// <summary><c>!fires schedule &lt;target&gt; &lt;on HH:MM&gt; &lt;off HH:MM&gt;</c> or <c>… off</c></summary>
    Schedule,

    /// <summary><c>!fires smoke &lt;target&gt; on|off</c></summary>
    Smoke,

    /// <summary><c>!fires reset &lt;target&gt;</c></summary>
    Reset,

    /// <summary><c>!fires preset &lt;name&gt; [target]</c></summary>
    Preset,

    /// <summary><c>!fires undo</c> — revert your own last change.</summary>
    Undo,

    /// <summary><c>!fires adminonly on|off</c></summary>
    AdminOnly,

    /// <summary><c>!fires hidecommands on|off</c></summary>
    HideCommands,

    /// <summary><c>!fires ignorerain on|off</c></summary>
    IgnoreRain,

    /// <summary><c>!fires serverownership on|off</c></summary>
    ServerOwnership,
}
