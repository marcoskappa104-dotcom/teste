using UnityEngine;
using Mirror;
using RPG.Data;
using RPG.UI;
using System;
using System.Collections.Generic;

namespace RPG.Network
{
    /// <summary>
    /// NetworkEquipment v2
    ///
    /// CORREÇÕES v2:
    ///
    ///   INTEGRAÇÃO — ServerLoadFromDatabase e ServerSaveAll agora usam
    ///     DatabaseManager.LoadEquipmentLoadout() e SaveEquipmentLoadout(),
    ///     que existem no DatabaseManager v11. Nas versões anteriores,
    ///     essas chamadas causavam erro de compilação porque os métodos
    ///     não existiam no DatabaseManager.
    ///
    ///   SEGURANÇA — CmdEquipItem valida MeetsRequirements antes de equipar.
    ///     A validação usa EquipmentData.MeetsRequirements() corrigido (v2),
    ///     que agora compara atributos base totais em vez de stats derivados.
    ///
    ///   ROBUSTEZ — ServerRecalculateStats nunca acessa _agent se ele for null.
    ///
    ///   Todas as funcionalidades da v1 mantidas:
    ///     - 7 SyncVars de slot, hooks separados por slot.
    ///     - CmdEquipItem com swap automático.
    ///     - CmdUnequipItem com devolução ao inventário.
    ///     - BuildEquipmentBonuses para uso no ServerInitialize do NetworkPlayer.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkInventory))]
    public class NetworkEquipment : NetworkBehaviour
    {
        // ── SyncVars — um ItemId por slot (string vazia = slot vazio) ──────
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

        // ── Lifecycle ──────────────────────────────────────────────────────

        public override void OnStartClient()
        {
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

            InventorySlotData? found = null;
            foreach (var s in inventory.Slots)
                if (s.SlotIndex == inventorySlotIndex) { found = s; break; }

            if (found == null)
            {
                Debug.LogWarning($"[NetworkEquipment] CmdEquipItem: slot {inventorySlotIndex} não encontrado.");
                return;
            }

            var itemData = ItemDatabase.Instance?.GetItem(found.Value.ItemId);
            if (itemData == null || itemData.Type != ItemType.Equipment)
            {
                Debug.LogWarning($"[NetworkEquipment] '{found.Value.ItemId}' não é Equipment.");
                return;
            }

            var eqData = EquipmentDatabase.Instance?.GetEquipment(found.Value.ItemId);
            if (eqData == null)
            {
                Debug.LogWarning($"[NetworkEquipment] EquipmentData não encontrado para '{found.Value.ItemId}'.");
                return;
            }

            // Verifica requisitos usando o método corrigido (v2 de EquipmentData)
            var netPlayer = GetComponent<NetworkPlayer>();
            if (netPlayer != null && netPlayer._serverCharData != null)
            {
                if (!eqData.MeetsRequirements(netPlayer._serverCharData))
                {
                    netPlayer.RpcShowMessage($"Requisitos não atendidos para {itemData.DisplayName}!");
                    return;
                }
            }

            EquipmentSlot targetSlot  = eqData.Slot;
            string        currentItem = GetSlot(targetSlot);

            // Swap: devolve item atual ao inventário antes de equipar o novo
            if (!string.IsNullOrEmpty(currentItem))
            {
                int returnedSlot = inventory.ServerAddItem(currentItem, 1);
                if (returnedSlot < 0)
                {
                    Debug.LogError($"[NetworkEquipment] Inventário cheio — swap do slot {targetSlot} cancelado.");
                    return;
                }
                Debug.Log($"[NetworkEquipment] Swap: '{currentItem}' devolvido ao inventário (slot {returnedSlot}).");
            }

            inventory.ServerRemoveSlot(inventorySlotIndex);
            ServerSetSlot(targetSlot, found.Value.ItemId);
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
        /// Reconstrói o EquipmentBonuses completo e recalcula DerivedStats
        /// do NetworkPlayer. Atualiza MaxHP/MaxMP/MoveSpeed e StatsVersion.
        /// </summary>
        [Server]
        public void ServerRecalculateStats()
        {
            var netPlayer = GetComponent<NetworkPlayer>();
            if (netPlayer == null || netPlayer._serverCharData == null) return;

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

            // CORREÇÃO v23 do NetworkPlayer: MaxHP/MaxMP antes de CurrentHP/CurrentMP
            netPlayer.MaxHP = newMaxHP;
            netPlayer.MaxMP = newMaxMP;
            if (netPlayer.CurrentHP > newMaxHP) netPlayer.CurrentHP = newMaxHP;
            if (netPlayer.CurrentMP > newMaxMP) netPlayer.CurrentMP = newMaxMP;

            // Atualiza velocidade do NavMeshAgent se disponível
            if (netPlayer._agent != null && netPlayer._agent.isOnNavMesh)
                netPlayer._agent.speed = Mathf.Clamp(netPlayer._serverStats.MoveSpeed, 3f, 7f);

            netPlayer.StatsVersion++;

            Debug.Log($"[NetworkEquipment] Stats recalculados para {netPlayer.CharacterName}.");
        }

        /// <summary>
        /// Carrega os slots equipados do banco de dados ao iniciar o servidor.
        /// Chamado por NetworkPlayer.ServerInitialize após carregar o inventário.
        ///
        /// CORREÇÃO v2: usa DatabaseManager.LoadEquipmentLoadout() que agora existe.
        /// </summary>
        [Server]
        public void ServerLoadFromDatabase(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId)) return;

            var db = Managers.DatabaseManager.Instance;
            if (db == null)
            {
                Debug.LogWarning("[NetworkEquipment] DatabaseManager.Instance é null — loadout de equipamento não carregado.");
                return;
            }

            var loadout = db.LoadEquipmentLoadout(characterId);

            SlotWeapon = loadout.Weapon ?? "";
            SlotShield = loadout.Shield ?? "";
            SlotHelmet = loadout.Helmet ?? "";
            SlotChest  = loadout.Chest  ?? "";
            SlotLegs   = loadout.Legs   ?? "";
            SlotBoots  = loadout.Boots  ?? "";
            SlotGloves = loadout.Gloves ?? "";

            Debug.Log($"[NetworkEquipment] Loadout carregado para char:{characterId} | " +
                      $"Weapon:{SlotWeapon} Shield:{SlotShield} Helmet:{SlotHelmet} " +
                      $"Chest:{SlotChest} Legs:{SlotLegs} Boots:{SlotBoots} Gloves:{SlotGloves}");
        }

        /// <summary>
        /// Persiste o loadout de equipamentos no banco de dados.
        /// Chamado por NetworkPlayer.ServerSaveCharacterForced().
        ///
        /// CORREÇÃO v2: usa DatabaseManager.SaveEquipmentLoadout() que agora existe.
        /// </summary>
        [Server]
        public void ServerSaveAll(string characterId, string username)
        {
            if (string.IsNullOrWhiteSpace(characterId)) return;

            var db = Managers.DatabaseManager.Instance;
            if (db == null) return;

            db.SaveEquipmentLoadout(characterId, new EquipmentLoadout
            {
                CharacterId = characterId,
                Weapon      = SlotWeapon ?? "",
                Shield      = SlotShield ?? "",
                Helmet      = SlotHelmet ?? "",
                Chest       = SlotChest  ?? "",
                Legs        = SlotLegs   ?? "",
                Boots       = SlotBoots  ?? "",
                Gloves      = SlotGloves ?? ""
            });
        }

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
