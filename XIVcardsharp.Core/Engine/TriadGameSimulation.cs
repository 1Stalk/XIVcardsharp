#nullable disable
using System.Collections.Generic;
using XIVcardsharp.Core.Data;

namespace XIVcardsharp.Core.Engine;

public class TriadGameSimulation
{
    public static int[][] cachedNeis = new int[9][];
    public TriadGameModifier.EFeature modFeatures = TriadGameModifier.EFeature.None;
    public List<TriadGameModifier> modifiers = [];
    public ETriadGameSpecialMod specialRules;

    static TriadGameSimulation()
    {
        StaticInitialize();
    }

    public TriadGameSimulationState StartGame(TriadDeck deckBlue, TriadDeck deckRed, ETriadGameState state)
    {
        foreach (var mod in modifiers)
        {
            mod.OnMatchInit();
        }

        return new()
        {
            state = state,
            deckBlue = new TriadDeckInstanceManual(deckBlue),
            deckRed = new TriadDeckInstanceManual(deckRed)
        };
    }

    public void Initialize(IEnumerable<TriadGameModifier> modsA, IEnumerable<TriadGameModifier> modsB = null)
    {
        modifiers.Clear();

        if (modsA != null)
        {
            foreach (var mod in modsA)
            {
                var modCopy = TriadGameModifierDB.CreateFresh(mod);
                modifiers.Add(modCopy);
            }
        }

        if (modsB != null)
        {
            foreach (var mod in modsB)
            {
                var modCopy = TriadGameModifierDB.CreateFresh(mod);
                modifiers.Add(modCopy);
            }
        }

        UpdateSpecialRules();
    }

    public void DeepCopyModifiersFrom(TriadGameSimulation source)
    {
        modifiers.Clear();
        foreach (var mod in source.modifiers)
        {
            modifiers.Add(CloneModifierDeep(mod));
        }

        UpdateSpecialRules();
    }

    private static TriadGameModifier CloneModifierDeep(TriadGameModifier mod)
    {
        var clone = TriadGameModifierDB.CreateFresh(mod);
        if (mod is TriadGameModifierRoulette roulette && roulette.GetResolvedRule() is { } resolvedRule)
        {
            ((TriadGameModifierRoulette)clone).SetRuleInstance(CloneModifierDeep(resolvedRule));
        }

        return clone;
    }

    public void UpdateSpecialRules()
    {
        specialRules = ETriadGameSpecialMod.None;
        modFeatures = TriadGameModifier.EFeature.None;
        foreach (var mod in modifiers)
        {
            specialRules |= mod.GetSpecialRules();
            modFeatures |= mod.GetFeatures();
        }
    }

    public bool HasSpecialRule(ETriadGameSpecialMod specialRule) => (specialRules & specialRule) != ETriadGameSpecialMod.None;

    public bool PlaceCard(TriadGameSimulationState gameState, int cardIdx, TriadDeckInstance cardDeck, ETriadCardOwner owner, int boardPos)
    {
        var bResult = false;

        var bIsAllowedOwner =
            ((owner == ETriadCardOwner.Blue) && (gameState.state == ETriadGameState.InProgressBlue)) ||
            ((owner == ETriadCardOwner.Red) && (gameState.state == ETriadGameState.InProgressRed));

        var card = cardDeck.GetCard(cardIdx);
        if (bIsAllowedOwner && (boardPos >= 0) && (boardPos < TriadGameSimulationState.boardSizeSq) &&
            (gameState.board[boardPos] == null) && (card != null))
        {
            gameState.board[boardPos] = new(card, owner);
            gameState.numCardsPlaced++;

            if (owner == ETriadCardOwner.Blue)
            {
                gameState.deckBlue.OnCardPlacedFast(cardIdx);
                gameState.state = ETriadGameState.InProgressRed;
            }
            else
            {
                gameState.deckRed.OnCardPlacedFast(cardIdx);
                gameState.state = ETriadGameState.InProgressBlue;
            }

            bResult = true;

            var bAllowCombo = false;
            if ((modFeatures & TriadGameModifier.EFeature.CardPlaced) != 0)
            {
                foreach (var mod in modifiers)
                {
                    mod.OnCardPlaced(gameState, boardPos);
                    bAllowCombo = bAllowCombo || mod.AllowsCombo();
                }
            }

            List<int> comboList = [];
            var comboCounter = 0;
            CheckCaptures(gameState, boardPos, comboList, comboCounter);

            while (bAllowCombo && comboList.Count > 0)
            {
                List<int> nextCombo = [];
                comboCounter++;
                foreach (var pos in comboList)
                {
                    CheckCaptures(gameState, pos, nextCombo, comboCounter);
                }

                comboList = nextCombo;
            }

            if ((modFeatures & TriadGameModifier.EFeature.PostCapture) != 0)
            {
                foreach (var mod in modifiers)
                {
                    mod.OnPostCaptures(gameState, boardPos);
                }
            }

            if (gameState.numCardsPlaced == gameState.board.Length)
            {
                OnAllCardsPlaced(gameState);
            }
        }

        return bResult;
    }

    public bool PlaceCard(TriadGameSimulationState gameState, TriadCard card, ETriadCardOwner owner, int boardPos)
    {
        var useDeck = (owner == ETriadCardOwner.Blue) ? gameState.deckBlue : gameState.deckRed;
        var cardIdx = useDeck.GetCardIndex(card);

        return PlaceCard(gameState, cardIdx, useDeck, owner, boardPos);
    }

    public static int GetBoardPos(int x, int y) => x + (y * TriadGameSimulationState.boardSize);

    public static void GetBoardXY(int pos, out int x, out int y)
    {
        x = pos % TriadGameSimulationState.boardSize;
        y = pos / TriadGameSimulationState.boardSize;
    }

    public static int[] GetNeighbors(int boardPos)
    {
        GetBoardXY(boardPos, out var boardPosX, out var boardPosY);

        var resultNeis = new int[4];
        resultNeis[(int)ETriadGameSide.Up] = (boardPosY > 0) ? GetBoardPos(boardPosX, boardPosY - 1) : -1;
        resultNeis[(int)ETriadGameSide.Down] = (boardPosY < (TriadGameSimulationState.boardSize - 1)) ? GetBoardPos(boardPosX, boardPosY + 1) : -1;
        resultNeis[(int)ETriadGameSide.Right] = (boardPosX > 0) ? GetBoardPos(boardPosX - 1, boardPosY) : -1;
        resultNeis[(int)ETriadGameSide.Left] = (boardPosX < (TriadGameSimulationState.boardSize - 1)) ? GetBoardPos(boardPosX + 1, boardPosY) : -1;

        return resultNeis;
    }

    private void CheckCaptures(TriadGameSimulationState gameState, int boardPos, List<int> comboList, int comboCounter)
    {
        if (boardPos < 0 || boardPos >= cachedNeis.Length || cachedNeis[boardPos] == null)
        {
            return;
        }

        var neis = cachedNeis[boardPos];
        var allowMods = comboCounter == 0;
        if (allowMods && (modFeatures & TriadGameModifier.EFeature.CaptureNei) != 0)
        {
            foreach (var mod in modifiers)
            {
                mod.OnCheckCaptureNeis(gameState, boardPos, neis, comboList);
            }
        }

        var isReverseActive = allowMods && ((modFeatures & TriadGameModifier.EFeature.CaptureMath) != 0);

        var checkCard = gameState.board[boardPos];
        for (var sideIdx = 0; sideIdx < 4; sideIdx++)
        {
            var neiPos = neis[sideIdx];
            if (neiPos >= 0 && gameState.board[neiPos] != null)
            {
                var neiCard = gameState.board[neiPos];
                if (checkCard.owner != neiCard.owner)
                {
                    var numPos = checkCard.GetNumber((ETriadGameSide)sideIdx);
                    var numOther = neiCard.GetOppositeNumber((ETriadGameSide)sideIdx);

                    if (allowMods && (modFeatures & TriadGameModifier.EFeature.CaptureWeights) != 0)
                    {
                        foreach (var mod in modifiers)
                        {
                            mod.OnCheckCaptureCardWeights(gameState, boardPos, neiPos, isReverseActive, ref numPos, ref numOther);
                        }
                    }

                    var bIsCaptured = numPos > numOther;
                    if (allowMods && (modFeatures & TriadGameModifier.EFeature.CaptureMath) != 0)
                    {
                        foreach (var mod in modifiers)
                        {
                            mod.OnCheckCaptureCardMath(gameState, boardPos, neiPos, numPos, numOther, ref bIsCaptured);
                        }
                    }

                    if (bIsCaptured)
                    {
                        neiCard.owner = checkCard.owner;
                        if (comboCounter > 0)
                        {
                            comboList.Add(neiPos);
                        }
                    }
                }
            }
        }
    }

    private void OnAllCardsPlaced(TriadGameSimulationState gameState)
    {
        var numBlue = (gameState.deckBlue.availableCardMask != 0) ? 1 : 0;
        foreach (var card in gameState.board)
        {
            if (card.owner == ETriadCardOwner.Blue)
            {
                numBlue++;
            }
        }

        var numBlueToWin = (gameState.board.Length / 2) + 1;
        gameState.state = (numBlue > numBlueToWin) ? ETriadGameState.BlueWins :
            (numBlue == numBlueToWin) ? ETriadGameState.BlueDraw :
            ETriadGameState.BlueLost;

        if ((modFeatures & TriadGameModifier.EFeature.AllPlaced) != 0)
        {
            foreach (var mod in modifiers)
            {
                mod.OnAllCardsPlaced(gameState);
            }
        }
    }

    public static void StaticInitialize()
    {
        for (var idxPos = 0; idxPos < 9; idxPos++)
        {
            cachedNeis[idxPos] = GetNeighbors(idxPos);
        }
    }
}
