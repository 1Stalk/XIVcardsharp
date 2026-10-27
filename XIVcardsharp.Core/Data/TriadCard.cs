using System;

namespace XIVcardsharp.Core.Data;

public class TriadCard : IEquatable<TriadCard>
{
    public int Group;
    public int Id;
    public string Name = string.Empty;
    public float OptimizerScore;
    public ETriadCardRarity Rarity;
    public int SameNumberId;
    public int[] Sides;
    public int SortOrder;
    public ETriadCardType Type;

    public TriadCard()
    {
        Id = -1;
        Sides = [0, 0, 0, 0];
        SameNumberId = -1;
        SortOrder = 0;
        Group = 0;
        OptimizerScore = 0.0f;
    }

    public TriadCard(int id, ETriadCardRarity rarity, ETriadCardType type, int numUp, int numDown, int numLeft, int numRight, int sortOrder = 0, int group = 0, string name = "")
    {
        Id = id;
        Rarity = rarity;
        Type = type;
        Sides = [numUp, numLeft, numDown, numRight];
        SameNumberId = -1;
        SortOrder = sortOrder;
        Group = group;
        Name = name;

        if (group != 0 && SortOrder < 1000)
        {
            SortOrder += 1000;
        }

        OptimizerScore = CalculateCardScore(this);
    }

    public static float CalculateCardScore(TriadCard card)
    {
        var numberMax = Math.Max(Math.Max(card.Sides[0], card.Sides[1]), Math.Max(card.Sides[2], card.Sides[3]));
        var numberSum = card.Sides[0] + card.Sides[1] + card.Sides[2] + card.Sides[3];
        var numberAvg = numberSum / 4.0f;

        const float avgWeight = 1.0f;
        const float maxWeight = 0.75f;
        const float rarityWeight = 0.2f;
        const float totalWeight = avgWeight + maxWeight + rarityWeight;

        var score =
            ((numberAvg / 10.0f) * avgWeight) +
            ((numberMax / 10.0f) * maxWeight) +
            (((int)card.Rarity / (float)ETriadCardRarity.Legendary) * rarityWeight);

        return score / totalWeight;
    }

    public bool Equals(TriadCard? other) => other != null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as TriadCard);

    public override int GetHashCode() => 2108858624 + Id.GetHashCode();

    public bool IsValid() =>
        Id >= 0 &&
        Sides[0] >= 1 && Sides[0] <= 10 &&
        Sides[1] >= 1 && Sides[1] <= 10 &&
        Sides[2] >= 1 && Sides[2] <= 10 &&
        Sides[3] >= 1 && Sides[3] <= 10;

    public string ToShortCodeString() => "[" + Id + ":" + Name + "]";

    public override string ToString() =>
        string.Format("[{0}] {1} {2} [{3}, {4}, {5}, {6}]",
            Id, Name,
            new string('*', (int)Rarity + 1),
            Sides[0], Sides[1], Sides[2], Sides[3],
            (Type != ETriadCardType.None) ? " [" + Type + "]" : "");
}

public class TriadCardInstance
{
    public readonly TriadCard card;
    public ETriadCardOwner owner;
    public int scoreModifier;

    public TriadCardInstance(TriadCard card, ETriadCardOwner owner)
    {
        this.card = card;
        this.owner = owner;
        scoreModifier = 0;
    }

    public TriadCardInstance(TriadCardInstance copyFrom)
    {
        card = copyFrom.card;
        owner = copyFrom.owner;
        scoreModifier = copyFrom.scoreModifier;
    }

    public override string ToString() =>
        owner + " " + card +
        ((scoreModifier > 0) ? (" +" + scoreModifier) :
            (scoreModifier < 0) ? (" -" + scoreModifier) :
            "");

    public int GetRawNumber(ETriadGameSide side) => card.Sides[(int)side];

    public int GetNumber(ETriadGameSide side) => Math.Min(Math.Max(GetRawNumber(side) + scoreModifier, 1), 10);

    public int GetOppositeNumber(ETriadGameSide side) => GetNumber((ETriadGameSide)(((int)side + 2) % 4));
}
