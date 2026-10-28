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

    [Fact]
    public void FallenAce_AllowsOneToCaptureAce()
    {
        var sim = new TriadGameSimulation();
        sim.Initialize([new TriadGameModifierFallenAce()]);

        var cardAce = new TriadCard(1, ETriadCardRarity.Legendary, ETriadCardType.None, 10, 10, 10, 10);
        var cardOne = new TriadCard(2, ETriadCardRarity.Common, ETriadCardType.None, 1, 1, 1, 1);

        var deckBlue = new TriadDeck([cardOne]);
        var deckRed = new TriadDeck([cardAce]);

        var state = sim.StartGame(deckBlue, deckRed, ETriadGameState.InProgressRed);

        // Red plays Ace at center (pos 4)
        sim.PlaceCard(state, 0, state.deckRed, ETriadCardOwner.Red, 4);
        Assert.Equal(ETriadCardOwner.Red, state.board[4].owner);

        // Blue plays 1 at top (pos 1). Under Fallen Ace, bottom of pos 1 (1) attacks top of pos 4 (10) and captures it!
        state.state = ETriadGameState.InProgressBlue;
        sim.PlaceCard(state, 0, state.deckBlue, ETriadCardOwner.Blue, 1);

        Assert.Equal(ETriadCardOwner.Blue, state.board[4].owner);
    }

    [Fact]
    public void SameRule_CapturesWhenTwoSidesMatch()
    {
        var sim = new TriadGameSimulation();
        sim.Initialize([new TriadGameModifierSame()]);

        // Top neighbor at pos 1: numDown is 7 (touches center Up)
        // TriadCard constructor: (id, rarity, type, numUp, numDown, numLeft, numRight)
        var neiTop = new TriadCard(1, ETriadCardRarity.Rare, ETriadCardType.None, 5, 7, 5, 5);
        // Left neighbor at pos 3: numRight is 8 (touches center Left)
        var neiLeft = new TriadCard(2, ETriadCardRarity.Rare, ETriadCardType.None, 5, 5, 5, 8);
        // Player card placed at pos 4 (center): numUp is 7, numLeft is 8
        var playerCenter = new TriadCard(3, ETriadCardRarity.Rare, ETriadCardType.None, 7, 5, 8, 5);

        var deckBlue = new TriadDeck([playerCenter]);
        var deckRed = new TriadDeck([neiTop, neiLeft]);

        var state = sim.StartGame(deckBlue, deckRed, ETriadGameState.InProgressRed);

        // Place opponent card at pos 1
        sim.PlaceCard(state, 0, state.deckRed, ETriadCardOwner.Red, 1);
        Assert.Equal(ETriadCardOwner.Red, state.board[1].owner);

        // Prepare Red turn to place second card at pos 3
        state.state = ETriadGameState.InProgressRed;
        sim.PlaceCard(state, 1, state.deckRed, ETriadCardOwner.Red, 3);
        Assert.Equal(ETriadCardOwner.Red, state.board[3].owner);

        // Blue plays at center (pos 4): Up matches pos 1 Down (7==7), Left matches pos 3 Right (8==8) -> Same triggers!
        state.state = ETriadGameState.InProgressBlue;
        sim.PlaceCard(state, 0, state.deckBlue, ETriadCardOwner.Blue, 4);

        Assert.Equal(ETriadCardOwner.Blue, state.board[1].owner);
        Assert.Equal(ETriadCardOwner.Blue, state.board[3].owner);
    }

    [Fact]
    public void RarityBudgetTracker_CapsTournamentDecksCorrectly()
    {
        // Standard Open Tournament draft allows max 1x 5★, 1x 4★, 1x 3★, 1x 2★, 1x 1★
        var tracker = new RarityBudgetTracker(
            UniversalEngineMode.OpenTournamentDraft,
            knownOpponentCards: [new TriadCard(1, ETriadCardRarity.Legendary, ETriadCardType.None, 9, 9, 9, 9)]);

        // 5★ is already known, so remaining 5★ slots must be 0
        Assert.Equal(1, tracker.KnownCounts[ETriadCardRarity.Legendary]);
    }

    [Fact]
    public void GetNeighbors_ReturnsCorrectCardinalNeighbors()
    {
        // Center (pos 4): Up is pos 1, Down is pos 7, Left is pos 3, Right is pos 5
        var neisCenter = TriadGameSimulation.GetNeighbors(4);
        Assert.Equal(1, neisCenter[(int)ETriadGameSide.Up]);
        Assert.Equal(7, neisCenter[(int)ETriadGameSide.Down]);
        Assert.Equal(3, neisCenter[(int)ETriadGameSide.Left]);
        Assert.Equal(5, neisCenter[(int)ETriadGameSide.Right]);

        // Top-Left (pos 0): Up is -1, Left is -1, Down is pos 3, Right is pos 1
        var neisTopLeft = TriadGameSimulation.GetNeighbors(0);
        Assert.Equal(-1, neisTopLeft[(int)ETriadGameSide.Up]);
        Assert.Equal(-1, neisTopLeft[(int)ETriadGameSide.Left]);
        Assert.Equal(3, neisTopLeft[(int)ETriadGameSide.Down]);
        Assert.Equal(1, neisTopLeft[(int)ETriadGameSide.Right]);

        // Top-Right (pos 2): Up is -1, Right is -1, Down is pos 5, Left is pos 1
        var neisTopRight = TriadGameSimulation.GetNeighbors(2);
        Assert.Equal(-1, neisTopRight[(int)ETriadGameSide.Up]);
        Assert.Equal(-1, neisTopRight[(int)ETriadGameSide.Right]);
        Assert.Equal(5, neisTopRight[(int)ETriadGameSide.Down]);
        Assert.Equal(1, neisTopRight[(int)ETriadGameSide.Left]);
    }

    [Fact]
    public void HorizontalCombat_CapturesUsingCorrectLeftAndRightSides()
    {
        var sim = new TriadGameSimulation();
        sim.Initialize([]);

        // TriadCard: (id, rarity, type, numUp, numDown, numLeft, numRight)
        // Opponent at pos 3 (Left of center): Right is 4
        var cardLeft = new TriadCard(1, ETriadCardRarity.Rare, ETriadCardType.None, 5, 5, 5, 4);
        // Opponent at pos 5 (Right of center): Left is 6
        var cardRight = new TriadCard(2, ETriadCardRarity.Rare, ETriadCardType.None, 5, 5, 6, 5);
        // Player at pos 4 (center): Left is 8, Right is 2
        var cardCenter = new TriadCard(3, ETriadCardRarity.Rare, ETriadCardType.None, 5, 5, 8, 2);

        var deckBlue = new TriadDeck([cardCenter]);
        var deckRed = new TriadDeck([cardLeft, cardRight]);

        var state = sim.StartGame(deckBlue, deckRed, ETriadGameState.InProgressRed);

        // Red places card at pos 3 and pos 5
        sim.PlaceCard(state, 0, state.deckRed, ETriadCardOwner.Red, 3);
        state.state = ETriadGameState.InProgressRed;
        sim.PlaceCard(state, 1, state.deckRed, ETriadCardOwner.Red, 5);

        Assert.Equal(ETriadCardOwner.Red, state.board[3].owner);
        Assert.Equal(ETriadCardOwner.Red, state.board[5].owner);

        // Blue places card at center (pos 4): Left (8) beats pos 3 Right (4); Right (2) loses to pos 5 Left (6)
        state.state = ETriadGameState.InProgressBlue;
        sim.PlaceCard(state, 0, state.deckBlue, ETriadCardOwner.Blue, 4);

        // Pos 3 must be captured (Blue); Pos 5 must remain Red
        Assert.Equal(ETriadCardOwner.Blue, state.board[3].owner);
        Assert.Equal(ETriadCardOwner.Red, state.board[5].owner);
    }

    [Fact]
    public void PlusRule_CapturesHorizontallyWithMatchingSums()
    {
        var sim = new TriadGameSimulation();
        sim.Initialize([new TriadGameModifierPlus()]);

        // Bottom-left at pos 6: Right is 9
        var cardBottomLeft = new TriadCard(1, ETriadCardRarity.Rare, ETriadCardType.None, 5, 5, 5, 9);
        // Bottom-right at pos 8: Left is 4
        var cardBottomRight = new TriadCard(2, ETriadCardRarity.Rare, ETriadCardType.None, 5, 5, 4, 5);
        // Bottom-center at pos 7 (Gajasura-style): Left is 4, Right is 9
        // Left sum = 4 + 9 = 13; Right sum = 9 + 4 = 13 -> Plus triggers!
        var cardBottomCenter = new TriadCard(3, ETriadCardRarity.Rare, ETriadCardType.None, 5, 5, 4, 9);

        var deckBlue = new TriadDeck([cardBottomLeft, cardBottomRight]);
        var deckRed = new TriadDeck([cardBottomCenter]);

        var state = sim.StartGame(deckBlue, deckRed, ETriadGameState.InProgressBlue);

        // Blue places cards at pos 6 and pos 8
        sim.PlaceCard(state, 0, state.deckBlue, ETriadCardOwner.Blue, 6);
        state.state = ETriadGameState.InProgressBlue;
        sim.PlaceCard(state, 1, state.deckBlue, ETriadCardOwner.Blue, 8);

        Assert.Equal(ETriadCardOwner.Blue, state.board[6].owner);
        Assert.Equal(ETriadCardOwner.Blue, state.board[8].owner);

        // Red plays card at pos 7: matching sums of 13 trigger Plus on both adjacent cards!
        state.state = ETriadGameState.InProgressRed;
        sim.PlaceCard(state, 0, state.deckRed, ETriadCardOwner.Red, 7);

        Assert.Equal(ETriadCardOwner.Red, state.board[6].owner);
        Assert.Equal(ETriadCardOwner.Red, state.board[8].owner);
    }

    [Fact]
    public void DraftSwap_DetectsExactSwappedCards()
    {
        var state = new UniversalTriadState
        {
            LocalPlayerColor = ETriadCardOwner.Blue,
            RuleNames = ["Swap"]
        };

        var ramuh = new TriadCard(10, ETriadCardRarity.Epic, ETriadCardType.None, 8, 8, 1, 8);
        var beatrice = new TriadCard(20, ETriadCardRarity.Rare, ETriadCardType.None, 6, 7, 7, 4);

        state.InitialPlayerCards =
        [
            new TriadCard(1, ETriadCardRarity.Legendary, ETriadCardType.None, 9, 9, 9, 9),
            ramuh, // Given away!
            new TriadCard(2, ETriadCardRarity.Rare, ETriadCardType.None, 5, 5, 5, 5),
            new TriadCard(3, ETriadCardRarity.Uncommon, ETriadCardType.None, 3, 3, 3, 3),
            new TriadCard(4, ETriadCardRarity.Common, ETriadCardType.None, 1, 1, 1, 1)
        ];

        // Player's hand after swap contains Beatrice instead of Ramuh
        state.LocalPlayerHand[0] = state.InitialPlayerCards[0];
        state.LocalPlayerHand[1] = beatrice; // Received from opponent!
        state.LocalPlayerHand[2] = state.InitialPlayerCards[2];
        state.LocalPlayerHand[3] = state.InitialPlayerCards[3];
        state.LocalPlayerHand[4] = state.InitialPlayerCards[4];

        var (givenTier, takenTier) = RarityBudgetTracker.DetectDraftSwap(state);
        Assert.Equal(ETriadCardRarity.Epic, givenTier);
        Assert.Equal(ETriadCardRarity.Rare, takenTier);
    }

    [Fact]
    public void UniversalDeckSampler_AssignsUnplacedKnownOpponentCardsToHiddenSlots()
    {
        var cardDb = TriadCardDB.Get();
        cardDb.cards.Clear();
        for (var i = 1; i <= 20; i++)
        {
            cardDb.cards.Add(new TriadCard(i, (ETriadCardRarity)(i % 5), ETriadCardType.None, 3, 3, 3, 3));
        }

        var state = new UniversalTriadState
        {
            LocalPlayerColor = ETriadCardOwner.Blue,
            HiddenRedCount = 4
        };

        var givenRamuh = new TriadCard(99, ETriadCardRarity.Epic, ETriadCardType.None, 8, 8, 1, 8);
        cardDb.cards.Add(givenRamuh);

        // We know Red has Ramuh in their hidden hand (via Swap)
        state.KnownOpponentCards.Add(givenRamuh);

        var sampled = UniversalDeckSampler.SampleOpponentDecks(state, UniversalEngineMode.OpenTournamentDraft, sampleCount: 15);
        Assert.Equal(15, sampled.Count);

        // Every sampled deck must include Ramuh in one of the slots!
        foreach (var deck in sampled)
        {
            var containsRamuh = false;
            for (var slot = 0; slot < 5; slot++)
            {
                if (deck.cards[slot] != null && deck.cards[slot]!.Id == 99)
                {
                    containsRamuh = true;
                    break;
                }
            }
            Assert.True(containsRamuh, "Sampled deck must contain known unplaced opponent card (Ramuh)");
        }
    }

    [Fact]
    public void UniversalTriadState_Clone_CreatesDeepIndependentCopy()
    {
        var original = new UniversalTriadState
        {
            LocalPlayerColor = ETriadCardOwner.Blue,
            HiddenRedCount = 3,
            NumCardsPlaced = 2
        };
        var card = new TriadCard(42, ETriadCardRarity.Rare, ETriadCardType.None, 4, 4, 4, 4);
        original.BlueHand[0] = card;
        original.KnownRedCards.Add(card);
        original.RuleNames.Add("Plus");

        var cloned = original.Clone();

        Assert.Equal(original.LocalPlayerColor, cloned.LocalPlayerColor);
        Assert.Equal(original.HiddenRedCount, cloned.HiddenRedCount);
        Assert.Equal(original.NumCardsPlaced, cloned.NumCardsPlaced);
        Assert.Equal(card.Id, cloned.BlueHand[0]?.Id);
        Assert.Single(cloned.KnownRedCards);
        Assert.Single(cloned.RuleNames);

        // Mutating cloned collections should not affect original
        cloned.BlueHand[0] = null;
        cloned.KnownRedCards.Clear();
        cloned.RuleNames.Add("Same");

        Assert.NotNull(original.BlueHand[0]);
        Assert.Single(original.KnownRedCards);
        Assert.Single(original.RuleNames);
    }

    [Fact]
    public void UniversalTriadSolver_Cancellation_StopsSolve()
    {
        var state = new UniversalTriadState
        {
            LocalPlayerColor = ETriadCardOwner.Blue,
            HiddenRedCount = 4
        };
        state.LocalPlayerHand[0] = new TriadCard(1, ETriadCardRarity.Common, ETriadCardType.None, 3, 3, 3, 3);
        state.LocalPlayerHand[1] = new TriadCard(2, ETriadCardRarity.Common, ETriadCardType.None, 4, 4, 4, 4);

        var cardDb = TriadCardDB.Get();
        cardDb.cards.Clear();
        cardDb.cards.Add(state.LocalPlayerHand[0]);
        cardDb.cards.Add(state.LocalPlayerHand[1]);
        cardDb.cards.Add(new TriadCard(3, ETriadCardRarity.Common, ETriadCardType.None, 2, 2, 2, 2));

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled token

        var solver = new UniversalTriadSolver(UniversalEngineMode.UniversalMatch, sampleCount: 100);

        Assert.Throws<OperationCanceledException>(() =>
        {
            solver.Solve(state, cts.Token);
        });
    }

    [Fact]
    public void UniversalTriadState_RedPerspective_PropertiesMapCorrectly()
    {
        var state = new UniversalTriadState
        {
            LocalPlayerColor = ETriadCardOwner.Red,
            HiddenBlueCount = 2,
            HiddenRedCount = 0
        };

        var redCard1 = new TriadCard(10, ETriadCardRarity.Common, ETriadCardType.None, 4, 4, 4, 4);
        var redCard2 = new TriadCard(11, ETriadCardRarity.Uncommon, ETriadCardType.None, 5, 5, 5, 5);
        state.RedHand[0] = redCard1;
        state.RedHand[1] = redCard2;

        var blueCard1 = new TriadCard(20, ETriadCardRarity.Rare, ETriadCardType.None, 6, 6, 6, 6);
        state.BlueHand[0] = blueCard1;
        state.KnownBlueCards.Add(blueCard1);

        // Perspective mapping verification
        Assert.Equal(ETriadCardOwner.Red, state.LocalPlayerColor);
        Assert.Equal(ETriadCardOwner.Blue, state.OpponentColor);
        Assert.Same(state.RedHand, state.LocalPlayerHand);
        Assert.Same(state.BlueHand, state.OpponentHand);
        Assert.Same(state.RedCardPlaced, state.LocalCardPlaced);
        Assert.Same(state.BlueCardPlaced, state.OpponentCardPlaced);
        Assert.Equal(2, state.HiddenOpponentCount);
        Assert.Single(state.KnownOpponentCards);
        Assert.Equal(blueCard1.Id, state.KnownOpponentCards[0].Id);
        Assert.Equal(2, state.AvailableLocalCardCount);
    }

    [Fact]
    public void UniversalTriadSolver_SolvesCorrectly_WhenPlayerIsRed()
    {
        var state = new UniversalTriadState
        {
            LocalPlayerColor = ETriadCardOwner.Red,
            HiddenBlueCount = 0,
            NumCardsPlaced = 1
        };

        var cardDb = TriadCardDB.Get();
        cardDb.cards.Clear();

        var playerCard1 = new TriadCard(101, ETriadCardRarity.Common, ETriadCardType.None, 4, 4, 4, 4);
        var playerCard2 = new TriadCard(102, ETriadCardRarity.Uncommon, ETriadCardType.None, 5, 5, 5, 5);
        var oppCard1 = new TriadCard(201, ETriadCardRarity.Common, ETriadCardType.None, 3, 3, 3, 3);
        var oppCard2 = new TriadCard(202, ETriadCardRarity.Common, ETriadCardType.None, 3, 3, 3, 3);

        cardDb.cards.Add(playerCard1);
        cardDb.cards.Add(playerCard2);
        cardDb.cards.Add(oppCard1);
        cardDb.cards.Add(oppCard2);

        state.RedHand[0] = playerCard1;
        state.RedHand[1] = playerCard2;

        state.BlueHand[0] = oppCard1;
        state.BlueHand[1] = oppCard2;
        state.BlueCardPlaced[0] = true;

        // Opponent (Blue) played oppCard1 at tile (0, 0)
        state.BoardCards[0] = oppCard1;
        state.BoardOwners[0] = ETriadCardOwner.Blue;

        var solver = new UniversalTriadSolver(UniversalEngineMode.OpenTournamentDraft, sampleCount: 10);
        var (bestCard, bestBoard, _, candidates) = solver.Solve(state);

        Assert.InRange(bestCard, 0, 1);
        Assert.InRange(bestBoard, 1, 8); // Board slot 0 is already occupied
        Assert.Null(state.BoardCards[bestBoard]);
        Assert.NotNull(state.LocalPlayerHand[bestCard]);
    }
}

