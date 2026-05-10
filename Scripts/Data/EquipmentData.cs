using UnityEngine;
using RPG.Combat;

namespace RPG.Data
{
    /// <summary>
    /// Slot físico de equipamento do personagem.
    /// Cada valor corresponde a um botão de slot na EquipmentUI.
    /// </summary>
    public enum EquipmentSlot
    {
        Weapon  = 0,  // Arma
        Shield  = 1,  // Escudo / Off-hand
        Helmet  = 2,  // Elmo
        Chest   = 3,  // Peitoral
        Legs    = 4,  // Pernas
        Boots   = 5,  // Botas
        Gloves  = 6,  // Luvas
    }

    /// <summary>
    /// EquipmentData — ScriptableObject de um item de equipamento.
    ///
    /// Funciona em paralelo com ItemData: um item de equipamento tem Type == Equipment
    /// e referencia um EquipmentData que descreve o slot e os bônus.
    ///
    /// Para criar: Assets → Create → RPG → Equipment Data
    /// O campo ItemId no ItemData pai deve ser idêntico ao desta asset.
    ///
    /// INTEGRAÇÃO COM O SISTEMA EXISTENTE:
    ///   Os bônus (BonusSTR, BonusATK etc.) são somados ao EquipmentBonuses
    ///   no servidor antes de recalcular DerivedStats via StatsCalculator.Calculate.
    ///   Isso significa que TODA a lógica de stats existente funciona sem alteração.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Equipment Data", fileName = "Equip_New")]
    public class EquipmentData : ScriptableObject
    {
        [Header("Identificação — deve bater com o ItemId do ItemData correspondente")]
        public string ItemId = "equip_001";

        [Header("Slot")]
        public EquipmentSlot Slot = EquipmentSlot.Weapon;

        [Header("Bônus de Atributos")]
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

        [Header("Resistências Elementais (0–75%)")]
        public float BonusResistFire;
        public float BonusResistIce;
        public float BonusResistPoison;
        public float BonusResistLightning;

        [Header("Requisitos Mínimos (0 = sem requisito)")]
        public int RequiredLevel;
        public int RequiredSTR;
        public int RequiredAGI;
        public int RequiredVIT;
        public int RequiredDEX;
        public int RequiredINT;

        // ── Helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Gera uma string de tooltip com os bônus do equipamento.
        /// Usada pelo ItemTooltipUI para exibir stats de equipamento.
        /// </summary>
        public string GetStatsTooltip()
        {
            var sb = new System.Text.StringBuilder();

            void Add(string label, float val, string color = "#88FF88")
            {
                if (val != 0f) sb.AppendLine($"<color={color}>{(val > 0 ? "+" : "")}{val:0.#} {label}</color>");
            }

            Add("STR", BonusSTR);   Add("AGI", BonusAGI);   Add("VIT", BonusVIT);
            Add("DEX", BonusDEX);   Add("INT", BonusINT);   Add("LUK", BonusLUK);
            Add("ATK", BonusATK);   Add("DEF", BonusDEF);
            Add("MATK", BonusMATK); Add("MDEF", BonusMDEF);
            Add("HP", BonusHP);     Add("MP", BonusMP);
            Add("Resist Fogo",    BonusResistFire,      "#FF8844");
            Add("Resist Gelo",    BonusResistIce,       "#88DDFF");
            Add("Resist Veneno",  BonusResistPoison,    "#88FF44");
            Add("Resist Raio",    BonusResistLightning, "#FFFF44");

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Aplica os bônus deste equipamento sobre um EquipmentBonuses existente.
        /// Chamado pelo servidor ao equipar/desequipar.
        /// </summary>
        public void ApplyTo(EquipmentBonuses target)
        {
            target.STR  += BonusSTR;   target.AGI  += BonusAGI;
            target.VIT  += BonusVIT;   target.DEX  += BonusDEX;
            target.INT  += BonusINT;   target.LUK  += BonusLUK;
            target.ATK  += BonusATK;   target.DEF  += BonusDEF;
            target.MATK += BonusMATK;  target.MDEF += BonusMDEF;
            target.HPBonus += BonusHP; target.MPBonus += BonusMP;
            target.ResistFire      += BonusResistFire;
            target.ResistIce       += BonusResistIce;
            target.ResistPoison    += BonusResistPoison;
            target.ResistLightning += BonusResistLightning;
        }

        /// <summary>
        /// Remove os bônus deste equipamento de um EquipmentBonuses existente.
        /// Chamado pelo servidor ao desequipar.
        /// </summary>
        public void RemoveFrom(EquipmentBonuses target)
        {
            target.STR  -= BonusSTR;   target.AGI  -= BonusAGI;
            target.VIT  -= BonusVIT;   target.DEX  -= BonusDEX;
            target.INT  -= BonusINT;   target.LUK  -= BonusLUK;
            target.ATK  -= BonusATK;   target.DEF  -= BonusDEF;
            target.MATK -= BonusMATK;  target.MDEF -= BonusMDEF;
            target.HPBonus -= BonusHP; target.MPBonus -= BonusMP;
            target.ResistFire      -= BonusResistFire;
            target.ResistIce       -= BonusResistIce;
            target.ResistPoison    -= BonusResistPoison;
            target.ResistLightning -= BonusResistLightning;
        }

        /// <summary>
        /// Verifica se o personagem atende os requisitos para equipar este item.
        /// </summary>
        public bool MeetsRequirements(CharacterData data)
        {
            if (data == null) return false;
            if (data.Level < RequiredLevel) return false;

            var stats = data.GetDerivedStats();
            if (stats.ATK  < RequiredSTR && RequiredSTR > 0) return false;
            if (stats.ASPD < RequiredAGI && RequiredAGI > 0) return false;

            // Verifica atributos base + alocados + raça
            var rb = StatsCalculator.GetRaceBonus(data.Race);
            int str = data.BaseAttributes.STR + rb.STR + data.AllocatedSTR;
            int agi = data.BaseAttributes.AGI + rb.AGI + data.AllocatedAGI;
            int vit = data.BaseAttributes.VIT + rb.VIT + data.AllocatedVIT;
            int dex = data.BaseAttributes.DEX + rb.DEX + data.AllocatedDEX;
            int iNt = data.BaseAttributes.INT + rb.INT + data.AllocatedINT;

            if (str < RequiredSTR && RequiredSTR > 0) return false;
            if (agi < RequiredAGI && RequiredAGI > 0) return false;
            if (vit < RequiredVIT && RequiredVIT > 0) return false;
            if (dex < RequiredDEX && RequiredDEX > 0) return false;
            if (iNt < RequiredINT && RequiredINT > 0) return false;

            return true;
        }

        public string SlotDisplayName => Slot switch
        {
            EquipmentSlot.Weapon => "Arma",
            EquipmentSlot.Shield => "Escudo",
            EquipmentSlot.Helmet => "Elmo",
            EquipmentSlot.Chest  => "Peitoral",
            EquipmentSlot.Legs   => "Pernas",
            EquipmentSlot.Boots  => "Botas",
            EquipmentSlot.Gloves => "Luvas",
            _                    => Slot.ToString()
        };
    }
}
