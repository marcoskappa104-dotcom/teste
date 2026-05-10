using UnityEngine;
using Mirror;
using RPG.Data;
using RPG.UI;
using System;
using System.Collections.Generic;

namespace RPG.Network
{
    /// <summary>
    /// NetworkEquipment v1
    ///
    /// Gerencia os 7 slots de equipamento do jogador (Weapon, Shield, Helmet,
    /// Chest, Legs, Boots, Gloves) em modo server-authoritative.
    ///
    /// ARQUITETURA:
    ///   • SyncVars por slot — sincronização automática cliente/servidor.
    ///   • Servidor calcula EquipmentBonuses completo sempre que um slot muda.
    ///   • NetworkPlayer.ServerStats é recalculado via GetDerivedStats() com os novos bônus.
    ///   • Cliente recebe atualização via hooks e dispara OnEquipmentChanged para a UI.
    ///
    /// INTEGRAÇÃO COM O SISTEMA EXISTENTE:
    ///   • Não quebra NENHUM script existente.
    ///   • NetworkPlayer._serverCharData.EquipmentBonuses é atualizado pelo servidor.
    ///   • O mesmo StatsCalculator.Calculate() já existente recebe os bônus novos.
    ///   • DatabaseManager.SaveCharacter() já persiste EquipmentBonuses (campos equip_*).
    ///     → Para persistir os slots equipados, adicione uma tabela 'equipment_slots'
    ///       (ver tutorial de instalação).
    ///
    /// COLOQUE este componente no mesmo prefab de player onde está o NetworkInventory.
    /// Adicione [RequireComponent(typeof(NetworkEquipment))] no NetworkPlayer se desejar.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkInventory))]
    public class NetworkEquipment : NetworkBehaviour
    {
        // ── SyncVars — um ItemId por slot (string vazia = slot vazio) ──────
        // A ordem de declaração importa no Mirror: declare todos antes dos hooks.
        [SyncVar(hook = nameof(OnWeaponChanged))]  public string SlotWeapon = "";
        [SyncVar(hook = nameof(OnShieldChanged))]  public string SlotShield = "";
        [SyncVar(hook = nameof(OnHelmetChanged))]  public string SlotHelmet = "";
        [SyncVar(hook = nameof(OnChestChanged))]   public string SlotChest  = "";
        [SyncVar(hook = nameof(OnLegsChanged))]    public string SlotLegs   = "";
        [SyncVar(hook = nameof(OnBootsChanged))]   public string SlotBoots  = "";
        [SyncVar(hook = nameof(OnGlovesChanged))]  public string SlotGloves = "";

        // ── Evento para a UI ───────────────────────────────────────────────
        /// <summary>Disparado no cliente sempre que qualquer slot muda.</summary>
        public event Action OnEquipmentChanged;

        // ── Cache para persistência ────────────────────────────────────────
        private string _characterId;

        // ── Lifecycle ──────────────────────────────────────────────────────

        public override void OnStartClient()
        {
            // Vincula a EquipmentUI quando spawnar
            if (isLocalPlayer)
                EquipmentUI.Instance?.BindEquipment(this);
        }

        public override void OnStartLocalPlayer()
        {
            EquipmentUI.Instance?.BindEquipment(this);
        }

        // ── Hooks (cliente) ────────────────────────────────────────────────

        private void OnWeaponChanged(string _, string __) => OnEquipmentChanged?.Invoke();
        private void OnShieldChanged(string _, string __) => OnEquipmentChanged?.Invoke();
        private void OnHelmetChanged(string _, string __) => OnEquipmentChanged?.Invoke();
        private void OnChestChanged (string _, string __) => OnEquipmentChanged?.Invoke();
        private void OnLegsChanged  (string _, string __) => OnEquipmentChanged?.Invoke();
        private void OnBootsChanged (string _, string __) => OnEquipmentChanged?.Invoke();
        private void OnGlovesChanged(string _, string __) => OnEquipmentChanged?.Invoke();

        // ── API de leitura ─────────────────────────────────────────────────

        /// <summary>Retorna o ItemId equipado no slot, ou string.Empty se vazio.</summary>
        public string GetSlot(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Weapon => SlotWeapon ?? "",
            EquipmentSlot.Shield => SlotShield ?? "",
            EquipmentSlot.Helmet => SlotHelmet ?? "",
            EquipmentSlot.Chest  => SlotChest  ?? "",
            EquipmentSlot.Legs   => SlotLegs   ?? "",
            EquipmentSlot.Boots  => SlotBoots  ?? "",
            EquipmentSlot.Gloves => SlotGloves ?? "",
            _                    => ""
        };

        public bool IsSlotEmpty(EquipmentSlot slot) => string.IsNullOrEmpty(GetSlot(slot));

        // ── Commands (cliente → servidor) ──────────────────────────────────

        /// <summary>
        /// Equipa um item do inventário em seu slot correspondente.
        /// Se já houver item no slot, realiza swap com o inventário.
        /// </summary>
        [Command]
        public void CmdEquipItem(int inventorySlotIndex)
        {
            var inventory = GetComponent<NetworkInventory>();
            if (inventory == null) return;

            // Encontra o slot no inventário
            InventorySlotData? found = null;
            foreach (var s in inventory.Slots)
                if (s.SlotIndex == inventorySlotIndex) { found = s; break; }

            if (found == null)
            {
                Debug.LogWarning($"[NetworkEquipment] CmdEquipItem: slot {inventorySlotIndex} não encontrado.");
                return;
            }

            // Obtém o ItemData
            var itemData = ItemDatabase.Instance?.GetItem(found.Value.ItemId);
            if (itemData == null || itemData.Type != ItemType.Equipment)
            {
                Debug.LogWarning($"[NetworkEquipment] '{found.Value.ItemId}' não é Equipment.");
                return;
            }

            // Obtém o EquipmentData para saber o slot
            var eqData = EquipmentDatabase.Instance?.GetEquipment(found.Value.ItemId);
            if (eqData == null)
            {
                Debug.LogWarning($"[NetworkEquipment] EquipmentData não encontrado para '{found.Value.ItemId}'.");
                return;
            }

            // Verifica requisitos
            var netPlayer = GetComponent<NetworkPlayer>();
            if (netPlayer != null && netPlayer._serverCharData != null)
            {
                if (!eqData.MeetsRequirements(netPlayer._serverCharData))
                {
                    netPlayer.RpcShowMessage($"Requisitos não atendidos para {itemData.DisplayName}!");
                    return;
                }
            }

            EquipmentSlot targetSlot = eqData.Slot;
            string currentItemId = GetSlot(targetSlot);

            // Se já há item no slot → devolve ao inventário (swap)
            if (!string.IsNullOrEmpty(currentItemId))
            {
                int returnedSlot = inventory.ServerAddItem(currentItemId, 1);
                if (returnedSlot < 0)
                {
                    Debug.LogError($"[NetworkEquipment] Inventário cheio — não foi possível fazer swap do slot {targetSlot}.");
                    return;
                }
                Debug.Log($"[NetworkEquipment] Swap: '{currentItemId}' devolvido ao inventário.");
            }

            // Remove do inventário e equipa
            inventory.ServerRemoveSlot(inventorySlotIndex);
            ServerSetSlot(targetSlot, found.Value.ItemId);

            // Recalcula stats no servidor
            ServerRecalculateStats();

            Debug.Log($"[NetworkEquipment] '{itemData.DisplayName}' equipado no slot {targetSlot}.");
        }

        /// <summary>
        /// Desequipa o item do slot especificado e devolve ao inventário.
        /// </summary>
        [Command]
        public void CmdUnequipItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex > 6) return;
            var slot = (EquipmentSlot)slotIndex;

            string itemId = GetSlot(slot);
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogWarning($"[NetworkEquipment] CmdUnequipItem: slot {slot} já vazio.");
                return;
            }

            var inventory = GetComponent<NetworkInventory>();
            if (inventory == null) return;

            int newSlot = inventory.ServerAddItem(itemId, 1);
            if (newSlot < 0)
            {
                var netPlayer = GetComponent<NetworkPlayer>();
                netPlayer?.RpcShowMessage("Inventário cheio — não é possível desequipar!");
                return;
            }

            ServerSetSlot(slot, "");
            ServerRecalculateStats();

            Debug.Log($"[NetworkEquipment] Slot {slot} desequipado → inventário slot {newSlot}.");
        }

        // ── Métodos do servidor ────────────────────────────────────────────

        [Server]
        private void ServerSetSlot(EquipmentSlot slot, string itemId)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon: SlotWeapon = itemId; break;
                case EquipmentSlot.Shield: SlotShield = itemId; break;
                case EquipmentSlot.Helmet: SlotHelmet = itemId; break;
                case EquipmentSlot.Chest:  SlotChest  = itemId; break;
                case EquipmentSlot.Legs:   SlotLegs   = itemId; break;
                case EquipmentSlot.Boots:  SlotBoots  = itemId; break;
                case EquipmentSlot.Gloves: SlotGloves = itemId; break;
            }
        }

        /// <summary>
        /// Reconstrói o EquipmentBonuses completo a partir dos 7 slots
        /// e atualiza _serverCharData no NetworkPlayer, disparando recálculo
        /// de DerivedStats e propagação via SyncVars MaxHP/MaxMP.
        /// </summary>
        [Server]
        public void ServerRecalculateStats()
        {
            var netPlayer = GetComponent<NetworkPlayer>();
            if (netPlayer == null || netPlayer._serverCharData == null) return;

            // Reconstrói EquipmentBonuses do zero
            var bonuses = new EquipmentBonuses();
            var db = EquipmentDatabase.Instance;

            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                string id = GetSlot(slot);
                if (string.IsNullOrEmpty(id)) continue;
                db?.GetEquipment(id)?.ApplyTo(bonuses);
            }

            netPlayer._serverCharData.EquipmentBonuses = bonuses;
            netPlayer._serverStats = netPlayer._serverCharData.GetDerivedStats();

            float newMaxHP = Mathf.Min(netPlayer._serverStats.MaxHP, NetworkPlayer.MAX_HP_CAP);
            float newMaxMP = Mathf.Min(netPlayer._serverStats.MaxMP, NetworkPlayer.MAX_MP_CAP);

            // Mantém MaxHP/MaxMP ANTES de Current — alinhado com correção v23 do NetworkPlayer
            netPlayer.MaxHP = newMaxHP;
            netPlayer.MaxMP = newMaxMP;
            if (netPlayer.CurrentHP > newMaxHP) netPlayer.CurrentHP = newMaxHP;
            if (netPlayer.CurrentMP > newMaxMP) netPlayer.CurrentMP = newMaxMP;

            if (netPlayer._agent != null && netPlayer._agent.isOnNavMesh)
                netPlayer._agent.speed = Mathf.Clamp(netPlayer._serverStats.MoveSpeed, 3f, 7f);

            netPlayer.StatsVersion++;

            Debug.Log($"[NetworkEquipment] Stats recalculados para {netPlayer.CharacterName}.");
        }

        /// <summary>
        /// Carrega os slots equipados do banco de dados ao iniciar o servidor.
        /// Deve ser chamado por NetworkPlayer.ServerInitialize após carregar o inventário.
        /// </summary>
        [Server]
        public void ServerLoadFromDatabase(string characterId)
        {
            _characterId = characterId;
            var db = Managers.DatabaseManager.Instance;
            if (db == null) return;

            var loadout = db.LoadEquipmentLoadout(characterId);
            SlotWeapon = loadout.Weapon ?? "";
            SlotShield = loadout.Shield ?? "";
            SlotHelmet = loadout.Helmet ?? "";
            SlotChest  = loadout.Chest  ?? "";
            SlotLegs   = loadout.Legs   ?? "";
            SlotBoots  = loadout.Boots  ?? "";
            SlotGloves = loadout.Gloves ?? "";

            Debug.Log($"[NetworkEquipment] Loadout carregado para char:{characterId}");
        }

        [Server]
        public void ServerSaveAll(string characterId, string username)
        {
            var db = Managers.DatabaseManager.Instance;
            if (db == null) return;

            db.SaveEquipmentLoadout(characterId, new EquipmentLoadout
            {
                CharacterId = characterId,
                Weapon = SlotWeapon, Shield = SlotShield,
                Helmet = SlotHelmet, Chest  = SlotChest,
                Legs   = SlotLegs,   Boots  = SlotBoots,
                Gloves = SlotGloves
            });
        }

        // ── Reconstrução do EquipmentBonuses para uso em init ─────────────

        /// <summary>
        /// Gera um EquipmentBonuses completo com base nos slots atuais.
        /// Chamado pelo NetworkPlayer ao montar o PlayerInitData para o cliente.
        /// </summary>
        [Server]
        public EquipmentBonuses BuildEquipmentBonuses()
        {
            var bonuses = new EquipmentBonuses();
            var db = EquipmentDatabase.Instance;
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                string id = GetSlot(slot);
                if (!string.IsNullOrEmpty(id))
                    db?.GetEquipment(id)?.ApplyTo(bonuses);
            }
            return bonuses;
        }
    }

    // ── DTO para persistência (sem dependência de SQLite no cliente) ───────

    [Serializable]
    public class EquipmentLoadout
    {
        public string CharacterId;
        public string Weapon = "";
        public string Shield = "";
        public string Helmet = "";
        public string Chest  = "";
        public string Legs   = "";
        public string Boots  = "";
        public string Gloves = "";
    }
}
