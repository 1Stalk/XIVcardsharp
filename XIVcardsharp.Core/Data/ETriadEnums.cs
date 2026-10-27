namespace XIVcardsharp.Core.Data;

public enum ETriadCardRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public enum ETriadCardType
{
    None,
    Beastman,
    Primal,
    Scion,
    Garlean
}

public enum ETriadCardOwner
{
    Unknown,
    Blue,
    Red
}

public enum ETriadGameSide
{
    Up,
    Left,
    Down,
    Right
}

public enum ETriadGameState
{
    InProgressBlue,
    InProgressRed,
    BlueWins,
    BlueDraw,
    BlueLost
}

[Flags]
public enum ETriadGameSpecialMod
{
    None = 0,
    SelectVisible3 = 0x1,
    SelectVisible5 = 0x2,
    RandomizeRule = 0x4,
    RandomizeBlueDeck = 0x8,
    SwapCards = 0x10,
    BlueCardSelection = 0x20,
    IgnoreOwnedCheck = 0x40
}
