using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RPG.Data;

namespace RPG.UI
{
    /// <summary>
    /// ItemTooltipUI v2 — Equipment patch integrado.
    ///
    /// CORREÇÕES v2:
    ///   - Seção de Equipment adicionada diretamente (sem patch externo).
    ///     Exibe slot, bônus e requisitos do equipamento ao passar o mouse.
    ///   - Seção de PowerGem mantida intacta.
    ///   - Seção de Consumible mantida intacta.
    ///   - ItemTooltipUI_EquipmentPatch.cs pode ser deletado — não é mais necessário.
    /// </summary>
    public class ItemTooltipUI : MonoBehaviour
    {
        public static ItemTooltipUI Instance { get; private set; }

        [Header("Painel raiz")]
        [SerializeField] private RectTransform tooltipRect;
        [SerializeField] private CanvasGroup   canvasGroup;

        [Header("Textos comuns")]
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text itemRarityText;
        [SerializeField] private TMP_Text itemTypeText;
        [SerializeField] private TMP_Text descriptionText;

        [Header("Seção PowerGem")]
        [SerializeField] private GameObject gemSection;
        [SerializeField] private TMP_Text   gemSkillNameText;
        [SerializeField] private TMP_Text   gemSkillStatsText;

        [Header("Seção Consumível")]
        [SerializeField] private GameObject consumableSection;
        [SerializeField] private TMP_Text   consumableStatsText;

        [Header("Seção Equipment — crie um filho 'EquipmentSection' no prefab do tooltip")]
        [SerializeField] private GameObject equipmentSection;
        [SerializeField] private TMP_Text   equipSlotNameText;
        [SerializeField] private TMP_Text   equipStatsText;

        [Header("Comportamento")]
        [SerializeField] private Vector2 offset      = new Vector2(15f, -10f);
        [SerializeField] private float   edgePadding = 10f;

        private Canvas _canvas;
        private bool   _visible = false;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _canvas = GetComponentInParent<Canvas>();
            Hide();
        }

        private void Update()
        {
            if (_visible) FollowCursor();
        }

        // ── API pública ────────────────────────────────────────────────────

        public void Show(ItemData item)
        {
            if (item == null) { Hide(); return; }

            PopulateContent(item);

            if (canvasGroup != null)
            {
                canvasGroup.alpha          = 1f;
                canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(true);
            _visible = true;
            FollowCursor();
        }

        public void Hide()
        {
            _visible = false;
            gameObject.SetActive(false);
        }

        // ── Conteúdo ───────────────────────────────────────────────────────

        private void PopulateContent(ItemData item)
        {
            // Nome
            if (itemNameText != null)
            {
                itemNameText.text  = item.DisplayName;
                itemNameText.color = item.RarityColor;
            }

            // Raridade
            if (itemRarityText != null)
            {
                itemRarityText.text  = item.RarityDisplayName;
                itemRarityText.color = item.RarityColor;
            }

            // Tipo
            if (itemTypeText != null)
            {
                itemTypeText.text = item.Type switch
                {
                    ItemType.PowerGem   => "\u2726 Joia do Poder",
                    ItemType.Equipment  => "\u2694 Equipamento",
                    ItemType.Consumable => "\u2b21 Consumível",
                    ItemType.Misc       => "\u25c8 Miscelânea",
                    _                   => item.Type.ToString()
                };
            }

            // Descrição
            if (descriptionText != null)
                descriptionText.text = item.Description;

            // ── Seção PowerGem ─────────────────────────────────────────────
            bool isGem = item.IsPowerGem && item.EmbeddedSkill != null;
            if (gemSection != null) gemSection.SetActive(isGem);

            if (isGem && item.EmbeddedSkill != null)
            {
                var skill = item.EmbeddedSkill;

                if (gemSkillNameText  != null)
                    gemSkillNameText.text = $"\u26a1 {skill.Name}";

                if (gemSkillStatsText != null)
                {
                    gemSkillStatsText.text =
                        $"Tipo: {SkillTypeDisplay(skill.Type)}\n" +
                        $"Alcance: {skill.Range:0.0}m\n" +
                        $"Custo MP: {skill.ManaCost:0}\n" +
                        $"Cooldown: {skill.Cooldown:0.0}s\n" +
                        $"Mult. ATK: {skill.AtkMultiplier:0.00}x";
                }
            }

            // ── Seção Consumível ───────────────────────────────────────────
            bool isConsumable = item.IsConsumable;
            if (consumableSection != null) consumableSection.SetActive(isConsumable);

            if (isConsumable && consumableStatsText != null)
            {
                string stats = "";
                if (item.HealAmount   > 0f) stats += $"\u2764 Recupera {item.HealAmount:0} HP\n";
                if (item.ManaAmount   > 0f) stats += $"\u2726 Recupera {item.ManaAmount:0} MP\n";
                if (item.BuffDuration > 0f) stats += $"\u23f1 Duração: {item.BuffDuration:0}s\n";
                consumableStatsText.text = stats.TrimEnd();
            }

            // ── Seção Equipment ────────────────────────────────────────────
            bool isEquipment = item.Type == ItemType.Equipment;
            if (equipmentSection != null) equipmentSection.SetActive(isEquipment && !isGem);

            if (isEquipment && !isGem)
            {
                var eqData = EquipmentDatabase.Instance?.GetEquipment(item.ItemId);
                if (eqData != null)
                {
                    if (equipSlotNameText != null)
                        equipSlotNameText.text = $"\u2694 {eqData.SlotDisplayName}";

                    if (equipStatsText != null)
                    {
                        string text = eqData.GetStatsTooltip();

                        // Requisitos
                        bool hasReqs = eqData.RequiredLevel > 0 || eqData.RequiredSTR > 0
                                    || eqData.RequiredAGI   > 0 || eqData.RequiredVIT > 0
                                    || eqData.RequiredDEX   > 0 || eqData.RequiredINT > 0;
                        if (hasReqs)
                        {
                            string reqs = "\n<color=#FF8888>Requisitos:</color>\n";
                            if (eqData.RequiredLevel > 0) reqs += $"  Nível {eqData.RequiredLevel}\n";
                            if (eqData.RequiredSTR   > 0) reqs += $"  STR {eqData.RequiredSTR}\n";
                            if (eqData.RequiredAGI   > 0) reqs += $"  AGI {eqData.RequiredAGI}\n";
                            if (eqData.RequiredVIT   > 0) reqs += $"  VIT {eqData.RequiredVIT}\n";
                            if (eqData.RequiredDEX   > 0) reqs += $"  DEX {eqData.RequiredDEX}\n";
                            if (eqData.RequiredINT   > 0) reqs += $"  INT {eqData.RequiredINT}\n";
                            text += reqs.TrimEnd();
                        }

                        equipStatsText.text = text;
                    }
                }
                else if (equipStatsText != null)
                {
                    // Equipamento sem EquipmentData — fallback
                    equipStatsText.text = "<color=#888>Sem dados de equipamento.</color>";
                }
            }
        }

        // ── Posicionamento ─────────────────────────────────────────────────

        private void FollowCursor()
        {
            if (tooltipRect == null || _canvas == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform,
                Input.mousePosition, _canvas.worldCamera,
                out Vector2 localPoint);

            Vector2 targetPos = localPoint + offset;

            var canvasRect = _canvas.transform as RectTransform;
            if (canvasRect != null)
            {
                float halfW = tooltipRect.rect.width  * 0.5f;
                float halfH = tooltipRect.rect.height * 0.5f;
                float maxX  = canvasRect.rect.width  * 0.5f - halfW - edgePadding;
                float maxY  = canvasRect.rect.height * 0.5f - halfH - edgePadding;

                targetPos.x = Mathf.Clamp(targetPos.x, -maxX, maxX);
                targetPos.y = Mathf.Clamp(targetPos.y, -maxY, maxY);
            }

            tooltipRect.anchoredPosition = targetPos;
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static string SkillTypeDisplay(Combat.SkillType type) => type switch
        {
            Combat.SkillType.Physical => "Físico",
            Combat.SkillType.Magical  => "Mágico",
            Combat.SkillType.Heal     => "Cura",
            Combat.SkillType.Buff     => "Buff",
            _ => type.ToString()
        };
    }
}
