using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace RPG.Data
{
    /// <summary>
    /// EquipmentTypes v1 — sistema de equipamento extensível.
    ///
    /// Define todos os tipos relacionados a equipamento: slots, requisitos,
    /// dados sincronizáveis e helpers. Arquivo único para evitar fragmentação.
    ///
    /// EXTENSIBILIDADE:
    ///   O enum EquipmentSlot inclui slots reservados (Cape, Necklace, Earring1, Earring2)
    ///   que ainda não estão visíveis na UI mas já fazem parte do protocolo de rede e
    ///   da persistência. Adicionar um novo slot no futuro requer apenas:
    ///     1. Adicionar à lista EquipmentSlotEx.ActiveSlots.
    ///     2. Criar o widget de UI correspondente em EquipmentPanelUI.
    ///   Saves antigos NÃO são afetados — slots ausentes simplesmente não retornam itens.
    ///
    /// COMPATIBILIDADE:
    ///   - byte (0–255): cap mais que suficiente; cabe em SyncList sem overhead.
    ///   - CharacterRaceFlags: bit-flags permitem combinar várias raças (ex: "Apenas Anões e Orcs").
    /// </summary>
    public enum EquipmentSlot : byte
    {
        None      = 0,

        // ── Slots ativos (visíveis na UI hoje) ─────────────────────────────
        Weapon    = 1,
        Shield    = 2,
        Helmet    = 3,
        Armor     = 4,
        Gloves    = 5,
        Boots     = 6,
        Ring1     = 7,
        Ring2     = 8,

        // ── Slots reservados (futuro — protocolo já compatível) ────────────
        Cape      = 9,
        Necklace  = 10,
        Earring1  = 11,
        Earring2  = 12,
    }

    /// <summary>
    /// Bit-flags para representar conjuntos de raças permitidas em um equipamento.
    /// Permite que UM item liste múltiplas raças sem necessidade de array.
    /// </summary>
    [Flags]
    public enum CharacterRaceFlags : byte
    {
        None   = 0,
        Human  = 1 << 0,
        Elf    = 1 << 1,
        Dwarf  = 1 << 2,
        Orc    = 1 << 3,
        Undead = 1 << 4,
        All    = Human | Elf | Dwarf | Orc | Undead
    }

    /// <summary>
    /// Estrutura sincronizada via Mirror SyncList que representa um item equipado.
    /// Apenas o ItemId é trafegado pela rede — o cliente resolve o ItemData
    /// localmente via ItemDatabase, mantendo o tráfego mínimo.
    ///
    /// Durability/MaxDurability incluídos desde já para suportar sistema futuro
    /// de degradação de equipamento sem quebrar o protocolo.
    /// </summary>
    [Serializable]
    public struct EquippedItemData : IEquatable<EquippedItemData>
    {
        public byte   Slot;          // EquipmentSlot (cast)
        public string ItemId;        // referência ao ItemDatabase
        public int    Durability;    // 0 = quebrado / -1 = indestrutível (ver MaxDurability)
        public int    MaxDurability; // 0 = indestrutível, >0 = degradável

        public EquipmentSlot SlotEnum
        {
            get => (EquipmentSlot)Slot;
            set => Slot = (byte)value;
        }

        public bool IsEmpty => string.IsNullOrEmpty(ItemId);

        public static EquippedItemData Make(EquipmentSlot slot, string itemId,
                                            int durability = -1, int maxDurability = 0)
        {
            return new EquippedItemData
            {
                Slot          = (byte)slot,
                ItemId        = itemId ?? "",
                Durability    = maxDurability > 0 ? Math.Max(0, durability < 0 ? maxDurability : durability) : -1,
                MaxDurability = maxDurability
            };
        }

        public bool Equals(EquippedItemData other)
            => Slot == other.Slot && ItemId == other.ItemId
               && Durability == other.Durability && MaxDurability == other.MaxDurability;

        public override bool Equals(object obj) => obj is EquippedItemData e && Equals(e);

        public override int GetHashCode()
            => unchecked((Slot * 397) ^ (ItemId?.GetHashCode() ?? 0) ^ Durability ^ MaxDurability);
    }

    /// <summary>
    /// Requisitos para equipar um item.
    ///
    /// Validados SOMENTE no servidor (NetworkInventory.ServerEquipItem).
    /// O cliente exibe os requisitos no tooltip mas nunca confia no resultado
    /// para liberar o equip — toda a validação é server-authoritative.
    /// </summary>
    [Serializable]
    public class EquipmentRequirements
    {
        public int MinLevel = 1;
        public int MinSTR, MinAGI, MinVIT, MinDEX, MinINT, MinLUK;

        [Tooltip("Bit-flags de raças que podem equipar. Padrão: All (qualquer raça).")]
        public CharacterRaceFlags AllowedRaces = CharacterRaceFlags.All;

        /// <summary>
        /// Verifica se o personagem pode equipar este item.
        /// Recebe atributos TOTAIS já somados (base + raça + alocados + buffs).
        /// </summary>
        public bool Check(int level,
                          int totalSTR, int totalAGI, int totalVIT,
                          int totalDEX, int totalINT, int totalLUK,
                          CharacterRace race,
                          out string failReason)
        {
            if (level < MinLevel)
            { failReason = $"Requer nível {MinLevel} (você é {level})."; return false; }

            if (totalSTR < MinSTR) { failReason = $"Requer STR {MinSTR}.";                         return false; }
            if (totalAGI < MinAGI) { failReason = $"Requer AGI {MinAGI}.";                         return false; }
            if (totalVIT < MinVIT) { failReason = $"Requer VIT {MinVIT}.";                         return false; }
            if (totalDEX < MinDEX) { failReason = $"Requer DEX {MinDEX}.";                         return false; }
            if (totalINT < MinINT) { failReason = $"Requer INT {MinINT}.";                         return false; }
            if (totalLUK < MinLUK) { failReason = $"Requer LUK {MinLUK}.";                         return false; }

            var raceFlag = EquipmentSlotEx.ToFlag(race);
            if ((AllowedRaces & raceFlag) == 0)
            { failReason = $"Sua raça não pode equipar este item."; return false; }

            failReason = null;
            return true;
        }

        /// <summary>
        /// Verifica apenas pelo nível e atributos visíveis (sem raça).
        /// Útil para coloração no tooltip sem precisar simular tudo.
        /// </summary>
        public bool CheckBasic(int level,
                               int totalSTR, int totalAGI, int totalVIT,
                               int totalDEX, int totalINT, int totalLUK)
        {
            return level    >= MinLevel
                && totalSTR >= MinSTR && totalAGI >= MinAGI && totalVIT >= MinVIT
                && totalDEX >= MinDEX && totalINT >= MinINT && totalLUK >= MinLUK;
        }
    }

    /// <summary>
    /// Helpers estáticos para EquipmentSlot — nomes de exibição, agrupamento, etc.
    /// </summary>
    public static class EquipmentSlotEx
    {
        /// <summary>
        /// Slots VISÍVEIS na UI hoje. Para adicionar Cape/Necklace etc:
        /// inclua aqui e crie o EquipmentSlotUI correspondente em EquipmentPanelUI.
        /// </summary>
        public static readonly EquipmentSlot[] ActiveSlots =
        {
            EquipmentSlot.Weapon,
            EquipmentSlot.Shield,
            EquipmentSlot.Helmet,
            EquipmentSlot.Armor,
            EquipmentSlot.Gloves,
            EquipmentSlot.Boots,
            EquipmentSlot.Ring1,
            EquipmentSlot.Ring2,
        };

        /// <summary>Conjunto rápido de lookup para validar se um slot está ativo.</summary>
        private static readonly HashSet<EquipmentSlot> _activeSet = new HashSet<EquipmentSlot>(ActiveSlots);

        public static bool IsActive(EquipmentSlot slot) => _activeSet.Contains(slot);

        public static bool IsRing(EquipmentSlot slot)
            => slot == EquipmentSlot.Ring1 || slot == EquipmentSlot.Ring2;

        public static bool IsEarring(EquipmentSlot slot)
            => slot == EquipmentSlot.Earring1 || slot == EquipmentSlot.Earring2;

        public static string DisplayName(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Weapon   => "Arma",
            EquipmentSlot.Shield   => "Escudo",
            EquipmentSlot.Helmet   => "Elmo",
            EquipmentSlot.Armor    => "Armadura",
            EquipmentSlot.Gloves   => "Luvas",
            EquipmentSlot.Boots    => "Botas",
            EquipmentSlot.Ring1    => "Anel I",
            EquipmentSlot.Ring2    => "Anel II",
            EquipmentSlot.Cape     => "Capa",
            EquipmentSlot.Necklace => "Colar",
            EquipmentSlot.Earring1 => "Brinco I",
            EquipmentSlot.Earring2 => "Brinco II",
            EquipmentSlot.None     => "Nenhum",
            _                      => slot.ToString()
        };

        public static CharacterRaceFlags ToFlag(CharacterRace race) => race switch
        {
            CharacterRace.Human  => CharacterRaceFlags.Human,
            CharacterRace.Elf    => CharacterRaceFlags.Elf,
            CharacterRace.Dwarf  => CharacterRaceFlags.Dwarf,
            CharacterRace.Orc    => CharacterRaceFlags.Orc,
            CharacterRace.Undead => CharacterRaceFlags.Undead,
            _                    => CharacterRaceFlags.None
        };

        /// <summary>
        /// Converte um conjunto de flags em texto legível.
        /// Ex: "Apenas Anões, Orcs" ou "Todas as raças".
        /// </summary>
        public static string FlagsDisplayName(CharacterRaceFlags flags)
        {
            if (flags == CharacterRaceFlags.None) return "Nenhuma raça";
            if (flags == CharacterRaceFlags.All)  return "Todas as raças";

            var names = new List<string>(5);
            if ((flags & CharacterRaceFlags.Human)  != 0) names.Add("Humanos");
            if ((flags & CharacterRaceFlags.Elf)    != 0) names.Add("Elfos");
            if ((flags & CharacterRaceFlags.Dwarf)  != 0) names.Add("Anões");
            if ((flags & CharacterRaceFlags.Orc)    != 0) names.Add("Orcs");
            if ((flags & CharacterRaceFlags.Undead) != 0) names.Add("Mortos-Vivos");
            return $"Apenas {string.Join(", ", names)}";
        }

        /// <summary>
        /// Determina se um item pode ir no slot escolhido pelo jogador.
        /// Itens de anel podem ir em Ring1 OU Ring2; brincos idem.
        /// Outros itens devem ir no slot exato.
        /// </summary>
        public static bool CanItemFitInSlot(EquipmentSlot itemSlot, EquipmentSlot targetSlot)
        {
            if (itemSlot == targetSlot) return true;
            if (IsRing(itemSlot)    && IsRing(targetSlot))    return true;
            if (IsEarring(itemSlot) && IsEarring(targetSlot)) return true;
            return false;
        }

        /// <summary>
        /// Soma os bônus de TODOS os itens equipados em um único EquipmentBonuses.
        /// Usado tanto no servidor (ao recalcular stats) quanto no cliente
        /// (ao aplicar mudanças de SyncList no PlayerEntity).
        ///
        /// Se um itemId não existir no ItemDatabase, é silenciosamente ignorado
        /// (item removido em patch, etc) — nunca crasha.
        /// </summary>
        public static EquipmentBonuses AggregateBonuses(IEnumerable<EquippedItemData> equipped)
        {
            var b = new EquipmentBonuses();
            if (equipped == null) return b;

            var db = ItemDatabase.Instance;
            if (db == null) return b;

            foreach (var slot in equipped)
            {
                if (slot.IsEmpty) continue;

                var item = db.GetItem(slot.ItemId);
                if (item == null || !item.IsEquipment) continue;

                b.STR += item.BonusSTR;
                b.AGI += item.BonusAGI;
                b.VIT += item.BonusVIT;
                b.DEX += item.BonusDEX;
                b.INT += item.BonusINT;
                b.LUK += item.BonusLUK;

                b.ATK     += item.BonusATK;
                b.DEF     += item.BonusDEF;
                b.MATK    += item.BonusMATK;
                b.MDEF    += item.BonusMDEF;
                b.HPBonus += item.BonusHP;
                b.MPBonus += item.BonusMP;

                b.ResistFire      += item.BonusResistFire;
                b.ResistIce       += item.BonusResistIce;
                b.ResistPoison    += item.BonusResistPoison;
                b.ResistLightning += item.BonusResistLightning;
            }
            return b;
        }
    }
}
