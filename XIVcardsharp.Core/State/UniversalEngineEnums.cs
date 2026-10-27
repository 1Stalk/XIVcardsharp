namespace XIVcardsharp.Core.State;

public enum UniversalEngineMode
{
    UniversalMatch,
    OpenTournamentDraft
}

public enum UniversalEngineStatus
{
    Idle,
    WaitingTurn,
    Evaluating,
    MoveReady,
    Executing
}
