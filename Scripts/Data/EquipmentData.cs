using UnityEngine;
using RPG.Combat;

namespace RPG.Data
{
    /// <summary>
    /// EquipmentData v3
    ///
    /// CORREÇÃO v3 — MeetsRequirements tinha lógica incorreta:
    ///
    ///   BUG: O código comparava stats DERIVADOS (ATK, ASPD) com os requisitos
    ///   de ATRIBUTOS BASE (RequiredSTR, RequiredAGI). Isso é semanticamente errado:
    ///     - "stats.ATK < RequiredSTR" comparava float de dano com int de atributo.
    ///     - "stats.ASPD < RequiredAGI" comparava ataques/segundo com pontos de AGI.
    ///   Resultado: um personagem com STR=5 e RequiredSTR=10 poderia equipar o item
    ///   se seu ATK calculado fosse >= 10 (via bônus de equipamento), burlando o req.
    ///
    ///   SOLUÇÃO: MeetsRequirements agora compara ATRIBUTOS TOTAIS (base + raça + alocado)
    ///   com os requisitos de atributo. Equipamento atual NÃO é contado nos requisitos
    ///   (o item sendo equipado não pode satisfazer seu próprio requisito).
    ///
    ///   MELHORIA: RequiredINT agora também é verificado (estava faltando na v2).
    ///
    ///   Todas as funcionalidades de v2 mantidas.
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

        [Header("Requisitos Mínimos de ATRIBUTO (0 = sem requisito)")]
        [Tooltip("Compara com (BaseAttr + RaceBonus + AllocatedAttr), SEM equipamentos.")]
        public int RequiredLevel;
        public int RequiredSTR;
        public int RequiredAGI;
        public int RequiredVIT;
        public int RequiredDEX;
        public int RequiredINT;
        public int RequiredLUK;

        // ── Helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Tooltip com os bônus do equipamento para exibição na UI.
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
        /// Chamado pelo servidor ao equipar.
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
        ///
        /// CORREÇÃO v3: compara ATRIBUTOS TOTAIS (base + raça + alocado) com os
        /// requisitos de atributo. NÃO inclui bônus de equipamentos atuais, pois
        /// o item sendo equipado não pode satisfazer seu próprio requisito.
        ///
        /// Exemplo correto: RequiredSTR=50 é atendido se BaseSTR+RaceSTR+AllocSTR >= 50.
        /// Exemplo errado (v2): comparava stats.ATK (dano derivado) com RequiredSTR (atributo).
        /// </summary>
        public bool MeetsRequirements(CharacterData data)
        {
            if (data == null) return false;

            // Verificação de nível
            if (RequiredLevel > 0 && data.Level < RequiredLevel) return false;

            // Calcula atributos totais SEM equipamentos (base + raça + alocado)
            var raceBonus = StatsCalculator.GetRaceBonus(data.Race);

            int totalSTR = data.BaseAttributes.STR + raceBonus.STR + data.AllocatedSTR;
            int totalAGI = data.BaseAttributes.AGI + raceBonus.AGI + data.AllocatedAGI;
            int totalVIT = data.BaseAttributes.VIT + raceBonus.VIT + data.AllocatedVIT;
            int totalDEX = data.BaseAttributes.DEX + raceBonus.DEX + data.AllocatedDEX;
            int totalINT = data.BaseAttributes.INT + raceBonus.INT + data.AllocatedINT;
            int totalLUK = data.BaseAttributes.LUK + raceBonus.LUK + data.AllocatedLUK;

            // CORREÇÃO v3: comparação correta — atributo vs requisito de atributo
            if (RequiredSTR > 0 && totalSTR < RequiredSTR) return false;
            if (RequiredAGI > 0 && totalAGI < RequiredAGI) return false;
            if (RequiredVIT > 0 && totalVIT < RequiredVIT) return false;
            if (RequiredDEX > 0 && totalDEX < RequiredDEX) return false;
            if (RequiredINT > 0 && totalINT < RequiredINT) return false; // CORRIGIDO: estava faltando na v2
            if (RequiredLUK > 0 && totalLUK < RequiredLUK) return false;

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
