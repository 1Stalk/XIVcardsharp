using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using XIVcardsharp.Core.Data;
using XIVcardsharp.Core.Engine;
using XIVcardsharp.Core.State;

using static ECommons.GenericHelpers;

namespace XIVcardsharp.Interop;

public static unsafe class TriadAddonReader
{
    public static bool TryGetBoard(out AddonTripleTriad* addon)
    {
        return TryGetAddonByName("TripleTriad", out addon) && addon != null && addon->AtkUnitBase.IsVisible;
    }

    public static bool TryReadState(AddonTripleTriad* addon, out UniversalTriadState? state)
    {
        state = null;
        if (addon is null || !addon->AtkUnitBase.IsVisible)
        {
            return false;
        }

        var hasHiddenBlue = false;
        var hasHiddenRed = false;
        for (var i = 0; i < 5; i++)
        {
            if (addon->BlueDeck[i].HasCard && addon->BlueDeck[i].NumSideU == 0) hasHiddenBlue = true;
            if (addon->RedDeck[i].HasCard && addon->RedDeck[i].NumSideU == 0) hasHiddenRed = true;
        }

        ETriadCardOwner localColor;
        if (hasHiddenBlue && !hasHiddenRed)
        {
            localColor = ETriadCardOwner.Red;
        }
        else if (hasHiddenRed && !hasHiddenBlue)
        {
            localColor = ETriadCardOwner.Blue;
        }
        else
        {
            var localPlayerName = Svc.Objects.LocalPlayer?.Name.TextValue ?? "";
            var level0 = GUINodeUtils.GetImmediateChildNodes(addon->AtkUnitBase.RootNode);
            var redDescs = GetUIDescriptionRedPlayer(level0);
            var isRedPlayerLocal = !string.IsNullOrEmpty(localPlayerName) &&
                redDescs.Any(d => d.Contains(localPlayerName, StringComparison.OrdinalIgnoreCase));
            localColor = isRedPlayerLocal ? ETriadCardOwner.Red : ETriadCardOwner.Blue;
        }

        var level0Nodes = GUINodeUtils.GetImmediateChildNodes(addon->AtkUnitBase.RootNode);
        var ruleNames = GetUIDescriptionRules(level0Nodes);

        var result = new UniversalTriadState
        {
            LocalPlayerColor = localColor,
            TurnState = (byte)addon->TurnState,
            IsPlayerTurn = TriadTurnState.ReadIsPlayerTurn(&addon->AtkUnitBase),
            TurnBannerVisible = TriadTurnState.IsTurnBannerVisible(&addon->AtkUnitBase),
            RuleNames = ruleNames,
            Modifiers = ResolveModifiers(ruleNames)
        };

        // Extract Board (0..8)
        for (var i = 0; i < 9; i++)
        {
            var addonCard = addon->Board[i];
            if (addonCard.HasCard)
            {
                var card = ResolveCard(addonCard);
                result.BoardCards[i] = card;
                result.BoardOwners[i] = (int)addonCard.CardOwner == 1 ? ETriadCardOwner.Blue :
                                        (int)addonCard.CardOwner == 2 ? ETriadCardOwner.Red :
                                        ETriadCardOwner.Unknown;
                result.NumCardsPlaced++;

                if (card != null)
                {
                    if (result.BoardOwners[i] == ETriadCardOwner.Red)
                    {
                        result.KnownRedCards.Add(card);
                    }
                    else if (result.BoardOwners[i] == ETriadCardOwner.Blue)
                    {
                        result.KnownBlueCards.Add(card);
                    }
                }
            }
        }

        // Extract Blue Hand (0..4)
        for (var i = 0; i < 5; i++)
        {
            var addonCard = addon->BlueDeck[i];
            if (!addonCard.HasCard)
            {
                result.BlueCardPlaced[i] = true;
                result.BlueHand[i] = null;
            }
            else
            {
                result.BlueCardPlaced[i] = false;
                if (addonCard.NumSideU == 0)
                {
                    result.BlueCardRevealed[i] = false;
                    result.BlueHand[i] = null;
                    result.HiddenBlueCount++;
                }
                else
                {
                    result.BlueCardRevealed[i] = true;
                    var card = ResolveCard(addonCard);
                    result.BlueHand[i] = card;
                    if (card != null)
                    {
                        result.KnownBlueCards.Add(card);
                    }
                }
            }
        }

        // Extract Red Hand (0..4)
        for (var i = 0; i < 5; i++)
        {
            var addonCard = addon->RedDeck[i];
            if (!addonCard.HasCard)
            {
                result.RedCardPlaced[i] = true;
                result.RedHand[i] = null;
            }
            else
            {
                result.RedCardPlaced[i] = false;
                if (addonCard.NumSideU == 0)
                {
                    result.RedCardRevealed[i] = false;
                    result.RedHand[i] = null;
                    result.HiddenRedCount++;
                }
                else
                {
                    result.RedCardRevealed[i] = true;
                    var card = ResolveCard(addonCard);
                    result.RedHand[i] = card;
                    if (card != null)
                    {
                        result.KnownRedCards.Add(card);
                    }
                }
            }
        }

        // Forced pick under Chaos modifier
        if (TriadTurnState.IsForcedCardPickPhase(result.TurnState))
        {
            var activeIsBlue = result.LocalPlayerColor == ETriadCardOwner.Blue;
            for (var i = 0; i < 5; i++)
            {
                var addonCard = activeIsBlue ? addon->BlueDeck[i] : addon->RedDeck[i];
                if (addonCard.HasCard)
                {
                    var isLocked = IsCardLocked(addonCard);
                    if (!isLocked)
                    {
                        result.ForcedCardIdx = i;
                        break;
                    }
                }
            }
        }

        state = result;
        return true;
    }

    private static TriadCard? ResolveCard(AddonTripleTriad.TripleTriadCard addonCard)
    {
        if (!addonCard.HasCard)
        {
            return null;
        }

        if (addonCard.NumSideU == 0)
        {
            return TriadCardDB.Get().hiddenCard;
        }

        var numU = addonCard.NumSideU;
        var numL = addonCard.NumSideL;
        var numD = addonCard.NumSideD;
        var numR = addonCard.NumSideR;

        var matched = TriadCardDB.Get().Find(numU, numL, numD, numR);
        if (matched != null && matched.IsValid())
        {
            return matched;
        }

        var texPath = GetCardTexture(addonCard);
        if (!string.IsNullOrEmpty(texPath))
        {
            var texFallback = TriadCardDB.Get().FindByTexture(texPath);
            if (texFallback != null && texFallback.IsValid())
            {
                return texFallback;
            }
        }

        return new TriadCard(
            -1,
            (ETriadCardRarity)Math.Clamp((int)addonCard.CardRarity, 0, 4),
            (ETriadCardType)Math.Clamp((int)addonCard.CardType, 0, 4),
            numU, numD, numL, numR,
            0, 0,
            $"Card [{numU}-{numL}-{numD}-{numR}]");
    }

    private static bool IsCardLocked(AddonTripleTriad.TripleTriadCard addonCard)
    {
        var nodeA = GUINodeUtils.PickChildNode(addonCard.CardDropControl, 1, 3);
        var nodeB = GUINodeUtils.PickChildNode(nodeA, 0, 2);
        return nodeB is not null && nodeB->MultiplyRed < 100;
    }

    private static string? GetCardTexture(AddonTripleTriad.TripleTriadCard addonCard)
    {
        var nodeA = GUINodeUtils.PickChildNode(addonCard.CardDropControl, 1, 3);
        var nodeB = GUINodeUtils.PickChildNode(nodeA, 0, 2);
        var nodeC = GUINodeUtils.PickChildNode(nodeB, 3, 21);
        return GUINodeUtils.GetNodeTexturePath(nodeC);
    }

    private static List<string> GetUIDescriptionRules(AtkResNode*[]? level0)
    {
        var listRuleDesc = new List<string>();
        var nodeRule0 = GUINodeUtils.PickNode(level0, 4, 12);
        var nodeArrRule = GUINodeUtils.GetImmediateChildNodes(nodeRule0);
        if (nodeArrRule is { Length: 5 })
        {
            for (var idx = 0; idx < 4; idx++)
            {
                var text = GUINodeUtils.GetNodeText(nodeArrRule[4 - idx]);
                if (!string.IsNullOrEmpty(text))
                {
                    listRuleDesc.Add(text);
                }
            }
        }
        else if (nodeArrRule is not null)
        {
            foreach (var node in nodeArrRule)
            {
                var text = GUINodeUtils.GetNodeText(node);
                if (!string.IsNullOrEmpty(text))
                {
                    listRuleDesc.Add(text);
                }
            }
        }

        return [.. listRuleDesc
            .SelectMany(r => r.Split(['/', ',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct()];
    }

    private static List<string> GetUIDescriptionRedPlayer(AtkResNode*[]? level0)
    {
        var listRedDesc = new List<string>();
        var nodeName0 = GUINodeUtils.PickNode(level0, 6, 12);
        var nodeArrNameL1 = GUINodeUtils.GetImmediateChildNodes(nodeName0);
        var nodeNameL1 = GUINodeUtils.PickNode(nodeArrNameL1, 0, 5);
        var nodeArrNameL2 = GUINodeUtils.GetAllChildNodes(nodeNameL1);
        if (nodeArrNameL2 is not null)
        {
            foreach (var testNode in nodeArrNameL2)
            {
                if (!GUINodeUtils.IsNodeVisible(testNode)) continue;
                var text = GUINodeUtils.GetNodeText(testNode);
                if (!string.IsNullOrEmpty(text))
                {
                    listRedDesc.Add(text);
                }
            }
        }
        return listRedDesc;
    }

    private static List<TriadGameModifier> ResolveModifiers(List<string> ruleNames)
    {
        var result = new List<TriadGameModifier>();
        var db = TriadGameModifierDB.Get();

        foreach (var rule in ruleNames)
        {
            foreach (var mod in db.mods)
            {
                if (mod.GetCodeName().Equals(rule, StringComparison.OrdinalIgnoreCase) ||
                    mod.GetLocalizedName().Equals(rule, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(TriadGameModifierDB.CreateFresh(mod));
                    break;
                }
            }
        }

        return result;
    }
}
