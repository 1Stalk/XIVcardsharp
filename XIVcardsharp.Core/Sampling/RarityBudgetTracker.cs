using System;
using System.Collections.Generic;
using System.Linq;
using XIVcardsharp.Core.Data;
using XIVcardsharp.Core.Engine;
using XIVcardsharp.Core.State;

namespace XIVcardsharp.Core.Sampling;

public class RarityBudgetTracker
{
    public UniversalEngineMode Mode { get; }
    public Dictionary<ETriadCardRarity, int> KnownCounts { get; } = [];
    public (ETriadCardRarity? GivenTier, ETriadCardRarity? TakenTier) SwappedTiers { get; }
    public IReadOnlyList<TriadCard> InitialPlayerCards { get; }
    public IReadOnlyList<TriadCard> InitialBlueCards => InitialPlayerCards;

    public RarityBudgetTracker(
        UniversalEngineMode mode,
        IEnumerable<TriadCard> knownOpponentCards,
        (ETriadCardRarity? given, ETriadCardRarity? taken) swappedTiers = default,
        IReadOnlyList<TriadCard>? initialPlayerCards = null)
    {
        Mode = mode;
        SwappedTiers = swappedTiers;
        InitialPlayerCards = initialPlayerCards ?? [];
        foreach (var r in Enum.GetValues<ETriadCardRarity>())
        {
            KnownCounts[r] = 0;
        }

        foreach (var card in knownOpponentCards)
        {
            if (card != null && card.IsValid())
            {
                KnownCounts[card.Rarity]++;
            }
        }
    }

    public static (ETriadCardRarity? givenTier, ETriadCardRarity? takenTier) DetectDraftSwap(UniversalTriadState state)
    {
        var hasSwap = state.Modifiers.Exists(m => m is TriadGameModifierSwap) ||
            state.RuleNames.Exists(r =>
                r.Contains("Swap", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("Échange", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("Tausch", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("スワップ", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("交换", StringComparison.OrdinalIgnoreCase));

        if (!hasSwap)
        {
            return (null, null);
        }

        // Method 1: Compare pre-swap InitialPlayerCards with current LocalPlayerHand
        if (state.InitialPlayerCards.Count == 5)
        {
            var given = state.InitialPlayerCards.FirstOrDefault(c => c != null && !state.LocalPlayerHand.Any(h => h != null && h.Id == c.Id));
            var taken = state.LocalPlayerHand.FirstOrDefault(h => h != null && !state.InitialPlayerCards.Any(c => c != null && c.Id == h.Id));
            if (given != null && taken != null)
            {
                return (given.Rarity, taken.Rarity);
            }
        }

        // Method 2: Check rarity frequency distribution for missing (0) and extra (>1) tiers
        var sourceCards = state.LocalPlayerHand.Where(c => c != null && c.IsValid()).Select(c => c!).ToList();
        if (sourceCards.Count != 5 && state.InitialPlayerCards.Count == 5)
        {
            sourceCards = [.. state.InitialPlayerCards.Where(c => c != null && c.IsValid())];
        }

        if (sourceCards.Count != 5)
        {
            return (null, null);
        }

        var counts = new Dictionary<ETriadCardRarity, int>();
        foreach (var r in Enum.GetValues<ETriadCardRarity>())
        {
            counts[r] = 0;
        }

        foreach (var card in sourceCards)
        {
            if (card != null && card.IsValid())
            {
                counts[card.Rarity]++;
            }
        }

        ETriadCardRarity? givenTier = null;
        ETriadCardRarity? takenTier = null;

        foreach (var (rarity, count) in counts)
        {
            if (count == 0)
            {
                givenTier = rarity;
            }
            else if (count > 1)
            {
                takenTier = rarity;
            }
        }

        return (givenTier, takenTier);
    }

    public List<ETriadCardRarity> GetDraftRemainingTiers(int hiddenCount, Random? rng = null)
    {
        var random = rng ?? Random.Shared;
        var tiers = new List<ETriadCardRarity>();

        var max5 = 1 + (SwappedTiers.GivenTier == ETriadCardRarity.Legendary ? 1 : 0) - (SwappedTiers.TakenTier == ETriadCardRarity.Legendary ? 1 : 0);
        max5 = Math.Max(0, max5);

        var max4 = 2 + (SwappedTiers.GivenTier == ETriadCardRarity.Epic ? 1 : 0) - (SwappedTiers.TakenTier == ETriadCardRarity.Epic ? 1 : 0);
        max4 = Math.Max(0, max4);

        var canHave5 = KnownCounts[ETriadCardRarity.Legendary] < max5;
        var remaining4 = Math.Max(0, max4 - KnownCounts[ETriadCardRarity.Epic]);

        for (var i = 0; i < hiddenCount; i++)
        {
            if (canHave5)
            {
                tiers.Add(ETriadCardRarity.Legendary);
                canHave5 = false;
            }
            else if (remaining4 > 0 && random.Next(100) < 60)
            {
                tiers.Add(ETriadCardRarity.Epic);
                remaining4--;
            }
            else
            {
                var roll = random.Next(100);
                if (roll < 55)
                {
                    tiers.Add(ETriadCardRarity.Rare);
                }
                else if (roll < 85)
                {
                    tiers.Add(ETriadCardRarity.Uncommon);
                }
                else
                {
                    tiers.Add(ETriadCardRarity.Common);
                }
            }
        }

        return tiers;
    }

    public List<ETriadCardRarity> GetUniversalRemainingTiers(int hiddenCount, Random? rng = null)
    {
        var tiers = new List<ETriadCardRarity>();
        var canHave5 = KnownCounts[ETriadCardRarity.Legendary] == 0;
        var canHave4 = (KnownCounts[ETriadCardRarity.Legendary] + KnownCounts[ETriadCardRarity.Epic]) < 2;

        for (var i = 0; i < hiddenCount; i++)
        {
            if (canHave5)
            {
                tiers.Add(ETriadCardRarity.Legendary);
                canHave5 = false;
            }
            else if (canHave4)
            {
                tiers.Add(ETriadCardRarity.Epic);
                canHave4 = false;
            }
            else
            {
                var random = rng ?? Random.Shared;
                var roll = random.Next(100);
                if (roll < 80)
                {
                    tiers.Add(ETriadCardRarity.Rare);
                }
                else if (roll < 95)
                {
                    tiers.Add(ETriadCardRarity.Uncommon);
                }
                else
                {
                    tiers.Add(ETriadCardRarity.Common);
                }
            }
        }

        return tiers;
    }
}
