using System.Collections.Generic;
using XIVcardsharp.Core.Data;
using XIVcardsharp.Core.Engine;
using XIVcardsharp.Core.Solving;
using XIVcardsharp.Core.State;
using Xunit;

namespace XIVcardsharp.Tests;

public class CardIdentificationTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public CardIdentificationTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    private static void SeedWeaponCards(TriadCardDB cardDb)
    {
        while (cardDb.cards.Count <= 312)
        {
            cardDb.cards.Add(null);
        }

        // Ultima Weapon: Card 51, ★4 (Epic), None, Top: 7, Bottom: 9, Left: 1, Right: 8
        cardDb.cards[51] = new TriadCard(51, ETriadCardRarity.Epic, ETriadCardType.None, 7, 9, 1, 8, name: "Ultima Weapon");

        // Emerald Weapon: Card 312, ★4 (Epic), None, Top: 7, Bottom: 9, Left: 8, Right: 1
        cardDb.cards[312] = new TriadCard(312, ETriadCardRarity.Epic, ETriadCardType.None, 7, 9, 8, 1, name: "Emerald Weapon");

        // Ananta: Card 185, ★2 (Uncommon), Beastman, Top: 3, Bottom: 5, Left: 2, Right: 7
        while (cardDb.cards.Count <= 185)
        {
            cardDb.cards.Add(null);
        }
        cardDb.cards[185] = new TriadCard(185, ETriadCardRarity.Uncommon, ETriadCardType.Beastman, 3, 5, 2, 7, name: "Ananta");

        // Dodo: Card 1, ★1 (Common), None
        cardDb.cards[1] = new TriadCard(1, ETriadCardRarity.Common, ETriadCardType.None, 4, 2, 4, 3, name: "Dodo");
    }

    [Fact]
    public void EmeraldWeapon_And_UltimaWeapon_HaveCorrectDistinctSides()
    {
        var cardDb = TriadCardDB.Get();
        SeedWeaponCards(cardDb);

        var ultima = cardDb.FindById(51);
        var emerald = cardDb.FindById(312);

        Assert.NotNull(ultima);
        Assert.NotNull(emerald);

        // Ultima Weapon has Left=1, Right=8
        Assert.Equal(7, ultima.Sides[(int)ETriadGameSide.Up]);
        Assert.Equal(1, ultima.Sides[(int)ETriadGameSide.Left]);
        Assert.Equal(9, ultima.Sides[(int)ETriadGameSide.Down]);
        Assert.Equal(8, ultima.Sides[(int)ETriadGameSide.Right]);

        // Emerald Weapon has Left=8, Right=1
        Assert.Equal(7, emerald.Sides[(int)ETriadGameSide.Up]);
        Assert.Equal(8, emerald.Sides[(int)ETriadGameSide.Left]);
        Assert.Equal(9, emerald.Sides[(int)ETriadGameSide.Down]);
        Assert.Equal(1, emerald.Sides[(int)ETriadGameSide.Right]);
    }

    [Fact]
    public void TriadCardDB_Find_CorrectlyDistinguishes_Emerald_From_Ultima()
    {
        var cardDb = TriadCardDB.Get();
        SeedWeaponCards(cardDb);

        // Emerald Weapon lookup: U=7, L=8, D=9, R=1
        var matchedEmerald = cardDb.Find(7, 8, 9, 1);
        Assert.NotNull(matchedEmerald);
        Assert.Equal(312, matchedEmerald.Id);
        Assert.Equal("Emerald Weapon", matchedEmerald.Name);

        // Ultima Weapon lookup: U=7, L=1, D=9, R=8
        var matchedUltima = cardDb.Find(7, 1, 9, 8);
        Assert.NotNull(matchedUltima);
        Assert.Equal(51, matchedUltima.Id);
        Assert.Equal("Ultima Weapon", matchedUltima.Name);
    }

    [Fact]
    public void TriadCardDB_FindByTexture_ResolvesAllTextureFormats()
    {
        var cardDb = TriadCardDB.Get();
        SeedWeaponCards(cardDb);

        // 087000 card art (104x128)
        var texEmerald = cardDb.FindByTexture("ui/icon/087000/087312.tex");
        Assert.NotNull(texEmerald);
        Assert.Equal(312, texEmerald.Id);
        Assert.Equal("Emerald Weapon", texEmerald.Name);

        var texUltima = cardDb.FindByTexture("ui/icon/087000/087051.tex");
        Assert.NotNull(texUltima);
        Assert.Equal(51, texUltima.Id);
        Assert.Equal("Ultima Weapon", texUltima.Name);

        // 088000 small icon (40x40)
        var iconEmerald = cardDb.FindByTexture("ui/icon/088000/088312.tex");
        Assert.NotNull(iconEmerald);
        Assert.Equal(312, iconEmerald.Id);

        // Legacy 082000 formats:
        // rawId < 100: direct card ID (082051 -> 51)
        var legacyUltima = cardDb.FindByTexture("ui/icon/082000/082051.tex");
        Assert.NotNull(legacyUltima);
        Assert.Equal(51, legacyUltima.Id);

        // rawId >= 100: card ID = rawId - 100 (082101 -> 1)
        var legacyDodo = cardDb.FindByTexture("ui/icon/082000/082101.tex");
        Assert.NotNull(legacyDodo);
        Assert.Equal(1, legacyDodo.Id);

        // Relative path starting with pattern
        var relPath = cardDb.FindByTexture("087000/087312.tex");
        Assert.NotNull(relPath);
        Assert.Equal(312, relPath.Id);

        // Windows backslash path
        var winPath = cardDb.FindByTexture("ui\\icon\\087000\\087312.tex");
        Assert.NotNull(winPath);
        Assert.Equal(312, winPath.Id);

        // Direct short file name
        var shortName = cardDb.FindByTexture("087312.tex");
        Assert.NotNull(shortName);
        Assert.Equal(312, shortName.Id);

        var shortIconName = cardDb.FindByTexture("088051.tex");
        Assert.NotNull(shortIconName);
        Assert.Equal(51, shortIconName.Id);
    }

    [Fact]
    public void TriadCardDB_FindById_HandlesSparseUnpaddedList()
    {
        var cardDb = TriadCardDB.Get();
        cardDb.cards.Clear();

        // Sparse list where Count is only 2, but card Id is 312
        cardDb.cards.Add(new TriadCard(1, ETriadCardRarity.Common, ETriadCardType.None, 1, 1, 1, 1, name: "Card 1"));
        cardDb.cards.Add(new TriadCard(312, ETriadCardRarity.Epic, ETriadCardType.None, 7, 9, 8, 1, name: "Emerald Weapon"));

        var found = cardDb.FindById(312);
        Assert.NotNull(found);
        Assert.Equal(312, found.Id);
        Assert.Equal("Emerald Weapon", found.Name);
    }

    [Fact]
    public void TriadCardDB_Find_DisambiguatesDuplicateStats_ViaTypeAndRarity()
    {
        var cardDb = TriadCardDB.Get();
        cardDb.cards.Clear();

        // Duplicate stat pair: #118 Senor Sabotender (None, Epic) vs #403 Asura (Primal, Epic)
        // Stats: Top: 9, Bottom: 7, Left: 6, Right: 5
        var sabotender = new TriadCard(118, ETriadCardRarity.Epic, ETriadCardType.None, 9, 7, 6, 5, name: "Senor Sabotender");
        var asura = new TriadCard(403, ETriadCardRarity.Epic, ETriadCardType.Primal, 9, 7, 6, 5, name: "Asura");

        cardDb.cards.Add(sabotender);
        cardDb.cards.Add(asura);

        // Stat-only search returns first match (Sabotender)
        var genericMatch = cardDb.Find(9, 6, 7, 5);
        Assert.NotNull(genericMatch);
        Assert.Equal(118, genericMatch.Id);

        // Type and rarity disambiguation correctly finds Asura
        var asuraMatch = cardDb.Find(9, 6, 7, 5, ETriadCardType.Primal, ETriadCardRarity.Epic);
        Assert.NotNull(asuraMatch);
        Assert.Equal(403, asuraMatch.Id);
        Assert.Equal("Asura", asuraMatch.Name);

        // Type and rarity disambiguation correctly finds Senor Sabotender
        var saboMatch = cardDb.Find(9, 6, 7, 5, ETriadCardType.None, ETriadCardRarity.Epic);
        Assert.NotNull(saboMatch);
        Assert.Equal(118, saboMatch.Id);
        Assert.Equal("Senor Sabotender", saboMatch.Name);
    }

    [Fact]
    public void MirroredCardPairs_AreNotConfused_WhenCorrectSidesSupplied()
    {
        var cardDb = TriadCardDB.Get();
        cardDb.cards.Clear();

        // Pair 1: Ultima Weapon (51) vs Emerald Weapon (312)
        var ultima = new TriadCard(51, ETriadCardRarity.Epic, ETriadCardType.None, 7, 9, 1, 8, name: "Ultima Weapon");
        var emerald = new TriadCard(312, ETriadCardRarity.Epic, ETriadCardType.None, 7, 9, 8, 1, name: "Emerald Weapon");

        // Pair 2: G-Warrior (323) vs Diamond Weapon (324)
        var gwarrior = new TriadCard(323, ETriadCardRarity.Legendary, ETriadCardType.None, 9, 7, 8, 1, name: "G-Warrior");
        var diamond = new TriadCard(324, ETriadCardRarity.Legendary, ETriadCardType.None, 9, 7, 1, 8, name: "Diamond Weapon");

        // Pair 3: Hancock (219) vs Ruby Weapon (291)
        var hancock = new TriadCard(219, ETriadCardRarity.Epic, ETriadCardType.None, 8, 1, 7, 9, name: "Hancock");
        var ruby = new TriadCard(291, ETriadCardRarity.Epic, ETriadCardType.None, 8, 1, 9, 7, name: "Ruby Weapon");

        cardDb.cards.AddRange([null, null]);
        while (cardDb.cards.Count <= 324) cardDb.cards.Add(null);
        cardDb.cards[51] = ultima;
        cardDb.cards[312] = emerald;
        cardDb.cards[323] = gwarrior;
        cardDb.cards[324] = diamond;
        cardDb.cards[219] = hancock;
        cardDb.cards[291] = ruby;

        Assert.Equal("Emerald Weapon", cardDb.Find(7, 8, 9, 1)?.Name);
        Assert.Equal("Ultima Weapon", cardDb.Find(7, 1, 9, 8)?.Name);
        Assert.Equal("G-Warrior", cardDb.Find(9, 8, 7, 1)?.Name);
        Assert.Equal("Diamond Weapon", cardDb.Find(9, 1, 7, 8)?.Name);
        Assert.Equal("Hancock", cardDb.Find(8, 7, 1, 9)?.Name);
        Assert.Equal("Ruby Weapon", cardDb.Find(8, 9, 1, 7)?.Name);
    }

    [Fact]
    public void Ananta_Captures_EmeraldWeapon_AtCorner_ButCannotCapture_UltimaWeapon()
    {
        var cardDb = TriadCardDB.Get();
        SeedWeaponCards(cardDb);

        var emerald = cardDb.FindById(312)!;
        var ultima = cardDb.FindById(51)!;
        var ananta = cardDb.FindById(185)!;

        // Case A: Emerald Weapon placed at Tile (0, 0)
        // Emerald Weapon has Right = 1 facing tile (1, 0)
        var sim = new TriadGameSimulation();
        var blueDeckA = new TriadDeck([emerald, emerald, emerald, emerald, emerald]);
        var redDeckA = new TriadDeck([ananta, ananta, ananta, ananta, ananta]);
        var stateA = sim.StartGame(blueDeckA, redDeckA, ETriadGameState.InProgressBlue);

        // Blue plays Emerald Weapon at tile 0 (x=0, y=0)
        sim.PlaceCard(stateA, 0, stateA.deckBlue, ETriadCardOwner.Blue, 0);
        Assert.Equal(ETriadCardOwner.Blue, stateA.board[0].owner);

        // Red plays Ananta at tile 1 (x=1, y=0)
        // Ananta Left = 2 meets Emerald Weapon Right = 1 => 2 > 1 => Flip!
        sim.PlaceCard(stateA, 0, stateA.deckRed, ETriadCardOwner.Red, 1);
        Assert.Equal(ETriadCardOwner.Red, stateA.board[0].owner); // Captured!

        // Case B: Ultima Weapon placed at Tile (0, 0)
        // Ultima Weapon has Right = 8 facing tile (1, 0)
        var blueDeckB = new TriadDeck([ultima, ultima, ultima, ultima, ultima]);
        var redDeckB = new TriadDeck([ananta, ananta, ananta, ananta, ananta]);
        var stateB = sim.StartGame(blueDeckB, redDeckB, ETriadGameState.InProgressBlue);

        // Blue plays Ultima Weapon at tile 0 (x=0, y=0)
        sim.PlaceCard(stateB, 0, stateB.deckBlue, ETriadCardOwner.Blue, 0);
        Assert.Equal(ETriadCardOwner.Blue, stateB.board[0].owner);

        // Red plays Ananta at tile 1 (x=1, y=0)
        // Ananta Left = 2 meets Ultima Weapon Right = 8 => 2 < 8 => No flip!
        sim.PlaceCard(stateB, 0, stateB.deckRed, ETriadCardOwner.Red, 1);
        Assert.Equal(ETriadCardOwner.Blue, stateB.board[0].owner); // Blue retains ownership!
    }

    [Fact]
    public void PositionalSafety_RatesUltimaSignificantlyHigherThanEmerald_AtTopLeftCorner()
    {
        var cardDb = TriadCardDB.Get();
        SeedWeaponCards(cardDb);

        var emerald = cardDb.FindById(312)!;
        var ultima = cardDb.FindById(51)!;
        var state = new UniversalTriadState { NumCardsPlaced = 0 };

        // Corner 0: Left and Up face walls. Down and Right are exposed.
        // Ultima Weapon has Right=8 and Down=9 exposed (both very strong!).
        // Emerald Weapon has Right=1 and Down=9 exposed (Right=1 is very weak!).
        var safetyUltima = PositionalSafetyEvaluator.Evaluate(state, ultima, 0);
        var safetyEmerald = PositionalSafetyEvaluator.Evaluate(state, emerald, 0);

        Assert.True(safetyUltima > safetyEmerald,
            $"Ultima Weapon positional safety ({safetyUltima}) at corner (0,0) must exceed Emerald Weapon ({safetyEmerald}) due to exposed Right 8 vs 1");
    }

    [Fact]
    public void FFXIVClientStructs_TripleTriadCard_Offsets_AreInspected()
    {
        var type = typeof(FFXIVClientStructs.FFXIV.Client.UI.AddonTripleTriad.TripleTriadCard);
        var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var offsetMap = new Dictionary<string, int>();
        foreach (var field in fields)
        {
            var attr = (System.Runtime.InteropServices.FieldOffsetAttribute?)Attribute.GetCustomAttribute(field, typeof(System.Runtime.InteropServices.FieldOffsetAttribute));
            if (attr != null)
            {
                offsetMap[field.Name] = attr.Value;
            }
        }

        Assert.Equal(0x83, offsetMap["NumSideU"]);
        Assert.Equal(0x84, offsetMap["NumSideD"]);
        Assert.Equal(0x85, offsetMap["NumSideR"]);
        Assert.Equal(0x86, offsetMap["NumSideL"]);
    }

    [Fact]
    public void Verify_Lumina_EmeraldAndUltima_GameData()
    {
        var sqpackPath = Environment.GetEnvironmentVariable("FFXIV_SQPACK_PATH") ?? @"C:\Games\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack";
        if (!System.IO.Directory.Exists(sqpackPath)) return;

        var lumina = new Lumina.GameData(sqpackPath);
        var cardResidentSheet = lumina.GetExcelSheet<Lumina.Excel.Sheets.TripleTriadCardResident>();
        var cardSheet = lumina.GetExcelSheet<Lumina.Excel.Sheets.TripleTriadCard>();
        if (cardResidentSheet == null || cardSheet == null) return;

        // Ultima Weapon is row 51
        var ultimaResident = cardResidentSheet.GetRowOrDefault(51);
        var ultimaName = cardSheet.GetRowOrDefault(51);

        // Emerald Weapon is row 312
        var emeraldResident = cardResidentSheet.GetRowOrDefault(312);
        var emeraldName = cardSheet.GetRowOrDefault(312);

        Assert.True(ultimaResident.HasValue);
        Assert.True(ultimaName.HasValue);
        Assert.True(emeraldResident.HasValue);
        Assert.True(emeraldName.HasValue);

        Assert.Equal("Ultima Weapon", ultimaName.Value.Name.ToString());
        Assert.Equal(7, ultimaResident.Value.Top);
        Assert.Equal(9, ultimaResident.Value.Bottom);
        Assert.Equal(1, ultimaResident.Value.Left);
        Assert.Equal(8, ultimaResident.Value.Right);

        Assert.Equal("Emerald Weapon", emeraldName.Value.Name.ToString());
        Assert.Equal(7, emeraldResident.Value.Top);
        Assert.Equal(9, emeraldResident.Value.Bottom);
        Assert.Equal(8, emeraldResident.Value.Left);
        Assert.Equal(1, emeraldResident.Value.Right);
    }

    [Fact]
    public void Verify_UserHand_Cards_In_Lumina()
    {
        var sqpackPath = Environment.GetEnvironmentVariable("FFXIV_SQPACK_PATH") ?? @"C:\Games\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack";
        if (!System.IO.Directory.Exists(sqpackPath)) return;

        var lumina = new Lumina.GameData(sqpackPath);
        var cardResidentSheet = lumina.GetExcelSheet<Lumina.Excel.Sheets.TripleTriadCardResident>();
        var cardSheet = lumina.GetExcelSheet<Lumina.Excel.Sheets.TripleTriadCard>();
        if (cardResidentSheet == null || cardSheet == null) return;

        // Find Garuda, Ixal, Stormblood Alphinaud & Alisaie, Ultros & Typhon
        for (uint i = 0; i < cardResidentSheet.Count; i++)
        {
            var res = cardResidentSheet.GetRowOrDefault(i);
            var card = cardSheet.GetRowOrDefault(i);
            if (!res.HasValue || !card.HasValue) continue;
            var name = card.Value.Name.ToString();
            if (name == "Garuda")
            {
                // In log: [U:7 L:7 D:1 R:6]
                Assert.Equal(7, res.Value.Top);
                Assert.Equal(1, res.Value.Bottom);
                Assert.Equal(7, res.Value.Left);
                Assert.Equal(6, res.Value.Right);
            }
            else if (name == "Ixal")
            {
                // In log: [U:6 L:4 D:3 R:1]
                Assert.Equal(6, res.Value.Top);
                Assert.Equal(3, res.Value.Bottom);
                Assert.Equal(4, res.Value.Left);
                Assert.Equal(1, res.Value.Right);
            }
            else if (name == "Ultros & Typhon")
            {
                // In log: [U:2 L:7 D:6 R:3]
                Assert.Equal(2, res.Value.Top);
                Assert.Equal(6, res.Value.Bottom);
                Assert.Equal(7, res.Value.Left);
                Assert.Equal(3, res.Value.Right);
            }
        }
    }

    [Fact]
    public void Verify_AllMirroredPairs_In_RealGameData_ResolveCorrectly()
    {
        var sqpackPath = Environment.GetEnvironmentVariable("FFXIV_SQPACK_PATH") ?? @"C:\Games\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack";
        if (!System.IO.Directory.Exists(sqpackPath)) return;

        var lumina = new Lumina.GameData(sqpackPath);
        var cardResidentSheet = lumina.GetExcelSheet<Lumina.Excel.Sheets.TripleTriadCardResident>();
        var cardSheet = lumina.GetExcelSheet<Lumina.Excel.Sheets.TripleTriadCard>();
        if (cardResidentSheet == null || cardSheet == null) return;

        var cardDb = TriadCardDB.Get();
        cardDb.cards.Clear();

        for (uint idx = 0; idx < cardResidentSheet.Count; idx++)
        {
            var rowData = cardResidentSheet.GetRowOrDefault(idx);
            var rowName = cardSheet.GetRowOrDefault(idx);

            if (rowData.HasValue && rowName.HasValue && rowData.Value.Top > 0)
            {
                var cardOb = new TriadCard(
                    (int)idx,
                    ETriadCardRarity.Common,
                    ETriadCardType.None,
                    rowData.Value.Top,
                    rowData.Value.Bottom,
                    rowData.Value.Left,
                    rowData.Value.Right,
                    rowData.Value.Order,
                    rowData.Value.UIPriority,
                    rowName.Value.Name.ToString());

                while (cardDb.cards.Count < idx)
                {
                    cardDb.cards.Add(null);
                }
                cardDb.cards.Add(cardOb);
            }
        }

        var mirroredPairs = new List<(TriadCard cardA, TriadCard cardB)>();
        for (var i = 0; i < cardDb.cards.Count; i++)
        {
            var cardA = cardDb.cards[i];
            if (cardA == null) continue;

            var u = cardA.Sides[(int)ETriadGameSide.Up];
            var l = cardA.Sides[(int)ETriadGameSide.Left];
            var d = cardA.Sides[(int)ETriadGameSide.Down];
            var r = cardA.Sides[(int)ETriadGameSide.Right];

            // If left == right, it's symmetric, not a mirrored pair
            if (l == r) continue;

            // Search for mirror card where Left and Right are swapped
            var mirror = cardDb.cards.FirstOrDefault(c =>
                c != null && c.Id > cardA.Id &&
                c.Sides[(int)ETriadGameSide.Up] == u &&
                c.Sides[(int)ETriadGameSide.Down] == d &&
                c.Sides[(int)ETriadGameSide.Left] == r &&
                c.Sides[(int)ETriadGameSide.Right] == l);

            if (mirror != null)
            {
                mirroredPairs.Add((cardA, mirror));
            }
        }

        Assert.NotEmpty(mirroredPairs);

        // For every mirrored pair, simulate stat-based resolution
        // Game native memory stores:
        // offset 0x83: Top (numU)
        // offset 0x84: Bottom (numD)
        // offset 0x85: Left (in FFXIVClientStructs named NumSideR)
        // offset 0x86: Right (in FFXIVClientStructs named NumSideL)
        foreach (var (cardA, cardB) in mirroredPairs)
        {
            // Simulate reading cardA:
            var nativeOffset83 = cardA.Sides[(int)ETriadGameSide.Up];
            var nativeOffset84 = cardA.Sides[(int)ETriadGameSide.Down];
            var nativeOffset85 = cardA.Sides[(int)ETriadGameSide.Left];
            var nativeOffset86 = cardA.Sides[(int)ETriadGameSide.Right];

            // In TriadAddonReader:
            // numU = addonCard.NumSideU (0x83)
            // numD = addonCard.NumSideD (0x84)
            // numL = addonCard.NumSideR (0x85)
            // numR = addonCard.NumSideL (0x86)
            var resolvedU = nativeOffset83;
            var resolvedD = nativeOffset84;
            var resolvedL = nativeOffset85;
            var resolvedR = nativeOffset86;

            var matchA = cardDb.Find(resolvedU, resolvedL, resolvedD, resolvedR);
            Assert.NotNull(matchA);
            Assert.Equal(cardA.Id, matchA.Id);
            Assert.Equal(cardA.Name, matchA.Name);

            // Simulate reading cardB:
            var bOffset83 = cardB.Sides[(int)ETriadGameSide.Up];
            var bOffset84 = cardB.Sides[(int)ETriadGameSide.Down];
            var bOffset85 = cardB.Sides[(int)ETriadGameSide.Left];
            var bOffset86 = cardB.Sides[(int)ETriadGameSide.Right];

            var matchB = cardDb.Find(bOffset83, bOffset85, bOffset84, bOffset86);
            Assert.NotNull(matchB);
            Assert.Equal(cardB.Id, matchB.Id);
            Assert.Equal(cardB.Name, matchB.Name);
        }
    }
}
