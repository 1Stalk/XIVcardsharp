using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using XIVcardsharp.Core.Data;

namespace XIVcardsharp.Interop;

public static class LuminaCardLoader
{
    private static readonly ETriadCardType[] CardTypeMap =
    [
        ETriadCardType.None,
        ETriadCardType.Primal,
        ETriadCardType.Scion,
        ETriadCardType.Beastman,
        ETriadCardType.Garlean
    ];

    private static readonly ETriadCardRarity[] CardRarityMap =
    [
        ETriadCardRarity.Common,
        ETriadCardRarity.Common,
        ETriadCardRarity.Uncommon,
        ETriadCardRarity.Rare,
        ETriadCardRarity.Epic,
        ETriadCardRarity.Legendary
    ];

    public static bool LoadCards(IDataManager dataManager)
    {
        try
        {
            var cardDB = TriadCardDB.Get();
            if (cardDB.cards.Count > 50)
            {
                return true;
            }

            var cardDataSheet = dataManager.GetExcelSheet<TripleTriadCardResident>();
            var cardNameSheet = dataManager.GetExcelSheet<TripleTriadCard>();

            if (cardDataSheet == null || cardNameSheet == null)
            {
                return false;
            }

            for (uint idx = 0; idx < cardDataSheet.Count; idx++)
            {
                var rowData = cardDataSheet.GetRowOrDefault(idx);
                var rowName = cardNameSheet.GetRowOrDefault(idx);

                if (rowData != null && rowName != null && rowData.Value.Top > 0)
                {
                    var rowTypeId = rowData.Value.TripleTriadCardType.RowId;
                    var rowRarityId = rowData.Value.TripleTriadCardRarity.RowId;
                    var cardType = (rowTypeId < CardTypeMap.Length) ? CardTypeMap[rowTypeId] : ETriadCardType.None;
                    var cardRarity = (rowRarityId < CardRarityMap.Length) ? CardRarityMap[rowRarityId] : ETriadCardRarity.Common;

                    var cardOb = new TriadCard(
                        (int)idx,
                        cardRarity,
                        cardType,
                        rowData.Value.Top,
                        rowData.Value.Bottom,
                        rowData.Value.Left,
                        rowData.Value.Right,
                        rowData.Value.Order,
                        rowData.Value.UIPriority,
                        rowName.Value.Name.ToString());

                    while (cardDB.cards.Count < idx)
                    {
                        cardDB.cards.Add(null);
                    }
                    cardDB.cards.Add(cardOb);
                }
            }

            cardDB.ProcessSameSideLists();
            ECommons.DalamudServices.Svc.Log.Info($"[LuminaCardLoader] Loaded {cardDB.cards.Count} cards into TriadCardDB");
            return true;
        }
        catch (Exception ex)
        {
            ECommons.DalamudServices.Svc.Log.Error(ex, "[LuminaCardLoader] Failed to load cards from Lumina");
            return false;
        }
    }
}
