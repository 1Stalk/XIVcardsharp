using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using XIVcardsharp.Core.Data;
using XIVcardsharp.Core.Draft;

using static ECommons.GenericHelpers;

namespace XIVcardsharp.Interop;

public static unsafe class TournamentDraftReader
{
    public const string DraftAddonName = "TripleTriadPickUpDeckSelect";
    public const string ConfirmationAddonName = "TripleTriadDeckConfirmation";

    public static readonly uint[] Set1NodeIds = [11, 12, 13];
    public static readonly uint[] Set2NodeIds = [25, 26, 27];
    public static readonly uint[] Set3NodeIds = [39, 40, 41];

    public static bool TryGetDraftAddon(out AtkUnitBase* addon)
    {
        return TryGetAddonByName(DraftAddonName, out addon) && addon != null && addon->IsVisible;
    }

    public static bool TryGetConfirmationAddon(out AtkUnitBase* addon)
    {
        return TryGetAddonByName(ConfirmationAddonName, out addon) && addon != null && addon->IsVisible;
    }

    public static TournamentDraftState? ReadDraftState()
    {
        if (!TryGetDraftAddon(out var addon))
        {
            return null;
        }

        var state = new TournamentDraftState
        {
            Rules = ExtractRules(addon)
        };

        for (var i = 0; i < Set1NodeIds.Length; i++)
        {
            var opt = ReadOption(addon, 0, i, Set1NodeIds[i], isPair: true);
            if (opt != null) state.Set1Options.Add(opt);
        }

        for (var i = 0; i < Set2NodeIds.Length; i++)
        {
            var opt = ReadOption(addon, 1, i, Set2NodeIds[i], isPair: true);
            if (opt != null) state.Set2Options.Add(opt);
        }

        for (var i = 0; i < Set3NodeIds.Length; i++)
        {
            var opt = ReadOption(addon, 2, i, Set3NodeIds[i], isPair: false);
            if (opt != null) state.Set3Options.Add(opt);
        }

        return state.IsValid ? state : null;
    }

    private static List<string> ExtractRules(AtkUnitBase* addon)
    {
        var rules = new List<string>();

        var ruleNode = addon->GetTextNodeById(5);
        if (ruleNode != null && ruleNode->AtkResNode.IsVisible())
        {
            var text = ruleNode->NodeText.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var parts = text.Split(['/', ',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                rules.AddRange(parts.Distinct());
            }
        }

        if (rules.Count == 0)
        {
            rules.Add("Draft");
        }

        return rules;
    }

    private static TournamentDraftOption? ReadOption(
        AtkUnitBase* addon,
        int setIndex,
        int optionIndex,
        uint nodeId,
        bool isPair)
    {
        var compNode = (AtkComponentNode*)addon->GetNodeById(nodeId);
        if (compNode == null || compNode->Component == null)
        {
            return null;
        }

        var opt = new TournamentDraftOption
        {
            SetIndex = setIndex,
            OptionIndex = optionIndex,
            NodeId = nodeId
        };

        var card1 = ReadCardFromComponent(compNode->Component, 2);
        if (card1 != null)
        {
            opt.Cards.Add(card1);
        }

        if (isPair)
        {
            var card2 = ReadCardFromComponent(compNode->Component, 3);
            if (card2 != null)
            {
                opt.Cards.Add(card2);
            }
        }

        return opt.Cards.Count > 0 ? opt : null;
    }

    private static TournamentDraftCard? ReadCardFromComponent(AtkComponentBase* parentComp, uint cardSubNodeId)
    {
        var cardNode = (AtkComponentNode*)parentComp->UldManager.SearchNodeById(cardSubNodeId);
        if (cardNode == null || cardNode->Component == null)
        {
            return null;
        }

        var cardComp = cardNode->Component;

        var upNode = (AtkTextNode*)cardComp->UldManager.SearchNodeById(14);
        var downNode = (AtkTextNode*)cardComp->UldManager.SearchNodeById(15);
        var leftNode = (AtkTextNode*)cardComp->UldManager.SearchNodeById(16);
        var rightNode = (AtkTextNode*)cardComp->UldManager.SearchNodeById(17);

        var numU = ParseStat(upNode != null ? upNode->NodeText.ToString() : null);
        var numD = ParseStat(downNode != null ? downNode->NodeText.ToString() : null);
        var numL = ParseStat(leftNode != null ? leftNode->NodeText.ToString() : null);
        var numR = ParseStat(rightNode != null ? rightNode->NodeText.ToString() : null);

        if (numU == 0 && numD == 0 && numL == 0 && numR == 0)
        {
            return null;
        }

        var matched = TriadCardDB.Get().Find(numU, numL, numD, numR);
        if (matched != null)
        {
            return new TournamentDraftCard(
                matched.Name,
                numU,
                numL,
                numD,
                numR,
                (int)matched.Rarity,
                matched.Type.ToString());
        }

        return new TournamentDraftCard(
            $"Card [{numU}-{numL}-{numD}-{numR}]",
            numU,
            numL,
            numD,
            numR,
            1);
    }

    private static int ParseStat(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        s = s.Trim();
        if (string.Equals(s, "A", StringComparison.OrdinalIgnoreCase)) return 10;
        if (int.TryParse(s, out var v)) return v;
        return 0;
    }
}
