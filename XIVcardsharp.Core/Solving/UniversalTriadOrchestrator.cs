using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XIVcardsharp.Core.Engine;
using XIVcardsharp.Core.State;

namespace XIVcardsharp.Core.Solving;

public class UniversalTriadOrchestrator
{
    public UniversalEngineStatus Status { get; private set; } = UniversalEngineStatus.Idle;
    public string StatusSummary { get; private set; } = "Idle";

    public UniversalTriadSolver.UniversalSolveResult? Result { get; private set; }
    public bool IsEvaluating => _evaluatingTask != null && !_evaluatingTask.IsCompleted;
    public bool IsMoveReady => BestCardIdx >= 0 && BestBoardPos >= 0;

    public int BestCardIdx => Result?.CardIdx ?? -1;
    public int BestBoardPos => Result?.BoardPos ?? -1;
    public float BestEv => Result?.EV ?? 0.0f;
    public float BestPosScore => Result?.PositionalScore ?? 0.0f;
    public int LastEvalMs => Result?.ElapsedMs ?? 0;
    public string LastStrategy => Result?.Strategy ?? string.Empty;
    public List<UniversalTriadSolver.CandidateMove> LastCandidates => Result?.Candidates ?? [];

    private readonly Action<string, Exception?>? _onError;
    private Task<UniversalTriadSolver.UniversalSolveResult>? _evaluatingTask;
    private CancellationTokenSource? _evaluatingCts;
    private DateTime _evaluatingStartTime;

    public UniversalTriadOrchestrator(Action<string, Exception?>? onError = null)
    {
        _onError = onError;
    }

    public void SetStatus(UniversalEngineStatus status, string summary)
    {
        Status = status;
        StatusSummary = summary;
    }

    /// <summary>
    /// Checks or initiates asynchronous evaluation. Returns true if a move is ready to be executed this tick.
    /// </summary>
    public bool Update(
        UniversalTriadState state,
        UniversalEngineMode mode,
        int sampleCount,
        int maxThreads)
    {
        if (IsMoveReady)
        {
            return true;
        }

        // Check if an existing background evaluation task has finished
        if (_evaluatingTask != null)
        {
            if (TryProcessCompletedEvaluation(state))
            {
                return IsMoveReady;
            }

            if (_evaluatingTask != null)
            {
                Status = UniversalEngineStatus.Evaluating;
                var elapsed = (int)(DateTime.UtcNow - _evaluatingStartTime).TotalMilliseconds;
                StatusSummary = $"Thinking ({state.LocalPlayerColor})... ({elapsed}ms)";
                return false;
            }
        }

        // Start new evaluation task on thread pool
        Status = UniversalEngineStatus.Evaluating;
        StatusSummary = $"Thinking ({state.LocalPlayerColor})...";
        _evaluatingStartTime = DateTime.UtcNow;

        TrackSwapCards(state);

        _evaluatingCts?.Dispose();
        _evaluatingCts = new CancellationTokenSource();
        var ct = _evaluatingCts.Token;

        var solver = new UniversalTriadSolver(mode, sampleCount, maxThreads);
        _evaluatingTask = solver.SolveAsync(state, ct);

        if (TryProcessCompletedEvaluation(state))
        {
            return IsMoveReady;
        }

        return false;
    }

    private static void TrackSwapCards(UniversalTriadState state)
    {
        var hasSwap = state.Modifiers.Exists(m => m is TriadGameModifierSwap) ||
            state.RuleNames.Exists(r =>
                r.Contains("Swap", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("Échange", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("Tausch", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("スワップ", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("交换", StringComparison.OrdinalIgnoreCase));

        if (hasSwap && state.InitialPlayerCards.Count == 5)
        {
            var givenCard = state.InitialPlayerCards.FirstOrDefault(c => c != null && !state.LocalPlayerHand.Any(h => h != null && h.Id == c.Id));
            if (givenCard != null && !state.KnownOpponentCards.Any(k => k.Id == givenCard.Id))
            {
                state.KnownOpponentCards.Add(givenCard);
            }
        }
    }

    private bool TryProcessCompletedEvaluation(UniversalTriadState state)
    {
        if (_evaluatingTask == null || !_evaluatingTask.IsCompleted)
        {
            return false;
        }

        if (_evaluatingTask.IsCompletedSuccessfully)
        {
            var res = _evaluatingTask.Result;
            _evaluatingTask = null;
            _evaluatingCts?.Dispose();
            _evaluatingCts = null;

            if (res.NumCardsPlacedAtStart == state.NumCardsPlaced)
            {
                Result = res;

                if (res.CardIdx < 0 || res.BoardPos < 0)
                {
                    Status = UniversalEngineStatus.Evaluating;
                    StatusSummary = "No legal moves available";
                    return true;
                }

                Status = UniversalEngineStatus.MoveReady;
                StatusSummary = $"Ready [{state.LocalPlayerColor}]: Card #{res.CardIdx + 1} -> ({res.BoardPos % 3}, {res.BoardPos / 3}), EV: {res.EV:P0} ({res.ElapsedMs}ms)";
                return true;
            }

            // Board state advanced while evaluating
            Result = null;
            return false;
        }

        if (_evaluatingTask.IsFaulted && _evaluatingTask.Exception != null)
        {
            _onError?.Invoke("[UniversalTriadOrchestrator] Solve task faulted", _evaluatingTask.Exception);
        }

        _evaluatingTask = null;
        _evaluatingCts?.Dispose();
        _evaluatingCts = null;
        Result = null;
        return false;
    }

    public void ResetTurn()
    {
        if (_evaluatingCts != null)
        {
            try { _evaluatingCts.Cancel(); } catch { }
            _evaluatingCts.Dispose();
            _evaluatingCts = null;
        }
        _evaluatingTask = null;
        Result = null;
    }

    public void Reset()
    {
        ResetTurn();
        Status = UniversalEngineStatus.Idle;
        StatusSummary = "Idle";
    }
}
