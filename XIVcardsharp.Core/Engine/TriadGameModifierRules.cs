#nullable disable
using System.Collections.Generic;
using XIVcardsharp.Core.Data;

namespace XIVcardsharp.Core.Engine;

public class TriadGameModifierNone : TriadGameModifier
{
    public TriadGameModifierNone()
    {
        RuleName = "None";
        RuleIndex = 0;
    }
}

public class TriadGameModifierRoulette : TriadGameModifier
{
    protected TriadGameModifier RuleInst;

    public TriadGameModifierRoulette()
    {
        RuleName = "Roulette";
        RuleIndex = 1;
        SpecialMod = ETriadGameSpecialMod.RandomizeRule;
    }

    public override string GetCodeName() => base.GetCodeName() + (RuleInst != null ? (" (" + RuleInst.GetCodeName() + ")") : "");
    public override string GetLocalizedName() => base.GetLocalizedName() + (RuleInst != null ? (" (" + RuleInst.GetLocalizedName() + ")") : "");
    public override bool AllowsCombo() => (RuleInst != null) ? RuleInst.AllowsCombo() : base.AllowsCombo();
    public override bool IsDeckOrderImportant() => (RuleInst != null) ? RuleInst.IsDeckOrderImportant() : base.IsDeckOrderImportant();
    public override ETriadGameSpecialMod GetSpecialRules() => base.GetSpecialRules() | ((RuleInst != null) ? RuleInst.GetSpecialRules() : ETriadGameSpecialMod.None);
    public override EFeature GetFeatures() => (RuleInst != null) ? RuleInst.GetFeatures() : EFeature.None;
    public override bool HasLastRedReminder() => (RuleInst != null) ? RuleInst.HasLastRedReminder() : base.HasLastRedReminder();

    public override void OnCardPlaced(TriadGameSimulationState gameData, int boardPos) => RuleInst?.OnCardPlaced(gameData, boardPos);
    public override void OnCheckCaptureNeis(TriadGameSimulationState gameData, int boardPos, int[] neiPos, List<int> captureList) => RuleInst?.OnCheckCaptureNeis(gameData, boardPos, neiPos, captureList);
    public override void OnCheckCaptureCardWeights(TriadGameSimulationState gameData, int boardPos, int neiPos, bool isReverseActive, ref int cardNum, ref int neiNum) => RuleInst?.OnCheckCaptureCardWeights(gameData, boardPos, neiPos, isReverseActive, ref cardNum, ref neiNum);
    public override void OnCheckCaptureCardMath(TriadGameSimulationState gameData, int boardPos, int neiPos, int cardNum, int neiNum, ref bool isCaptured) => RuleInst?.OnCheckCaptureCardMath(gameData, boardPos, neiPos, cardNum, neiNum, ref isCaptured);
    public override void OnPostCaptures(TriadGameSimulationState gameData, int boardPos) => RuleInst?.OnPostCaptures(gameData, boardPos);
    public override void OnAllCardsPlaced(TriadGameSimulationState gameData) => RuleInst?.OnAllCardsPlaced(gameData);
    public override void OnFilterNextCards(TriadGameSimulationState gameData, ref int allowedCardsMask) => RuleInst?.OnFilterNextCards(gameData, ref allowedCardsMask);
    public override void OnMatchInit() => SetRuleInstance(null);

    public void SetRuleInstance(TriadGameModifier RuleInstance) => RuleInst = RuleInstance;
    public TriadGameModifier GetResolvedRule() => RuleInst;
}

public class TriadGameModifierAllOpen : TriadGameModifier
{
    public TriadGameModifierAllOpen()
    {
        RuleName = "All Open";
        RuleIndex = 2;
        SpecialMod = ETriadGameSpecialMod.SelectVisible5;
    }
}

public class TriadGameModifierThreeOpen : TriadGameModifier
{
    public TriadGameModifierThreeOpen()
    {
        RuleName = "Three Open";
        RuleIndex = 3;
        SpecialMod = ETriadGameSpecialMod.SelectVisible3;
    }
}

public class TriadGameModifierSuddenDeath : TriadGameModifier
{
    public TriadGameModifierSuddenDeath()
    {
        RuleName = "Sudden Death";
        RuleIndex = 4;
        bHasLastRedReminder = true;
        Features = EFeature.AllPlaced;
    }

    public override void OnAllCardsPlaced(TriadGameSimulationState gameData)
    {
        if (gameData.state == ETriadGameState.BlueDraw && gameData.numRestarts < 3)
        {
            if (gameData.deckBlue is TriadDeckInstanceManual deckBlueEx && gameData.deckRed is TriadDeckInstanceManual deckRedEx)
            {
                List<TriadCard> blueCards = [];
                List<TriadCard> redCards = [];
                List<TriadCard> redUnknownCards = [];

                for (var idx = 0; idx < gameData.board.Length; idx++)
                {
                    if (gameData.board[idx].owner == ETriadCardOwner.Blue)
                    {
                        blueCards.Add(gameData.board[idx].card);
                    }
                    else
                    {
                        redCards.Add(gameData.board[idx].card);
                    }

                    gameData.board[idx] = null;
                }

                if (deckBlueEx.numPlaced < deckRedEx.numPlaced)
                {
                    for (var idx = 0; idx < deckBlueEx.deck.knownCards.Count; idx++)
                    {
                        var bIsAvailable = !deckBlueEx.IsPlaced(idx);
                        if (bIsAvailable)
                        {
                            blueCards.Add(deckBlueEx.deck.knownCards[idx]);
                            break;
                        }
                    }

                    gameData.state = ETriadGameState.InProgressBlue;
                }
                else
                {
                    for (var idx = 0; idx < deckRedEx.deck.knownCards.Count; idx++)
                    {
                        var bIsAvailable = !deckRedEx.IsPlaced(idx);
                        if (bIsAvailable)
                        {
                            redCards.Add(deckRedEx.deck.knownCards[idx]);
                            break;
                        }
                    }

                    if (redCards.Count < blueCards.Count)
                    {
                        for (var idx = 0; idx < deckRedEx.deck.unknownCardPool.Count; idx++)
                        {
                            var cardIdx = idx + deckRedEx.deck.knownCards.Count;
                            var bIsAvailable = !deckRedEx.IsPlaced(cardIdx);
                            if (bIsAvailable)
                            {
                                redUnknownCards.Add(deckRedEx.deck.unknownCardPool[idx]);
                            }
                        }
                    }

                    gameData.state = ETriadGameState.InProgressRed;
                }

                gameData.deckBlue = new TriadDeckInstanceManual(new TriadDeck(blueCards));
                gameData.deckRed = new TriadDeckInstanceManual(new TriadDeck(redCards, redUnknownCards));
                gameData.numCardsPlaced = 0;
                gameData.numRestarts++;

                for (var idx = 0; idx < gameData.typeMods.Length; idx++)
                {
                    gameData.typeMods[idx] = 0;
                }
            }
        }
    }
}

public class TriadGameModifierRandom : TriadGameModifier
{
    public TriadGameModifierRandom()
    {
        RuleName = "Random";
        RuleIndex = 14;
        SpecialMod = ETriadGameSpecialMod.RandomizeBlueDeck;
    }
}

public class TriadGameModifierOrder : TriadGameModifier
{
    public TriadGameModifierOrder()
    {
        RuleName = "Order";
        RuleIndex = 11;
        bIsDeckOrderImportant = true;
        Features = EFeature.FilterNext;
    }

    public override void OnFilterNextCards(TriadGameSimulationState gameData, ref int allowedCardsMask)
    {
        if (allowedCardsMask != 0)
        {
            var isBlue = gameData.state == ETriadGameState.InProgressBlue;
            var activeDeck = isBlue ? gameData.deckBlue : gameData.deckRed;
            var firstIdx = activeDeck.GetFirstAvailableCardFast();
            allowedCardsMask = (firstIdx < 0) ? 0 : (1 << firstIdx);
        }
    }
}

public class TriadGameModifierChaos : TriadGameModifier
{
    public TriadGameModifierChaos()
    {
        RuleName = "Chaos";
        RuleIndex = 12;
        SpecialMod = ETriadGameSpecialMod.BlueCardSelection;
    }
}

public class TriadGameModifierReverse : TriadGameModifier
{
    public TriadGameModifierReverse()
    {
        RuleName = "Reverse";
        RuleIndex = 5;
        Features = EFeature.CaptureMath;
    }

    public override void OnCheckCaptureCardMath(TriadGameSimulationState gameData, int boardPos, int neiPos, int cardNum, int neiNum, ref bool isCaptured) => isCaptured = cardNum < neiNum;

    public override void OnScoreCard(TriadCard card, ref float score)
    {
        const float maxSum = 40.0f;
        var numberSum = card.Sides[0] + card.Sides[1] + card.Sides[2] + card.Sides[3];
        score = 1.0f - (numberSum / maxSum);
    }
}

public class TriadGameModifierFallenAce : TriadGameModifier
{
    public TriadGameModifierFallenAce()
    {
        RuleName = "Fallen Ace";
        RuleIndex = 6;
        Features = EFeature.CaptureWeights;
    }

    public override void OnCheckCaptureCardWeights(TriadGameSimulationState gameData, int boardPos, int neiPos, bool isReverseActive, ref int cardNum, ref int neiNum)
    {
        if (isReverseActive)
        {
            if (cardNum == 10 && neiNum == 1)
            {
                cardNum = 0;
            }
        }
        else
        {
            if (cardNum == 1 && neiNum == 10)
            {
                neiNum = 0;
            }
        }
    }
}

public class TriadGameModifierSame : TriadGameModifier
{
    public TriadGameModifierSame()
    {
        RuleName = "Same";
        RuleIndex = 7;
        bAllowCombo = true;
        Features = EFeature.CaptureNei | EFeature.CardPlaced;
    }

    public override void OnCheckCaptureNeis(TriadGameSimulationState gameData, int boardPos, int[] neiPos, List<int> captureList)
    {
        var checkCard = gameData.board[boardPos];
        var numSame = 0;
        var neiCaptureMask = 0;
        for (var sideIdx = 0; sideIdx < 4; sideIdx++)
        {
            var testNeiPos = neiPos[sideIdx];
            if (testNeiPos >= 0 && gameData.board[testNeiPos] != null)
            {
                var neiCard = gameData.board[testNeiPos];

                var numPos = checkCard.GetNumber((ETriadGameSide)sideIdx);
                var numOther = neiCard.GetOppositeNumber((ETriadGameSide)sideIdx);
                if (numPos == numOther)
                {
                    numSame++;

                    if (neiCard.owner != checkCard.owner)
                    {
                        neiCaptureMask |= (1 << sideIdx);
                    }
                }
            }
        }

        if (numSame >= 2)
        {
            for (var sideIdx = 0; sideIdx < 4; sideIdx++)
            {
                var testNeiPos = neiPos[sideIdx];
                if ((neiCaptureMask & (1 << sideIdx)) != 0)
                {
                    var neiCard = gameData.board[testNeiPos];
                    neiCard.owner = checkCard.owner;
                    captureList.Add(testNeiPos);
                }
            }
        }
    }
}

public class TriadGameModifierPlus : TriadGameModifier
{
    public TriadGameModifierPlus()
    {
        RuleName = "Plus";
        RuleIndex = 8;
        bAllowCombo = true;
        Features = EFeature.CaptureNei | EFeature.CardPlaced;
    }

    public override void OnCheckCaptureNeis(TriadGameSimulationState gameData, int boardPos, int[] neiPos, List<int> captureList)
    {
        var checkCard = gameData.board[boardPos];
        for (var sideIdx = 0; sideIdx < 4; sideIdx++)
        {
            var testNeiPos = neiPos[sideIdx];
            if (testNeiPos >= 0 && gameData.board[testNeiPos] != null)
            {
                var neiCard = gameData.board[testNeiPos];
                if (checkCard.owner != neiCard.owner)
                {
                    var numPosPattern = checkCard.GetNumber((ETriadGameSide)sideIdx);
                    var numOtherPattern = neiCard.GetOppositeNumber((ETriadGameSide)sideIdx);
                    var sumPattern = numPosPattern + numOtherPattern;
                    var bIsCaptured = false;

                    for (var vsSideIdx = 0; vsSideIdx < 4; vsSideIdx++)
                    {
                        var vsNeiPos = neiPos[vsSideIdx];
                        if (vsNeiPos >= 0 && sideIdx != vsSideIdx && gameData.board[vsNeiPos] != null)
                        {
                            var vsCard = gameData.board[vsNeiPos];

                            var numPosVs = checkCard.GetNumber((ETriadGameSide)vsSideIdx);
                            var numOtherVs = vsCard.GetOppositeNumber((ETriadGameSide)vsSideIdx);
                            var sumVs = numPosVs + numOtherVs;

                            if (sumPattern == sumVs)
                            {
                                bIsCaptured = true;

                                if (vsCard.owner != checkCard.owner)
                                {
                                    vsCard.owner = checkCard.owner;
                                    captureList.Add(vsNeiPos);
                                }
                            }
                        }
                    }

                    if (bIsCaptured)
                    {
                        neiCard.owner = checkCard.owner;
                        captureList.Add(testNeiPos);
                    }
                }
            }
        }
    }
}

public abstract class TriadGameModifierTypeShift : TriadGameModifier
{
    protected abstract int TypeStep { get; }

    public override void OnCardPlaced(TriadGameSimulationState gameData, int boardPos)
    {
        var checkCard = gameData.board[boardPos];
        if (checkCard.card.Type != ETriadCardType.None)
        {
            var scoreMod = gameData.typeMods[(int)checkCard.card.Type];
            if (scoreMod != 0)
            {
                checkCard.scoreModifier = scoreMod;
            }
        }
    }

    public override void OnPostCaptures(TriadGameSimulationState gameData, int boardPos)
    {
        var checkCard = gameData.board[boardPos];
        if (checkCard.card.Type != ETriadCardType.None)
        {
            var scoreMod = checkCard.scoreModifier + TypeStep;
            gameData.typeMods[(int)checkCard.card.Type] = scoreMod;

            for (var idx = 0; idx < gameData.board.Length; idx++)
            {
                var otherCard = gameData.board[idx];
                if (otherCard != null && checkCard.card.Type == otherCard.card.Type)
                {
                    otherCard.scoreModifier = scoreMod;
                }
            }
        }
    }

    public override void OnScreenUpdate(TriadGameSimulationState gameData)
    {
        for (var idx = 0; idx < gameData.typeMods.Length; idx++)
        {
            gameData.typeMods[idx] = 0;
        }

        for (var idx = 0; idx < gameData.board.Length; idx++)
        {
            var checkCard = gameData.board[idx];
            if (checkCard != null && checkCard.card.Type != ETriadCardType.None)
            {
                gameData.typeMods[(int)checkCard.card.Type] += TypeStep;
            }
        }

        for (var idx = 0; idx < gameData.board.Length; idx++)
        {
            var checkCard = gameData.board[idx];
            if (checkCard != null && checkCard.card.Type != ETriadCardType.None)
            {
                checkCard.scoreModifier = gameData.typeMods[(int)checkCard.card.Type];
            }
        }
    }
}

public class TriadGameModifierAscension : TriadGameModifierTypeShift
{
    public TriadGameModifierAscension()
    {
        RuleName = "Ascension";
        RuleIndex = 9;
        Features = EFeature.CardPlaced | EFeature.PostCapture;
    }

    protected override int TypeStep => 1;

    public override void OnScoreCard(TriadCard card, ref float score)
    {
        const float scoreMult = 0.8f;
        score *= scoreMult;

        if (card.Type != ETriadCardType.None)
        {
            score += (1.0f - scoreMult);
        }
    }
}

public class TriadGameModifierDescension : TriadGameModifierTypeShift
{
    public TriadGameModifierDescension()
    {
        RuleName = "Descension";
        RuleIndex = 10;
        Features = EFeature.CardPlaced | EFeature.PostCapture;
    }

    protected override int TypeStep => -1;

    public override void OnScoreCard(TriadCard card, ref float score)
    {
        const float scoreMult = 0.5f;
        score *= scoreMult;

        if (card.Type == ETriadCardType.None)
        {
            score += (1.0f - scoreMult);
        }
    }
}

public class TriadGameModifierSwap : TriadGameModifier
{
    public TriadGameModifierSwap()
    {
        RuleName = "Swap";
        RuleIndex = 13;
        SpecialMod = ETriadGameSpecialMod.SwapCards;
    }
}

public class TriadGameModifierDraft : TriadGameModifier
{
    public TriadGameModifierDraft()
    {
        RuleName = "Draft";
        RuleIndex = 15;
        SpecialMod = ETriadGameSpecialMod.IgnoreOwnedCheck;
    }
}
