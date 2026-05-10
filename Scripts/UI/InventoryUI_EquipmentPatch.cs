// InventoryUI_EquipmentPatch.cs
//
// INSTRUÇÃO:
//   Este arquivo documenta as modificações no InventoryUI.cs para suportar
//   o botão "Equipar" de itens do tipo Equipment.
//
//   O InventoryUI já tem o botão equipGemButton para PowerGems.
//   Vamos reutilizar o mesmo padrão para Equipment.
//
// ════════════════════════════════════════════════════════════════════════
// MODIFICAÇÃO 1 — Adicionar botão equipEquipmentButton no InventoryUI
// ════════════════════════════════════════════════════════════════════════
//
// No InventoryUI.cs, nos campos [Header("Painel de ação...")]:
//
//   [SerializeField] private Button equipGemButton;          // já existe
//   [SerializeField] private Button equipEquipmentButton;    // ADICIONAR
//
// ════════════════════════════════════════════════════════════════════════
// MODIFICAÇÃO 2 — Registrar listener no Start()
// ════════════════════════════════════════════════════════════════════════
//
// No Start() do InventoryUI.cs, após o registro do equipGemButton:
//
//   if (equipEquipmentButton != null)
//       equipEquipmentButton.onClick.AddListener(OnEquipEquipmentClicked); // ADICIONAR
//
// ════════════════════════════════════════════════════════════════════════
// MODIFICAÇÃO 3 — Mostrar/esconder o botão em ShowActionPanel
// ════════════════════════════════════════════════════════════════════════
//
// No ShowActionPanel() do InventoryUI.cs, logo após "bool isGem = ...":
//
//   bool isEquipment = itemData.Type == RPG.Data.ItemType.Equipment;   // ADICIONAR
//
//   // Modifique as linhas existentes:
//   if (useButton != null)
//       useButton.gameObject.SetActive(isConsumable);
//
//   if (equipGemButton != null)
//       equipGemButton.gameObject.SetActive(isGem);
//
//   if (equipEquipmentButton != null)                                   // ADICIONAR
//       equipEquipmentButton.gameObject.SetActive(isEquipment);         // ADICIONAR
//
//   if (discardButton != null)
//       discardButton.gameObject.SetActive(true);
//
// ════════════════════════════════════════════════════════════════════════
// MODIFICAÇÃO 4 — Implementar OnEquipEquipmentClicked
// ════════════════════════════════════════════════════════════════════════
//
// Adicione o método no InventoryUI.cs (após OnEquipGemClicked):
//
//   private void OnEquipEquipmentClicked()
//   {
//       if (_selectedSlot == null || _selectedSlot.IsEmpty) return;
//       if (_selectedSlot.ItemData.Type != RPG.Data.ItemType.Equipment) return;
//
//       EquipmentUI.Instance?.OpenForEquip(_selectedSlot.SlotData);
//       Close();
//   }
//
// ════════════════════════════════════════════════════════════════════════
// RESUMO (checklist)
// ════════════════════════════════════════════════════════════════════════
//
//  [ ] Adicionar campo [SerializeField] private Button equipEquipmentButton
//  [ ] No Start(): RegisterListener para equipEquipmentButton
//  [ ] No ShowActionPanel(): bool isEquipment + .SetActive(isEquipment)
//  [ ] Adicionar método OnEquipEquipmentClicked()
//  [ ] No prefab do InventoryUI: adicionar Button "Equipar" e arrastar para o campo
//
// ════════════════════════════════════════════════════════════════════════

namespace RPG.UI
{
    internal static class InventoryUIEquipmentPatch { }
}
