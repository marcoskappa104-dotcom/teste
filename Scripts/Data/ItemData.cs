using UnityEngine;
using RPG.Combat;

namespace RPG.Data
{
    public enum ItemType
    {
        PowerGem,       // Joia do Poder — concede uma skill
        Equipment,      // Armadura, arma, escudo, anéis (sistema novo)
        Consumable,     // Poção, comida, etc
        Misc            // Materiais, quest items, etc
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// ItemData v2 — ScriptableObject que define um item do jogo.
    ///
    /// MUDANÇAS v2 (sistema de equipamento):
    ///   - Type=Equipment agora é totalmente funcional.
    ///   - Adicionado EquipSlot (Weapon/Shield/Helmet/Armor/Gloves/Boots/Ring1/Ring2/etc).
    ///   - Adicionados bônus completos: STR/AGI/VIT/DEX/INT/LUK + ATK/DEF/MATK/MDEF
    ///     + HP/MP + ResistFire/Ice/Poison/Lightning.
    ///   - Adicionados Requirements (Lv mínimo, atributos mínimos, raças permitidas).
    ///   - Adicionada Durability (MaxDurability=0 → indestrutível por padrão).
    ///   - Helper IsEquipment.
    ///
    /// COMO CRIAR UM EQUIPAMENTO:
    ///   1. Assets → Create → RPG → Item Data
    ///   2. ItemId único (ex: "weapon_iron_sword")
    ///   3. Type = Equipment
    ///   4. EquipSlot = Weapon (ou outro)
    ///   5. Preencha os bônus desejados
    ///   6. Configure Requirements (opcional)
    ///   7. Adicione ao ItemDatabase.allItems da cena
    ///
    /// IMPORTANTE: NUNCA altere ItemId após criar personagens com este item —
    /// o banco de dados referencia por ID.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Item Data", fileName = "Item_New")]
    public class ItemData : ScriptableObject
    {
        // ── Identificação ──────────────────────────────────────────────────
        [Header("Identificação")]
        [Tooltip("ID único e estável — NUNCA altere após criar personagens com este item.")]
        public string ItemId = "item_001";

        public string DisplayName = "Item";

        [TextArea(2, 4)]
        public string Description = "Descrição do item.";

        public ItemType   Type   = ItemType.Misc;
        public ItemRarity Rarity = ItemRarity.Common;

        // ── Visual ─────────────────────────────────────────────────────────
        [Header("Visual")]
        public Sprite Icon;

        // ── Drop ───────────────────────────────────────────────────────────
        [Header("Drop")]
        [Tooltip("Peso de drop relativo. 100 = comum, 1 = rarissimo.")]
        [Range(0, 100)]
        public int DropWeight = 10;

        // ── PowerGem ───────────────────────────────────────────────────────
        [Header("PowerGem — preencha apenas se Type == PowerGem")]
        [Tooltip("Dados da skill concedida por esta joia.")]
        public SkillData EmbeddedSkill;

        // ── Equipment ──────────────────────────────────────────────────────
        [Header("Equipment — preencha apenas se Type == Equipment")]
        [Tooltip("Slot onde o item se encaixa. Ring1/Ring2 são intercambiáveis no equip.")]
        public EquipmentSlot EquipSlot = EquipmentSlot.None;

        [Header("Bônus de Atributo")]
        public int BonusSTR;
        public int BonusAGI;
        public int BonusVIT;
        public int BonusDEX;
        public int BonusINT;
        public int BonusLUK;

        [Header("Bônus de Combate")]
        public float BonusATK;
        public float BonusDEF;
        public float BonusMATK;
        public float BonusMDEF;
        public float BonusHP;
        public float BonusMP;

        [Header("Resistências Elementais (0–75)")]
        [Range(0f, 75f)] public float BonusResistFire;
        [Range(0f, 75f)] public float BonusResistIce;
        [Range(0f, 75f)] public float BonusResistPoison;
        [Range(0f, 75f)] public float BonusResistLightning;

        [Header("Requisitos para Equipar")]
        [Tooltip("Validados server-side. Cliente exibe no tooltip mas servidor decide.")]
        public EquipmentRequirements Requirements = new EquipmentRequirements();

        [Header("Durabilidade (futuro)")]
        [Tooltip("0 = indestrutível. >0 = degradável a cada hit/recebimento de dano.")]
        public int MaxDurability = 0;

        // ── Consumable ─────────────────────────────────────────────────────
        [Header("Consumable — preencha apenas se Type == Consumable")]
        public float HealAmount  = 0f;
        public float ManaAmount  = 0f;
        public float BuffDuration = 0f;

        // ── Helpers ────────────────────────────────────────────────────────

        public bool IsPowerGem   => Type == ItemType.PowerGem;
        public bool IsEquipment  => Type == ItemType.Equipment && EquipSlot != EquipmentSlot.None;
        public bool IsConsumable => Type == ItemType.Consumable;

        public Color RarityColor => Rarity switch
        {
            ItemRarity.Common    => new Color(0.8f, 0.8f, 0.8f),
            ItemRarity.Uncommon  => new Color(0.3f, 0.8f, 0.3f),
            ItemRarity.Rare      => new Color(0.2f, 0.5f, 1.0f),
            ItemRarity.Epic      => new Color(0.7f, 0.2f, 0.9f),
            ItemRarity.Legendary => new Color(1.0f, 0.6f, 0.1f),
            _                    => Color.white
        };

        public string RarityDisplayName => Rarity switch
        {
            ItemRarity.Common    => "Comum",
            ItemRarity.Uncommon  => "Incomum",
            ItemRarity.Rare      => "Raro",
            ItemRarity.Epic      => "Épico",
            ItemRarity.Legendary => "Lendário",
            _                    => Rarity.ToString()
        };

        /// <summary>
        /// Retorna true se este item NÃO concede nenhum bônus relevante.
        /// Útil para tooltips minimalistas.
        /// </summary>
        public bool HasAnyBonus()
        {
            if (!IsEquipment) return false;
            return BonusSTR != 0 || BonusAGI != 0 || BonusVIT != 0
                || BonusDEX != 0 || BonusINT != 0 || BonusLUK != 0
                || BonusATK > 0f || BonusDEF > 0f || BonusMATK > 0f || BonusMDEF > 0f
                || BonusHP > 0f || BonusMP > 0f
                || BonusResistFire > 0f || BonusResistIce > 0f
                || BonusResistPoison > 0f || BonusResistLightning > 0f;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Validação no Editor: avisa se o ItemData tem inconsistências comuns.
        /// Roda quando o asset é selecionado/modificado no Inspector.
        /// </summary>
        private void OnValidate()
        {
            if (Type == ItemType.Equipment && EquipSlot == EquipmentSlot.None)
                Debug.LogWarning($"[ItemData] '{name}' é Equipment mas EquipSlot=None.");

            if (Type != ItemType.Equipment && EquipSlot != EquipmentSlot.None)
                Debug.LogWarning($"[ItemData] '{name}' tem EquipSlot mas Type não é Equipment.");

            if (Requirements != null && Requirements.MinLevel < 1)
                Requirements.MinLevel = 1;

            if (MaxDurability < 0) MaxDurability = 0;
        }
#endif
    }
}
