#nullable disable
using System;
using XIVcardsharp.Core.Data;

namespace XIVcardsharp.Core.Engine;

public class TriadGameSimulationState
{
    public const int boardSize = 3;
    public const int boardSizeSq = boardSize * boardSize;
    public bool bDebugRules;
    public TriadCardInstance[] board;
    public TriadDeckInstance deckBlue;
    public TriadDeckInstance deckRed;
    public int forcedCardIdx;
    public int numCardsPlaced;
    public int numRestarts;
    public ETriadGameSpecialMod resolvedSpecial;
    public ETriadGameState state;
    public int[] typeMods;

    public TriadGameSimulationState()
    {
        board = new TriadCardInstance[boardSizeSq];
        typeMods = new int[Enum.GetNames(typeof(ETriadCardType)).Length];
        state = ETriadGameState.InProgressBlue;
        resolvedSpecial = ETriadGameSpecialMod.None;
        numCardsPlaced = 0;
        numRestarts = 0;
        forcedCardIdx = -1;
        bDebugRules = false;

        for (var idx = 0; idx < typeMods.Length; idx++)
        {
            typeMods[idx] = 0;
        }
    }

    public TriadGameSimulationState(TriadGameSimulationState copyFrom)
    {
        board = new TriadCardInstance[copyFrom.board.Length];
        for (var idx = 0; idx < board.Length; idx++)
        {
            board[idx] = (copyFrom.board[idx] == null) ? null : new TriadCardInstance(copyFrom.board[idx]);
        }

        typeMods = new int[copyFrom.typeMods.Length];
        for (var idx = 0; idx < typeMods.Length; idx++)
        {
            typeMods[idx] = copyFrom.typeMods[idx];
        }

        deckBlue = copyFrom.deckBlue.CreateCopy();
        deckRed = copyFrom.deckRed.CreateCopy();
        state = copyFrom.state;
        numCardsPlaced = copyFrom.numCardsPlaced;
        numRestarts = copyFrom.numRestarts;
        resolvedSpecial = copyFrom.resolvedSpecial;
        forcedCardIdx = copyFrom.forcedCardIdx;
        bDebugRules = copyFrom.bDebugRules;
    }
}
