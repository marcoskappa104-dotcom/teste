// ItemTooltipUI_EquipmentPatch.cs
//
// INSTRUÇÃO:
//   Este arquivo documenta a ÚNICA modificação necessária no ItemTooltipUI.cs
//   para exibir stats de equipamento no tooltip.
//
//   Localize o método PopulateContent(ItemData item) no ItemTooltipUI.cs
//   e substitua o trecho de Equipment pelo código abaixo:
//
// ════════════════════════════════════════════════════════════════════════
// SUBSTITUIR no PopulateContent(), onde está o case ItemType.Equipment:
// ════════════════════════════════════════════════════════════════════════
//
// ANTES (no itemTypeText switch):
//   ItemType.Equipment  => "⚔ Equipamento",
//
// DEPOIS (mantém igual — só o conteúdo abaixo muda):
//   ItemType.Equipment  => "⚔ Equipamento",
//
// ADICIONAR após o bloco "Seção Consumível" (antes do fechamento de PopulateContent):
//
//   // Seção Equipment
//   bool isEquipment = item.Type == ItemType.Equipment;
//   // Reutilize a gemSection ou crie uma equipmentSection separada:
//   // Se usar a gemSection para equipment também:
//   if (isEquipment && !isGem)
//   {
//       if (gemSection != null) gemSection.SetActive(true);
//       var eqData = RPG.Data.EquipmentDatabase.Instance?.GetEquipment(item.ItemId);
//       if (eqData != null)
//       {
//           if (gemSkillNameText  != null)
//               gemSkillNameText.text = $"⚔ {eqData.SlotDisplayName}";
//           if (gemSkillStatsText != null)
//               gemSkillStatsText.text = eqData.GetStatsTooltip();
//
//           // Requisitos
//           if (eqData.RequiredLevel > 0 || eqData.RequiredSTR > 0)
//           {
//               string reqs = "<color=#FF8888>Requisitos:</color>\n";
//               if (eqData.RequiredLevel > 0) reqs += $"  Nível {eqData.RequiredLevel}\n";
//               if (eqData.RequiredSTR   > 0) reqs += $"  STR {eqData.RequiredSTR}\n";
//               if (eqData.RequiredAGI   > 0) reqs += $"  AGI {eqData.RequiredAGI}\n";
//               if (eqData.RequiredVIT   > 0) reqs += $"  VIT {eqData.RequiredVIT}\n";
//               if (eqData.RequiredDEX   > 0) reqs += $"  DEX {eqData.RequiredDEX}\n";
//               if (eqData.RequiredINT   > 0) reqs += $"  INT {eqData.RequiredINT}\n";
//               if (gemSkillStatsText != null)
//                   gemSkillStatsText.text += "\n" + reqs.TrimEnd();
//           }
//       }
//   }
//
// ════════════════════════════════════════════════════════════════════════
// ALTERNATIVA RECOMENDADA: adicionar campos separados para Equipment
// ════════════════════════════════════════════════════════════════════════
//
// Se preferir campos separados no prefab do tooltip (mais organizado):
//
// 1. Adicione no Inspector do ItemTooltipUI:
//    [Header("Seção Equipment")]
//    [SerializeField] private GameObject equipmentSection;
//    [SerializeField] private TMP_Text   equipSlotNameText;
//    [SerializeField] private TMP_Text   equipStatsText;
//
// 2. No PopulateContent():
//    bool isEquipment = item.Type == ItemType.Equipment;
//    if (equipmentSection != null) equipmentSection.SetActive(isEquipment);
//    if (isEquipment)
//    {
//        var eqData = RPG.Data.EquipmentDatabase.Instance?.GetEquipment(item.ItemId);
//        if (eqData != null)
//        {
//            if (equipSlotNameText != null) equipSlotNameText.text = $"⚔ {eqData.SlotDisplayName}";
//            if (equipStatsText    != null) equipStatsText.text    = eqData.GetStatsTooltip();
//        }
//    }
//
// ════════════════════════════════════════════════════════════════════════

namespace RPG.UI
{
    internal static class ItemTooltipUIEquipmentPatch { }
}
