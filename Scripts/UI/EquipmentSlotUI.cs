using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using RPG.Data;

namespace RPG.UI
{
    /// <summary>
    /// EquipmentSlotUI v1 — Slot visual de um item de equipamento na EquipmentUI.
    ///
    /// Exibe o ícone do item equipado (ou ícone de slot vazio), nome do slot e
    /// responde a hover (tooltip) e clique (ação de desequipar).
    ///
    /// PREFAB SUGERIDO (hierarquia):
    ///   EquipmentSlot_Weapon (RectTransform + EquipmentSlotUI)
    ///     ├── Background     (Image — fundo, muda cor por raridade)
    ///     ├── SlotTypeIcon   (Image — ícone silhueta do tipo de slot, desativado se ocupado)
    ///     ├── ItemIcon       (Image — ícone do item, desativado se vazio)
    ///     ├── SlotLabel      (TMP_Text — nome do slot: "Arma", "Elmo" etc)
    ///     └── SelectionBorder(Image — borda amarela, inativo por padrão)
    ///
    /// SETUP no EquipmentUI Inspector:
    ///   Arraste os 7 GameObjects de slot para os campos weapon/shield/etc.
    /// </summary>
    public class EquipmentSlotUI : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
    {
        [Header("Referências visuais")]
        [SerializeField] private Image    background;
        [SerializeField] private Image    slotTypeIcon;   // ícone silhueta (vazio)
        [SerializeField] private Image    itemIcon;        // ícone do item equipado
        [SerializeField] private TMP_Text slotLabel;
        [SerializeField] private Image    selectionBorder;

        [Header("Sprite de silhueta (slot vazio)")]
        [Tooltip("Ícone mostrando o tipo de slot quando está vazio (ex: silhueta de espada para Weapon).")]
        [SerializeField] private Sprite defaultSlotSprite;

        [Header("Cores")]
        [SerializeField] private Color emptyColor    = new Color(0.10f, 0.10f, 0.13f, 0.90f);
        [SerializeField] private Color occupiedColor = new Color(0.10f, 0.18f, 0.28f, 0.95f);

        // ── Callbacks ──────────────────────────────────────────────────────
        public System.Action<EquipmentSlotUI> OnSlotClicked;
        public System.Action<EquipmentSlotUI> OnSlotHoverEnter;
        public System.Action<EquipmentSlotUI> OnSlotHoverExit;

        // ── Estado ─────────────────────────────────────────────────────────
        private EquipmentSlot _slot;
        private ItemData      _currentItem;       // null = vazio
        private bool          _isEmpty = true;

        public EquipmentSlot Slot        => _slot;
        public ItemData      CurrentItem => _currentItem;
        public bool          IsEmpty     => _isEmpty;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            Clear();
            SetSelected(false);
        }

        // ── API pública ────────────────────────────────────────────────────

        /// <summary>Configura este widget para um slot específico.</summary>
        public void Initialize(EquipmentSlot slot)
        {
            _slot = slot;
            if (slotLabel != null)
                slotLabel.text = slot switch
                {
                    EquipmentSlot.Weapon => "Arma",
                    EquipmentSlot.Shield => "Escudo",
                    EquipmentSlot.Helmet => "Elmo",
                    EquipmentSlot.Chest  => "Peitoral",
                    EquipmentSlot.Legs   => "Pernas",
                    EquipmentSlot.Boots  => "Botas",
                    EquipmentSlot.Gloves => "Luvas",
                    _                    => slot.ToString()
                };
        }

        /// <summary>Preenche o slot com um item equipado.</summary>
        public void SetItem(ItemData item)
        {
            _currentItem = item;
            _isEmpty     = item == null;

            if (_isEmpty)
            {
                Clear();
                return;
            }

            if (background  != null) background.color  = occupiedColor;
            if (slotTypeIcon!= null) slotTypeIcon.gameObject.SetActive(false);

            if (itemIcon != null)
            {
                itemIcon.gameObject.SetActive(true);
                itemIcon.sprite = item.Icon;
                itemIcon.color  = item.Icon != null ? Color.white : new Color(1f, 1f, 1f, 0.5f);
            }
        }

        /// <summary>Esvazia o slot.</summary>
        public void Clear()
        {
            _currentItem = null;
            _isEmpty     = true;

            if (background  != null) background.color = emptyColor;

            if (slotTypeIcon != null)
            {
                slotTypeIcon.gameObject.SetActive(true);
                if (defaultSlotSprite != null) slotTypeIcon.sprite = defaultSlotSprite;
            }

            if (itemIcon != null) itemIcon.gameObject.SetActive(false);

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectionBorder != null)
                selectionBorder.gameObject.SetActive(selected);
        }

        // ── Eventos de ponteiro ────────────────────────────────────────────

        public void OnPointerClick(PointerEventData e) => OnSlotClicked?.Invoke(this);
        public void OnPointerEnter(PointerEventData e)
        {
            if (!_isEmpty) OnSlotHoverEnter?.Invoke(this);
        }
        public void OnPointerExit(PointerEventData e) => OnSlotHoverExit?.Invoke(this);
    }
}
