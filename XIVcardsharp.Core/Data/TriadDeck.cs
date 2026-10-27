using System;
using System.Collections.Generic;

namespace XIVcardsharp.Core.Data;

public enum ETriadDeckState
{
    Valid,
    MissingCards,
    HasDuplicates,
    TooMany4Star,
    TooMany5Star
}

public class TriadDeck
{
    public string deckId = string.Empty;
    public List<TriadCard> knownCards;
    public List<TriadCard> unknownCardPool;

    public TriadDeck()
    {
        knownCards = [];
        unknownCardPool = [];
    }

    public TriadDeck(List<TriadCard> knownCards, List<TriadCard> unknownCardPool)
    {
        this.knownCards = [.. knownCards];
        this.unknownCardPool = [.. unknownCardPool];
        UpdateDeckId();
    }

    public TriadDeck(IEnumerable<TriadCard> knownCards)
    {
        this.knownCards = [.. knownCards];
        unknownCardPool = [];
        UpdateDeckId();
    }

    public void UpdateDeckId()
    {
        knownCards.Sort((a, b) => (a.Id != b.Id) ? a.Id.CompareTo(b.Id) : a.GetHashCode().CompareTo(b.GetHashCode()));
        deckId = string.Empty;
        for (var idx = 0; idx < knownCards.Count; idx++)
        {
            deckId += (idx > 0 ? " " : "") + knownCards[idx].Id;
        }
    }

    public TriadCard? GetCard(int idx) =>
        (idx < knownCards.Count) ? knownCards[idx] :
        (idx < knownCards.Count + unknownCardPool.Count) ? unknownCardPool[idx - knownCards.Count] : null;

    public int GetCardIndex(TriadCard card)
    {
        var cardIdx = knownCards.IndexOf(card);
        if (cardIdx < 0)
        {
            cardIdx = unknownCardPool.IndexOf(card);
            if (cardIdx >= 0) cardIdx += knownCards.Count;
        }
        return cardIdx;
    }
}

public abstract class TriadDeckInstance
{
    public const int maxAvailableCards = 15;
    public int availableCardMask;
    public TriadDeck? deck;
    public int numPlaced;
    public int numUnknownPlaced;

    public abstract void OnCardPlacedFast(int idx);
    public abstract int GetFirstAvailableCardFast();
    public abstract TriadCard? GetCard(int idx);
    public abstract int GetCardIndex(TriadCard card);
    public abstract TriadDeckInstance CreateCopy();

    public bool IsPlaced(int cardIdx) => (availableCardMask & (1 << cardIdx)) == 0;

    public TriadCard? GetFirstAvailableCard()
    {
        var cardIdx = GetFirstAvailableCardFast();
        return (cardIdx < 0) ? null : GetCard(cardIdx);
    }
}

public class TriadDeckInstanceManual : TriadDeckInstance
{
    public TriadDeckInstanceManual(TriadDeck deck)
    {
        this.deck = deck;
        numUnknownPlaced = 0;
        numPlaced = 0;
        availableCardMask = (1 << (deck.knownCards.Count + deck.unknownCardPool.Count)) - 1;
    }

    public TriadDeckInstanceManual(TriadDeckInstanceManual copyFrom)
    {
        deck = copyFrom.deck;
        numUnknownPlaced = copyFrom.numUnknownPlaced;
        numPlaced = copyFrom.numPlaced;
        availableCardMask = copyFrom.availableCardMask;
    }

    public override TriadDeckInstance CreateCopy() => new TriadDeckInstanceManual(this);

    public override void OnCardPlacedFast(int cardIdx)
    {
        availableCardMask &= ~(1 << cardIdx);
        numPlaced++;

        if (deck != null && cardIdx >= deck.knownCards.Count)
        {
            const int maxCardsToPlace = 5;
            var maxUnknownToPlace = maxCardsToPlace - deck.knownCards.Count;

            numUnknownPlaced++;
            if (numUnknownPlaced >= maxUnknownToPlace)
            {
                availableCardMask &= ((1 << deck.knownCards.Count) - 1);
            }
        }
    }

    public override int GetFirstAvailableCardFast()
    {
        if (deck == null) return -1;
        for (var idx = 0; idx < deck.knownCards.Count; idx++)
        {
            if ((availableCardMask & (1 << idx)) != 0)
            {
                return idx;
            }
        }
        return -1;
    }

    public override TriadCard? GetCard(int idx) => deck?.GetCard(idx);

    public override int GetCardIndex(TriadCard card) => deck?.GetCardIndex(card) ?? -1;
}

public class TriadDeckInstanceScreen : TriadDeckInstance
{
    public TriadCard?[] cards;
    public TriadCard? swappedCard;
    public int swappedCardIdx;
    public int unknownPoolMask;

    public TriadDeckInstanceScreen()
    {
        cards = new TriadCard?[5];
        availableCardMask = 0;
        unknownPoolMask = 0;
        numUnknownPlaced = 0;
        numPlaced = 0;
        swappedCardIdx = -1;
        swappedCard = null;
    }

    public TriadDeckInstanceScreen(TriadDeckInstanceScreen copyFrom)
    {
        cards = new TriadCard?[copyFrom.cards.Length];
        Array.Copy(copyFrom.cards, cards, copyFrom.cards.Length);

        deck = copyFrom.deck;
        numUnknownPlaced = copyFrom.numUnknownPlaced;
        numPlaced = copyFrom.numPlaced;
        availableCardMask = copyFrom.availableCardMask;
        unknownPoolMask = copyFrom.unknownPoolMask;
        swappedCardIdx = copyFrom.swappedCardIdx;
        swappedCard = copyFrom.swappedCard;
    }

    public override TriadDeckInstance CreateCopy() => new TriadDeckInstanceScreen(this);

    public override int GetFirstAvailableCardFast()
    {
        for (var idx = 0; idx < cards.Length; idx++)
        {
            if ((availableCardMask & (1 << idx)) != 0)
            {
                return idx;
            }
        }
        return -1;
    }

    public void UpdateAvailableCards(TriadCard?[] screenCards)
    {
        availableCardMask = 0;
        numPlaced = 0;
        numUnknownPlaced = 0;

        Array.Copy(screenCards, cards, 5);

        var hiddenCardId = TriadCardDB.Get().hiddenCard.Id;
        for (var idx = 0; idx < cards.Length; idx++)
        {
            if (cards[idx] != null)
            {
                if (cards[idx]!.Id != hiddenCardId)
                {
                    availableCardMask |= (1 << idx);
                }
            }
            else
            {
                numPlaced++;
            }
        }
    }

    public void SetSwappedCard(TriadCard? swappedCard, int swappedCardIdx)
    {
        this.swappedCard = swappedCard;
        this.swappedCardIdx = swappedCardIdx;
        unknownPoolMask &= ~(1 << swappedCardIdx);
    }

    public override void OnCardPlacedFast(int cardIdx)
    {
        var cardMask = (1 << cardIdx);
        availableCardMask &= ~cardMask;

        if (deck != null)
        {
            var isUnknown = (unknownPoolMask & cardMask) != 0;
            if (isUnknown)
            {
                numUnknownPlaced++;

                var maxUnknownToUse = cards.Length - deck.knownCards.Count;
                if (numUnknownPlaced >= maxUnknownToUse)
                {
                    availableCardMask &= ~unknownPoolMask;
                }
            }
        }
    }

    public override TriadCard? GetCard(int idx) =>
        (idx < 0) ? null :
        (idx == swappedCardIdx) ? swappedCard :
        (idx < cards.Length) ? cards[idx] :
        deck?.GetCard(idx - cards.Length);

    public override int GetCardIndex(TriadCard card)
    {
        if (card == null) return -1;
        if (card == swappedCard) return swappedCardIdx;

        var cardIdx = Array.IndexOf(cards, card);
        if (cardIdx < 0 && deck != null)
        {
            cardIdx = deck.GetCardIndex(card);
            if (cardIdx >= 0) cardIdx += cards.Length;
        }

        return cardIdx;
    }
}
