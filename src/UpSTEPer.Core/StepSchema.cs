namespace UpSTEPer.Core;

/// <summary>The STEP application protocol a file is written in.</summary>
public enum StepSchema
{
    /// <summary>AP214 (automotive design): read by practically every CAD system; the default.</summary>
    Ap214 = 0,

    /// <summary>AP242 (managed model based 3D engineering): the current protocol, successor of AP203/AP214.</summary>
    Ap242 = 1,
}
