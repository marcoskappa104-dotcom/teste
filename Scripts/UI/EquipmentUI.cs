using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using RPG.Data;
using RPG.Network;
using System.Collections;
using System.Collections.Generic;

namespace RPG.UI
{
    /// <summary>
    /// EquipmentUI v1 — Painel de equipamentos do personagem.
    ///
    /// ════════════════════════════════════════════════════════════════════
    /// FUNCIONALIDADES
    /// ════════════════════════════════════════════════════════════════════
    ///   • Exibe os 7 slots de equipamento (Arma, Escudo, Elmo, Peitoral,
    ///     Pernas, Botas, Luvas) com ícone do item equipado ou silhueta vazia.
    ///   • Clique em slot ocupado → botão "Desequipar" aparece.
    ///   • Integração com InventoryUI: ao clicar "Equipar" em um item de
    ///     equipamento no inventário, a EquipmentUI destaca os slots compatíveis.
    ///   • Tooltip via ItemTooltipUI ao passar o mouse.
    ///   • Painel de stats resumidos (ATK, DEF, HP, MP) para comparação rápida.
    ///
    /// ════════════════════════════════════════════════════════════════════
    /// SETUP NA CENA (hierarquia sugerida — dentro do mesmo Canvas do Inventário)
    /// ════════════════════════════════════════════════════════════════════
    ///
    ///   EquipmentPanel (Panel + EquipmentUI) [começa INATIVO]
    ///     ├── Header
    ///     │   ├── TitleText ("Equipamentos")
    ///     │   └── CloseButton
    ///     ├── CharacterSilhouette (Image — boneco do personagem, opcional)
    ///     ├── SlotsGroup
    ///     │   ├── SlotHelmet   (EquipmentSlotUI) — posicionado na cabeça
    ///     │   ├── SlotChest    (EquipmentSlotUI) — torso
    ///     │   ├── SlotLegs     (EquipmentSlotUI) — pernas
    ///     │   ├── SlotBoots    (EquipmentSlotUI) — pés
    ///     │   ├── SlotGloves   (EquipmentSlotUI) — mãos
    ///     │   ├── SlotWeapon   (EquipmentSlotUI) — lado esquerdo
    ///     │   └── SlotShield   (EquipmentSlotUI) — lado direito
    ///     ├── ActionPanel (desativado por padrão)
    ///     │   ├── ActionItemName (TMP_Text)
    ///     │   ├── ActionItemDesc (TMP_Text)
    ///     │   ├── ActionItemIcon (Image)
    ///     │   ├── StatsText      (TMP_Text — bônus do item)
    ///     │   └── UnequipButton  (Button)
    ///     └── SummaryPanel
    ///         ├── SummaryATK  (TMP_Text)
    ///         ├── SummaryDEF  (TMP_Text)
    ///         ├── SummaryHP   (TMP_Text)
    ///         └── SummaryMP   (TMP_Text)
    ///
    /// INTEGRAÇÃO COM InventoryUI:
    ///   Quando o jogador clica "Equipar" em um item de Equipment no InventoryUI,
    ///   chame EquipmentUI.Instance.OpenForEquip(slotData) — igual ao PowerGemUI.
    ///   A EquipmentUI destaca automaticamente o slot correto e aguarda confirmação.
    ///
    /// O GameObject raiz DEVE estar ATIVO (apenas o panel filho inicia inativo).
    /// </summary>
    public class EquipmentUI : MonoBehaviour
    {
        public static EquipmentUI Instance { get; private set; }

        [Header("Painel raiz")]
        [SerializeField] private GameObject equipmentPanel;
        [SerializeField] private Button     closeButton;
        [SerializeField] private TMP_Text   titleText;
        [SerializeField] private TMP_Text   instructionText;

        [Header("Slots — arraste os 7 EquipmentSlotUI aqui")]
        [SerializeField] private EquipmentSlotUI slotWeapon;
        [SerializeField] private EquipmentSlotUI slotShield;
        [SerializeField] private EquipmentSlotUI slotHelmet;
        [SerializeField] private EquipmentSlotUI slotChest;
        [SerializeField] private EquipmentSlotUI slotLegs;
        [SerializeField] private EquipmentSlotUI slotBoots;
        [SerializeField] private EquipmentSlotUI slotGloves;

        [Header("Painel de ação (ativo ao selecionar slot ocupado)")]
        [SerializeField] private GameObject actionPanel;
        [SerializeField] private TMP_Text   actionItemNameText;
        [SerializeField] private TMP_Text   actionItemDescText;
        [SerializeField] private Image      actionItemIcon;
        [SerializeField] private TMP_Text   actionStatsText;
        [SerializeField] private Button     unequipButton;

        [Header("Painel de resumo de stats")]
        [SerializeField] private TMP_Text summaryATK;
        [SerializeField] private TMP_Text summaryDEF;
        [SerializeField] private TMP_Text summaryHP;
        [SerializeField] private TMP_Text summaryMP;

        // ── Estado ─────────────────────────────────────────────────────────
        private NetworkEquipment  _equipment;
        private bool              _isOpen        = false;

        // Modo equip (vindo do InventoryUI)
        private bool              _equipMode     = false;
        private InventorySlotData _pendingItem;
        private EquipmentSlot     _pendingSlot;

        private EquipmentSlotUI   _selectedSlot;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (equipmentPanel != null) equipmentPanel.SetActive(false);
            if (actionPanel    != null) actionPanel.SetActive(false);

            if (closeButton  != null) closeButton.onClick.AddListener(Close);
            if (unequipButton!= null) unequipButton.onClick.AddListener(OnUnequipClicked);

            // Inicializa cada slot com seu tipo
            InitSlot(slotWeapon, EquipmentSlot.Weapon);
            InitSlot(slotShield, EquipmentSlot.Shield);
            InitSlot(slotHelmet, EquipmentSlot.Helmet);
            InitSlot(slotChest,  EquipmentSlot.Chest);
            InitSlot(slotLegs,   EquipmentSlot.Legs);
            InitSlot(slotBoots,  EquipmentSlot.Boots);
            InitSlot(slotGloves, EquipmentSlot.Gloves);

            TryBindEquipment();
        }

        private void InitSlot(EquipmentSlotUI widget, EquipmentSlot slot)
        {
            if (widget == null) return;
            widget.Initialize(slot);
            widget.OnSlotClicked    = OnSlotClicked;
            widget.OnSlotHoverEnter = OnSlotHoverEnter;
            widget.OnSlotHoverExit  = _ => ItemTooltipUI.Instance?.Hide();
        }

        // ── Vínculo com NetworkEquipment ───────────────────────────────────

        public void BindEquipment(NetworkEquipment equipment)
        {
            if (equipment == null || _equipment == equipment) return;

            if (_equipment != null)
                _equipment.OnEquipmentChanged -= OnEquipmentChanged;

            _equipment = equipment;
            _equipment.OnEquipmentChanged += OnEquipmentChanged;

            if (_isOpen) RefreshAll();
            Debug.Log("[EquipmentUI] Vinculado ao NetworkEquipment.");
        }

        private bool TryBindEquipment()
        {
            if (_equipment != null) return true;
            if (NetworkClient.localPlayer == null) return false;
            var eq = NetworkClient.localPlayer.GetComponent<NetworkEquipment>();
            if (eq == null) return false;
            BindEquipment(eq);
            return true;
        }

        private void OnEquipmentChanged()
        {
            if (_isOpen) RefreshAll();
        }

        // ── Abrir / Fechar ─────────────────────────────────────────────────

        public void Toggle() { if (_isOpen) Close(); else Open(); }

        public void Open()
        {
            TryBindEquipment();
            _equipMode  = false;
            _isOpen     = true;
            if (equipmentPanel  != null) equipmentPanel.SetActive(true);
            if (actionPanel     != null) actionPanel.SetActive(false);
            if (instructionText != null) instructionText.text = "Clique em um slot para desequipar.";
            if (titleText       != null) titleText.text = "Equipamentos";

            DeselectAll();

            if (!TryBindEquipment())
                StartCoroutine(WaitAndBind());
            else
                RefreshAll();
        }

        /// <summary>
        /// Abre em modo "equipar" vindo do InventoryUI.
        /// Recebe o slot de inventário com o item a equipar.
        /// </summary>
        public void OpenForEquip(InventorySlotData inventorySlot)
        {
            var itemData = ItemDatabase.Instance?.GetItem(inventorySlot.ItemId);
            if (itemData == null || itemData.Type != ItemType.Equipment) return;

            var eqData = EquipmentDatabase.Instance?.GetEquipment(inventorySlot.ItemId);
            if (eqData == null) return;

            _equipMode    = true;
            _pendingItem  = inventorySlot;
            _pendingSlot  = eqData.Slot;

            _isOpen = true;
            if (equipmentPanel != null) equipmentPanel.SetActive(true);
            if (actionPanel    != null) actionPanel.SetActive(false);
            if (titleText      != null) titleText.text = "Equipar Item";
            if (instructionText!= null) instructionText.text =
                $"Equipar <color=#FFD700>{itemData.DisplayName}</color>\nno slot: {eqData.SlotDisplayName}";

            DeselectAll();

            if (!TryBindEquipment())
                StartCoroutine(WaitAndBind());
            else
            {
                RefreshAll();
                HighlightCompatibleSlot(eqData.Slot);
            }
        }

        public void Close()
        {
            _isOpen    = false;
            _equipMode = false;
            DeselectAll();
            ClearHighlights();
            if (equipmentPanel != null) equipmentPanel.SetActive(false);
            if (actionPanel    != null) actionPanel.SetActive(false);
            ItemTooltipUI.Instance?.Hide();
        }

        // ── Refresh ────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            if (_equipment == null) return;

            RefreshSlot(slotWeapon, EquipmentSlot.Weapon);
            RefreshSlot(slotShield, EquipmentSlot.Shield);
            RefreshSlot(slotHelmet, EquipmentSlot.Helmet);
            RefreshSlot(slotChest,  EquipmentSlot.Chest);
            RefreshSlot(slotLegs,   EquipmentSlot.Legs);
            RefreshSlot(slotBoots,  EquipmentSlot.Boots);
            RefreshSlot(slotGloves, EquipmentSlot.Gloves);

            RefreshSummary();
        }

        private void RefreshSlot(EquipmentSlotUI widget, EquipmentSlot slot)
        {
            if (widget == null || _equipment == null) return;
            string itemId = _equipment.GetSlot(slot);
            var item = string.IsNullOrEmpty(itemId) ? null : ItemDatabase.Instance?.GetItem(itemId);
            if (item != null) widget.SetItem(item);
            else              widget.Clear();
        }

        private void RefreshSummary()
        {
            if (_equipment == null) return;

            // Soma todos os bônus dos itens equipados para exibição rápida
            float totalATK = 0f, totalDEF = 0f, totalHP = 0f, totalMP = 0f;
            var db = EquipmentDatabase.Instance;

            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                string id = _equipment.GetSlot(slot);
                if (string.IsNullOrEmpty(id)) continue;
                var eq = db?.GetEquipment(id);
                if (eq == null) continue;
                totalATK += eq.BonusATK + eq.BonusSTR * 1.2f;
                totalDEF += eq.BonusDEF + eq.BonusVIT;
                totalHP  += eq.BonusHP  + eq.BonusVIT * 15f;
                totalMP  += eq.BonusMP  + eq.BonusINT * 12f;
            }

            if (summaryATK != null) summaryATK.text = $"ATK +{totalATK:0}";
            if (summaryDEF != null) summaryDEF.text = $"DEF +{totalDEF:0}";
            if (summaryHP  != null) summaryHP.text  = $"HP  +{totalHP:0}";
            if (summaryMP  != null) summaryMP.text  = $"MP  +{totalMP:0}";
        }

        // ── Eventos de slot ────────────────────────────────────────────────

        private void OnSlotClicked(EquipmentSlotUI widget)
        {
            if (_equipment == null || widget == null) return;

            if (_equipMode)
            {
                // Confirma equipar apenas se o slot clicado é o slot certo
                if (widget.Slot == _pendingSlot)
                {
                    _equipment.CmdEquipItem(_pendingItem.SlotIndex);
                    ClearHighlights();
                    Close();
                    UIManager.Instance?.ShowMessage($"Item equipado no slot {widget.Slot}!");
                }
                else
                {
                    UIManager.Instance?.ShowMessage($"Este item só pode ser equipado no slot {_pendingSlot}!");
                }
                return;
            }

            // Modo browse: seleciona/deseleciona
            if (!widget.IsEmpty)
            {
                DeselectAll();
                _selectedSlot = widget;
                widget.SetSelected(true);
                ShowActionPanel(widget);
            }
            else
            {
                DeselectAll();
                if (actionPanel != null) actionPanel.SetActive(false);
            }
        }

        private void OnSlotHoverEnter(EquipmentSlotUI widget)
        {
            if (widget == null || widget.IsEmpty || widget.CurrentItem == null) return;
            ItemTooltipUI.Instance?.Show(widget.CurrentItem);
        }

        // ── Painel de ação ─────────────────────────────────────────────────

        private void ShowActionPanel(EquipmentSlotUI widget)
        {
            if (actionPanel == null || widget.CurrentItem == null) return;

            var item  = widget.CurrentItem;
            var eqData = EquipmentDatabase.Instance?.GetEquipment(item.ItemId);

            actionPanel.SetActive(true);

            if (actionItemNameText != null)
            {
                actionItemNameText.text  = item.DisplayName;
                actionItemNameText.color = item.RarityColor;
            }
            if (actionItemDescText != null)
                actionItemDescText.text = item.Description;
            if (actionItemIcon != null)
            {
                actionItemIcon.sprite  = item.Icon;
                actionItemIcon.enabled = item.Icon != null;
            }
            if (actionStatsText != null)
                actionStatsText.text = eqData?.GetStatsTooltip() ?? "";
        }

        private void OnUnequipClicked()
        {
            if (_selectedSlot == null || _equipment == null) return;
            _equipment.CmdUnequipItem((int)_selectedSlot.Slot);
            DeselectAll();
            if (actionPanel != null) actionPanel.SetActive(false);
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private void DeselectAll()
        {
            if (_selectedSlot != null)
            {
                _selectedSlot.SetSelected(false);
                _selectedSlot = null;
            }
        }

        private void HighlightCompatibleSlot(EquipmentSlot slot)
        {
            // No modo equip, destaca apenas o slot compatível
            var widget = GetWidget(slot);
            widget?.SetSelected(true);
        }

        private void ClearHighlights()
        {
            slotWeapon?.SetSelected(false);
            slotShield?.SetSelected(false);
            slotHelmet?.SetSelected(false);
            slotChest?.SetSelected(false);
            slotLegs?.SetSelected(false);
            slotBoots?.SetSelected(false);
            slotGloves?.SetSelected(false);
        }

        private EquipmentSlotUI GetWidget(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Weapon => slotWeapon,
            EquipmentSlot.Shield => slotShield,
            EquipmentSlot.Helmet => slotHelmet,
            EquipmentSlot.Chest  => slotChest,
            EquipmentSlot.Legs   => slotLegs,
            EquipmentSlot.Boots  => slotBoots,
            EquipmentSlot.Gloves => slotGloves,
            _                    => null
        };

        private IEnumerator WaitAndBind()
        {
            float elapsed = 0f;
            while (_equipment == null && elapsed < 10f)
            {
                yield return new WaitForSeconds(0.2f);
                elapsed += 0.2f;
                TryBindEquipment();
            }
            if (_equipment != null && _isOpen) RefreshAll();
        }

        private void OnDestroy()
        {
            if (_equipment != null)
                _equipment.OnEquipmentChanged -= OnEquipmentChanged;
        }
    }
}
