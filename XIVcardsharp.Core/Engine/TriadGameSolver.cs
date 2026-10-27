using System;
using System.Collections.Generic;
using XIVcardsharp.Core.Data;

namespace XIVcardsharp.Core.Engine;

public class TriadGameAgentRandom
{
    private Random? randGen;

    public TriadGameAgentRandom(TriadGameSolver? solver, int sessionSeed) => Initialize(solver, sessionSeed);

    public void Initialize(TriadGameSolver? solver, int sessionSeed) => randGen = new(sessionSeed);

    public bool IsInitialized() => randGen != null;

    public bool FindNextMove(TriadGameSolver solver, TriadGameSimulationState gameState, out int cardIdx, out int boardPos)
    {
        cardIdx = -1;
        boardPos = -1;

        if (!IsInitialized())
        {
            return false;
        }

        const int boardPosMax = TriadGameSimulationState.boardSizeSq;
        if (gameState.numCardsPlaced < TriadGameSimulationState.boardSizeSq)
        {
            var testPos = randGen!.Next(boardPosMax);
            for (var passIdx = 0; passIdx < boardPosMax; passIdx++)
            {
                testPos = (testPos + 1) % boardPosMax;
                if (gameState.board[testPos] == null)
                {
                    boardPos = testPos;
                    break;
                }
            }
        }

        var useDeck = (gameState.state == ETriadGameState.InProgressBlue) ? gameState.deckBlue : gameState.deckRed;
        if (useDeck.availableCardMask > 0)
        {
            var testIdx = randGen!.Next(TriadDeckInstance.maxAvailableCards);
            for (var passIdx = 0; passIdx < TriadDeckInstance.maxAvailableCards; passIdx++)
            {
                testIdx = (testIdx + 1) % TriadDeckInstance.maxAvailableCards;
                if ((useDeck.availableCardMask & (1 << testIdx)) != 0)
                {
                    cardIdx = testIdx;
                    break;
                }
            }
        }

        return (boardPos >= 0) && (cardIdx >= 0);
    }
}

public class TriadGameSolver
{
    public TriadGameSimulation simulation = new();

    public TriadGameSolver() { }

    public void InitializeSimulation(IEnumerable<TriadGameModifier> modsA, IEnumerable<TriadGameModifier>? modsB = null) =>
        simulation.Initialize(modsA, modsB);

    public void InitializeSimulation(IEnumerable<TriadGameModifier> mods) =>
        simulation.Initialize(mods);

    public TriadGameSolver CreateWorkerCopy()
    {
        var worker = new TriadGameSolver();
        worker.simulation.DeepCopyModifiersFrom(simulation);
        return worker;
    }

    public void RunSimulation(TriadGameSimulationState gameState, TriadGameAgentRandom agentBlue, TriadGameAgentRandom agentRed)
    {
        var keepPlaying = true;
        while (keepPlaying)
        {
            if (gameState.state == ETriadGameState.InProgressBlue)
            {
                keepPlaying = agentBlue.FindNextMove(this, gameState, out var cardIdx, out var boardPos);
                if (keepPlaying)
                {
                    keepPlaying = simulation.PlaceCard(gameState, cardIdx, gameState.deckBlue, ETriadCardOwner.Blue, boardPos);
                }
            }
            else if (gameState.state == ETriadGameState.InProgressRed)
            {
                keepPlaying = agentRed.FindNextMove(this, gameState, out var cardIdx, out var boardPos);
                if (keepPlaying)
                {
                    keepPlaying = simulation.PlaceCard(gameState, cardIdx, gameState.deckRed, ETriadCardOwner.Red, boardPos);
                }
            }
            else
            {
                keepPlaying = false;
            }
        }
    }

    public void FindAvailableActions(TriadGameSimulationState gameState, out int availBoardMask, out int availCardsMask)
    {
        availBoardMask = 0;
        for (var idx = 0; idx < gameState.board.Length; idx++)
        {
            if (gameState.board[idx] == null)
            {
                availBoardMask |= (1 << idx);
            }
        }

        var ownerDeckMask = (gameState.state == ETriadGameState.InProgressBlue)
            ? gameState.deckBlue.availableCardMask
            : gameState.deckRed.availableCardMask;

        if (gameState.forcedCardIdx >= 0)
        {
            var forcedBit = 1 << gameState.forcedCardIdx;
            availCardsMask = (ownerDeckMask & forcedBit) != 0 ? forcedBit : ownerDeckMask;
        }
        else
        {
            availCardsMask = ownerDeckMask;
        }

        if ((simulation.modFeatures & TriadGameModifier.EFeature.FilterNext) != 0)
        {
            foreach (var mod in simulation.modifiers)
            {
                mod.OnFilterNextCards(gameState, ref availCardsMask);
            }
        }
    }
}
