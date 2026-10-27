using System.Collections.Generic;
using XIVcardsharp.Core.Data;
using XIVcardsharp.Core.Draft;
using XIVcardsharp.Core.Engine;
using XIVcardsharp.Core.Sampling;
using XIVcardsharp.Core.Solving;
using XIVcardsharp.Core.State;
using Xunit;

namespace XIVcardsharp.Tests;

public class SolverTests
{
    [Fact]
    public void OrderRule_RestrictsMovesToFirstAvailableCard()
    {
        var state = new UniversalTriadState
        {
            LocalPlayerColor = ETriadCardOwner.Blue,
            RuleNames = ["Order"]
        };

        state.LocalPlayerHand[0] = new TriadCard(1, ETriadCardRarity.Common, ETriadCardType.None, 2, 2, 2, 2);
        state.LocalPlayerHand[1] = new TriadCard(2, ETriadCardRarity.Rare, ETriadCardType.None, 5, 5, 5, 5);
        state.LocalPlayerHand[2] = new TriadCard(3, ETriadCardRarity.Legendary, ETriadCardType.None, 9, 9, 9, 9);
        state.LocalCardPlaced[0] = false;
        state.LocalCardPlaced[1] = false;
        state.LocalCardPlaced[2] = false;

        var moves = UniversalTriadSolver.GetLegalMoves(state);
        Assert.NotEmpty(moves);
        // Every move must only use card index 0 under Order!
        Assert.All(moves, m => Assert.Equal(0, m.CardIdx));
    }

    [Fact]
    public void OrderRule_AdvancesToNextCard_WhenFirstIsPlaced()
    {
        var state = new UniversalTriadState
        {
            LocalPlayerColor = ETriadCardOwner.Blue,
            RuleNames = ["Order"]
        };

        state.LocalPlayerHand[0] = new TriadCard(1, ETriadCardRarity.Common, ETriadCardType.None, 2, 2, 2, 2);
        state.LocalPlayerHand[1] = new TriadCard(2, ETriadCardRarity.Rare, ETriadCardType.None, 5, 5, 5, 5);
        state.LocalCardPlaced[0] = true; // Placed!
        state.LocalCardPlaced[1] = false;

        var moves = UniversalTriadSolver.GetLegalMoves(state);
        Assert.NotEmpty(moves);
        // Next move must strictly use card index 1
        Assert.All(moves, m => Assert.Equal(1, m.CardIdx));
    }

    [Fact]
    public void DraftSwap_DetectsMissingAndExtraTiers()
    {
        var state = new UniversalTriadState
        {
            LocalPlayerColor = ETriadCardOwner.Blue,
            RuleNames = ["Swap"]
        };

        // Standard draft has 1x Legendary (5★), 1x Epic (4★), 1x Rare (3★), 1x Uncommon (2★), 1x Common (1★).
        // Suppose player gave 5★ and received another 3★:
        state.InitialPlayerCards =
        [
            new TriadCard(1, ETriadCardRarity.Epic, ETriadCardType.None, 6, 6, 6, 6),
            new TriadCard(2, ETriadCardRarity.Rare, ETriadCardType.None, 5, 5, 5, 5),
            new TriadCard(3, ETriadCardRarity.Rare, ETriadCardType.None, 5, 5, 5, 5), // Extra Rare!
            new TriadCard(4, ETriadCardRarity.Uncommon, ETriadCardType.None, 3, 3, 3, 3),
            new TriadCard(5, ETriadCardRarity.Common, ETriadCardType.None, 1, 1, 1, 1)
            // Missing Legendary (given away)
        ];

        var (given, taken) = RarityBudgetTracker.DetectDraftSwap(state);
        Assert.Equal(ETriadCardRarity.Legendary, given);
        Assert.Equal(ETriadCardRarity.Rare, taken);
    }

    [Fact]
    public void PositionalSafety_EvaluatesCornerHigherThanCenterOnOpening()
    {
        var state = new UniversalTriadState { NumCardsPlaced = 0 };
        var card = new TriadCard(1, ETriadCardRarity.Legendary, ETriadCardType.None, 9, 9, 2, 2);

        // Corner (e.g. 0) with protected walls vs Center (4)
        var cornerScore = PositionalSafetyEvaluator.Evaluate(state, card, 0);
        var centerScore = PositionalSafetyEvaluator.Evaluate(state, card, 4);

        Assert.True(cornerScore > centerScore, $"Corner ({cornerScore}) should score higher than center ({centerScore})");
    }

    [Fact]
    public void TournamentDraftEvaluator_ScoresReverseMode()
    {
        var draftState = new TournamentDraftState
        {
            Rules = ["Reverse"],
            Set1Options =
            [
                new TournamentDraftOption { SetIndex = 0, OptionIndex = 0, Cards = [new TournamentDraftCard("Card A", 1, 1, 2, 1, 1)] },
                new TournamentDraftOption { SetIndex = 0, OptionIndex = 1, Cards = [new TournamentDraftCard("Card B", 9, 9, 8, 8, 5)] },
                new TournamentDraftOption { SetIndex = 0, OptionIndex = 2, Cards = [new TournamentDraftCard("Card C", 5, 5, 5, 5, 3)] }
            ],
            Set2Options =
            [
                new TournamentDraftOption { SetIndex = 1, OptionIndex = 0, Cards = [new TournamentDraftCard("Card D", 2, 2, 1, 1, 2), new TournamentDraftCard("Card E", 1, 2, 1, 2, 1)] },
                new TournamentDraftOption { SetIndex = 1, OptionIndex = 1, Cards = [new TournamentDraftCard("Card F", 8, 7, 8, 7, 4), new TournamentDraftCard("Card G", 6, 6, 6, 6, 3)] },
                new TournamentDraftOption { SetIndex = 1, OptionIndex = 2, Cards = [new TournamentDraftCard("Card H", 5, 4, 5, 4, 3), new TournamentDraftCard("Card I", 4, 4, 4, 4, 2)] }
            ],
            Set3Options =
            [
                new TournamentDraftOption { SetIndex = 2, OptionIndex = 0, Cards = [new TournamentDraftCard("Card J", 1, 1, 1, 1, 1), new TournamentDraftCard("Card K", 2, 1, 2, 1, 2)] },
                new TournamentDraftOption { SetIndex = 2, OptionIndex = 1, Cards = [new TournamentDraftCard("Card L", 9, 8, 9, 8, 4), new TournamentDraftCard("Card M", 7, 7, 7, 7, 3)] },
                new TournamentDraftOption { SetIndex = 2, OptionIndex = 2, Cards = [new TournamentDraftCard("Card N", 6, 5, 6, 5, 3), new TournamentDraftCard("Card O", 3, 3, 3, 3, 2)] }
            ]
        };

        var result = TournamentDraftEvaluator.Evaluate(draftState);
        Assert.NotNull(result);
        Assert.Equal(0, result.Set1Option); // Option 0 with low numbers (1s and 2s) must win in Reverse!
        Assert.Equal(0, result.Set2Option);
        Assert.Equal(0, result.Set3Option);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void UniversalTriadSolver_RespectsMaxThreadsConstraint(int maxThreads)
    {
        var state = new UniversalTriadState
        {
            LocalPlayerColor = ETriadCardOwner.Blue,
            HiddenRedCount = 2,
            RuleNames = []
        };

        state.LocalPlayerHand[0] = new TriadCard(1, ETriadCardRarity.Common, ETriadCardType.None, 3, 3, 3, 3);
        state.LocalPlayerHand[1] = new TriadCard(2, ETriadCardRarity.Rare, ETriadCardType.None, 5, 5, 5, 5);
        state.LocalCardPlaced[0] = false;
        state.LocalCardPlaced[1] = false;

        // Populate known cards so sampler has a pool
        var cardDb = TriadCardDB.Get();
        cardDb.cards.Clear();
        cardDb.cards.Add(state.LocalPlayerHand[0]);
        cardDb.cards.Add(state.LocalPlayerHand[1]);
        cardDb.cards.Add(new TriadCard(3, ETriadCardRarity.Common, ETriadCardType.None, 2, 2, 2, 2));
        cardDb.cards.Add(new TriadCard(4, ETriadCardRarity.Common, ETriadCardType.None, 4, 4, 4, 4));

        var solver = new UniversalTriadSolver(UniversalEngineMode.UniversalMatch, sampleCount: 5, maxThreads: maxThreads);
        Assert.Equal(maxThreads, solver.MaxThreads);

        var (bestCard, bestBoard, _, candidates) = solver.Solve(state);
        Assert.True(bestCard >= 0);
        Assert.True(bestBoard >= 0);
        Assert.NotEmpty(candidates);
    }
}
