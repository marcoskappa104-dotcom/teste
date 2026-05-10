// DatabaseManager_EquipmentExtension.cs
//
// INSTRUÇÃO DE USO:
//   Este arquivo NÃO substitui o DatabaseManager.cs existente.
//   Adicione o conteúdo dos métodos abaixo diretamente no DatabaseManager.cs
//   dentro da região "#if UNITY_SERVER" e seus stubs cliente correspondentes.
//
//   Alternativamente, você pode usar este arquivo como referência e copiar
//   apenas os trechos necessários.
//
// O que adicionar no DatabaseManager.cs:
//   1. Tabela EquipmentLoadoutRow (dentro do #if UNITY_SERVER)
//   2. Stub EquipmentLoadoutRow (dentro do #else)
//   3. Métodos LoadEquipmentLoadout e SaveEquipmentLoadout
//   4. Criação da tabela no InitializeDatabase: _db.CreateTable<EquipmentLoadoutRow>()
//
// ═══════════════════════════════════════════════════════════════════════════

using System;
using UnityEngine;
using RPG.Network;

namespace RPG.Managers
{
    // ──────────────────────────────────────────────────────────────────────
    // PASSO 1 — Adicione dentro do bloco #if UNITY_SERVER no DatabaseManager
    // ──────────────────────────────────────────────────────────────────────

    /*
    [Table("equipment_loadout")]
    public class EquipmentLoadoutRow
    {
        [PrimaryKey][Column("character_id")]
        public string CharacterId { get; set; }

        [Column("slot_weapon")] public string Weapon { get; set; } = "";
        [Column("slot_shield")] public string Shield { get; set; } = "";
        [Column("slot_helmet")] public string Helmet { get; set; } = "";
        [Column("slot_chest")]  public string Chest  { get; set; } = "";
        [Column("slot_legs")]   public string Legs   { get; set; } = "";
        [Column("slot_boots")]  public string Boots  { get; set; } = "";
        [Column("slot_gloves")] public string Gloves { get; set; } = "";
    }
    */

    // ──────────────────────────────────────────────────────────────────────
    // PASSO 2 — Adicione dentro do bloco #else (stubs cliente) no DatabaseManager
    // ──────────────────────────────────────────────────────────────────────

    /*
    public class EquipmentLoadoutRow
    {
        public string CharacterId { get; set; }
        public string Weapon { get; set; } = "";
        public string Shield { get; set; } = "";
        public string Helmet { get; set; } = "";
        public string Chest  { get; set; } = "";
        public string Legs   { get; set; } = "";
        public string Boots  { get; set; } = "";
        public string Gloves { get; set; } = "";
    }
    */

    // ──────────────────────────────────────────────────────────────────────
    // PASSO 3 — Adicione no InitializeDatabase (dentro do #if UNITY_SERVER):
    //   _db.CreateTable<EquipmentLoadoutRow>();
    // ──────────────────────────────────────────────────────────────────────

    // ──────────────────────────────────────────────────────────────────────
    // PASSO 4 — Adicione os métodos abaixo na região INVENTÁRIO do DatabaseManager
    // (dentro do #if UNITY_SERVER e seus stubs abaixo do #else)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Este arquivo serve como documentação e referência para os métodos de
    /// equipamento a adicionar no DatabaseManager.cs.
    ///
    /// Copie o conteúdo de DatabaseManagerEquipmentMethods e cole no DatabaseManager.cs.
    /// </summary>
    public static class DatabaseManagerEquipmentMethods
    {
        // ── MÉTODO 1: LoadEquipmentLoadout ─────────────────────────────────
        //
        // Adicione dentro da classe DatabaseManager, no bloco #if UNITY_SERVER:
        //
        // public EquipmentLoadout LoadEquipmentLoadout(string characterId)
        // {
        //     if (string.IsNullOrWhiteSpace(characterId)) return new EquipmentLoadout();
        //     try
        //     {
        //         EquipmentLoadoutRow row;
        //         lock (_dbLock) row = _db.Find<EquipmentLoadoutRow>(characterId);
        //         if (row == null) return new EquipmentLoadout { CharacterId = characterId };
        //         return new EquipmentLoadout
        //         {
        //             CharacterId = row.CharacterId,
        //             Weapon = row.Weapon ?? "", Shield = row.Shield ?? "",
        //             Helmet = row.Helmet ?? "", Chest  = row.Chest  ?? "",
        //             Legs   = row.Legs   ?? "", Boots  = row.Boots  ?? "",
        //             Gloves = row.Gloves ?? ""
        //         };
        //     }
        //     catch (Exception e) { Debug.LogError($"[DB] LoadEquipmentLoadout: {e.Message}"); return new EquipmentLoadout(); }
        // }
        //
        // ── MÉTODO 2: SaveEquipmentLoadout ────────────────────────────────
        //
        // Adicione dentro da classe DatabaseManager, no bloco #if UNITY_SERVER:
        //
        // public void SaveEquipmentLoadout(string characterId, EquipmentLoadout loadout)
        // {
        //     if (string.IsNullOrWhiteSpace(characterId)) return;
        //     string charId  = characterId;
        //     string weapon  = loadout.Weapon ?? "";
        //     string shield  = loadout.Shield ?? "";
        //     string helmet  = loadout.Helmet ?? "";
        //     string chest   = loadout.Chest  ?? "";
        //     string legs    = loadout.Legs   ?? "";
        //     string boots   = loadout.Boots  ?? "";
        //     string gloves  = loadout.Gloves ?? "";
        //
        //     EnqueueWrite(() =>
        //     {
        //         try
        //         {
        //             lock (_dbLock)
        //                 _db.InsertOrReplace(new EquipmentLoadoutRow
        //                 {
        //                     CharacterId = charId,
        //                     Weapon = weapon, Shield = shield,
        //                     Helmet = helmet, Chest  = chest,
        //                     Legs   = legs,   Boots  = boots,
        //                     Gloves = gloves
        //                 });
        //         }
        //         catch (Exception e) { Debug.LogError($"[DB] SaveEquipmentLoadout: {e.Message}"); }
        //     });
        // }
        //
        // ── STUBS para o bloco #else (cliente/editor) ─────────────────────
        //
        // public EquipmentLoadout LoadEquipmentLoadout(string id) => new EquipmentLoadout();
        // public void SaveEquipmentLoadout(string id, EquipmentLoadout l) { }
        //
        // ─────────────────────────────────────────────────────────────────

        // Este método estático é apenas para manter o compilador feliz.
        // Não há lógica executável aqui — tudo está nos comentários acima.
        public static void Placeholder() { }
    }
}
