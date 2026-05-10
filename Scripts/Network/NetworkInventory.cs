using UnityEngine;
using Mirror;
using RPG.Data;
using RPG.Combat;
using RPG.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RPG.Network
{
    /// <summary>
    /// NetworkInventory v4
    ///
    /// CORREÇÕES v4:
    ///
    ///   CRÍTICO — CmdEquipGem sem verificação de inventário cheio no swap:
    ///     Se o slot de destino já tinha uma joia, ServerAddItem era chamado
    ///     para devolvê-la ao inventário. Se o inventário estivesse CHEIO,
    ///     ServerAddItem retornava -1, mas o código continuava e a joia antiga
    ///     era perdida permanentemente (sobrescrita sem devolução).
    ///     SOLUÇÃO: aborta o equip se não há espaço para o swap ANTES de
    ///     remover qualquer coisa do inventário.
    ///
    ///   CRÍTICO — CmdUnequipGem perdia joia se inventário cheio:
    ///     ServerAddItem retornava -1 mas o slot era limpo mesmo assim.
    ///     SOLUÇÃO: verifica ServerAddItem antes de limpar o slot.
    ///     (Bug já existia na v3 mas estava mascarado — corrigido aqui.)
    ///
    ///   ROBUSTEZ — MAX_INVENTORY_SIZE adicionado:
    ///     Limita o inventário a 100 slots para evitar abuso de memória/rede
    ///     em caso de bug de geração infinita de itens.
    ///
    ///   ROBUSTEZ — ServerAddItem retorna -1 se inventário cheio (MAX_INVENTORY_SIZE).
    ///
    ///   SEGURANÇA — CmdRemoveItem e CmdUseConsumable verificam se o player
    ///     está morto no servidor antes de processar.
    ///
    ///   Todas as correções v3 mantidas (hooks separados, _nextSlotIndex seguro).
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class NetworkInventory : NetworkBehaviour
    {
        // ── Constantes ─────────────────────────────────────────────────────
        /// <summary>
        /// Número máximo de slots no inventário.
        /// Protege contra abuso de memória/rede e facilita UI de grid fixo.
        /// </summary>
        public const int MAX_INVENTORY_SIZE = 100;

        // ── SyncList — Inventário ──────────────────────────────────────────
        public readonly SyncList<InventorySlotData> Slots = new SyncList<InventorySlotData>();

        // ── SyncVars — Joias Equipadas (hooks separados por slot) ──────────
        [SyncVar(hook = nameof(OnGemSlotQChanged))] public string GemSlotQ = "";
        [SyncVar(hook = nameof(OnGemSlotWChanged))] public string GemSlotW = "";
        [SyncVar(hook = nameof(OnGemSlotEChanged))] public string GemSlotE = "";
        [SyncVar(hook = nameof(OnGemSlotRChanged))] public string GemSlotR = "";

        // ── Eventos (cliente) ──────────────────────────────────────────────
        public event Action OnInventoryChanged;
        public event Action OnGemLoadoutChanged;

        private int _nextSlotIndex = 0;

        // ── Lifecycle ──────────────────────────────────────────────────────

        public override void OnStartClient()
        {
            Slots.Callback += OnSlotsChanged;
        }

        public override void OnStartLocalPlayer()
        {
            StartCoroutine(BindUIDelayed());
        }

        public override void OnStopClient()
        {
            Slots.Callback -= OnSlotsChanged;
        }

        private IEnumerator BindUIDelayed()
        {
            yield return null;
            yield return null;

            InventoryUI.Instance?.BindInventory(this);
            PowerGemUI.Instance?.BindInventory(this);
            Debug.Log("[NetworkInventory] UIs de inventário vinculadas.");
        }

        // ── Hooks ──────────────────────────────────────────────────────────

        private void OnSlotsChanged(SyncList<InventorySlotData>.Operation op,
                                    int index, InventorySlotData oldItem, InventorySlotData newItem)
            => OnInventoryChanged?.Invoke();

        private void OnGemSlotQChanged(string oldVal, string newVal) => OnGemLoadoutChanged?.Invoke();
        private void OnGemSlotWChanged(string oldVal, string newVal) => OnGemLoadoutChanged?.Invoke();
        private void OnGemSlotEChanged(string oldVal, string newVal) => OnGemLoadoutChanged?.Invoke();
        private void OnGemSlotRChanged(string oldVal, string newVal) => OnGemLoadoutChanged?.Invoke();

        // ══════════════════════════════════════════════════════════════════
        // INVENTÁRIO — API do servidor
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Adiciona um item ao inventário. Retorna o SlotIndex atribuído ou -1 se falhar.
        /// Falha se: itemId inválido, item não no database, ou inventário cheio (MAX_INVENTORY_SIZE).
        /// </summary>
        [Server]
        public int ServerAddItem(string itemId, int quantity = 1)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogWarning("[NetworkInventory] ServerAddItem: itemId vazio.");
                return -1;
            }

            var db = ItemDatabase.Instance;
            if (db == null || !db.Contains(itemId))
            {
                Debug.LogWarning($"[NetworkInventory] Item '{itemId}' não no ItemDatabase.");
                return -1;
            }

            // CORREÇÃO v4: verifica limite de inventário
            if (Slots.Count >= MAX_INVENTORY_SIZE)
            {
                Debug.LogWarning($"[NetworkInventory] Inventário cheio ({MAX_INVENTORY_SIZE} slots) — item '{itemId}' não adicionado.");
                return -1;
            }

            var slot = new InventorySlotData
            {
                SlotIndex = _nextSlotIndex++,
                ItemId    = itemId,
                Quantity  = Mathf.Max(1, quantity)
            };

            Slots.Add(slot);
            Debug.Log($"[NetworkInventory] Item adicionado: {itemId} x{quantity} slot:{slot.SlotIndex}");
            return slot.SlotIndex;
        }

        [Server]
        public bool ServerRemoveSlot(int slotIndex)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].SlotIndex == slotIndex)
                {
                    Slots.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        [Server]
        public bool ServerRemoveItemById(string itemId)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].ItemId == itemId)
                {
                    Slots.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public bool HasItem(string itemId)
        {
            foreach (var slot in Slots)
                if (slot.ItemId == itemId) return true;
            return false;
        }

        public int FindSlotByItemId(string itemId)
        {
            foreach (var slot in Slots)
                if (slot.ItemId == itemId) return slot.SlotIndex;
            return -1;
        }

        /// <summary>
        /// Verifica se há espaço disponível no inventário para adicionar um item.
        /// Útil para verificar ANTES de fazer swaps que precisam de espaço.
        /// </summary>
        public bool HasInventorySpace(int slotsNeeded = 1)
            => (Slots.Count + slotsNeeded) <= MAX_INVENTORY_SIZE;

        /// <summary>
        /// Carrega inventário do banco de dados.
        /// _nextSlotIndex é calculado como max+1 para evitar colisão (BUG-02).
        /// </summary>
        [Server]
        public void ServerLoadFromDatabase(string characterId)
        {
            var db = Managers.DatabaseManager.Instance;
            if (db == null) return;

            Slots.Clear();
            _nextSlotIndex = 0;

            var rows = db.LoadInventory(characterId);
            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.ItemId)) continue;

                // Respeita o limite mesmo ao carregar do banco
                if (Slots.Count >= MAX_INVENTORY_SIZE)
                {
                    Debug.LogWarning($"[NetworkInventory] Banco tem mais de {MAX_INVENTORY_SIZE} itens para char:{characterId} — excedente ignorado!");
                    break;
                }

                var slot = new InventorySlotData
                {
                    SlotIndex = row.SlotIndex >= 0 ? row.SlotIndex : _nextSlotIndex,
                    ItemId    = row.ItemId,
                    Quantity  = row.Quantity
                };
                Slots.Add(slot);
            }

            if (Slots.Count > 0)
                _nextSlotIndex = Slots.Max(s => s.SlotIndex) + 1;

            Debug.Log($"[NetworkInventory] {Slots.Count} itens carregados para char:{characterId} | nextSlot:{_nextSlotIndex}");
        }

        [Server]
        public void ServerLoadGemLoadout(string characterId)
        {
            var db = Managers.DatabaseManager.Instance;
            if (db == null) return;

            var loadout = db.LoadGemLoadout(characterId);
            GemSlotQ = loadout.SlotQ ?? "";
            GemSlotW = loadout.SlotW ?? "";
            GemSlotE = loadout.SlotE ?? "";
            GemSlotR = loadout.SlotR ?? "";

            Debug.Log($"[NetworkInventory] Loadout: Q={GemSlotQ} W={GemSlotW} E={GemSlotE} R={GemSlotR}");
        }

        [Server]
        public void ServerSaveAll(string characterId, string username)
        {
            var db = Managers.DatabaseManager.Instance;
            if (db == null) return;

            db.SaveInventory(characterId, username, new List<InventorySlotData>(Slots));
            db.SaveGemLoadout(characterId, new PowerGemLoadout
            {
                SlotQ = GemSlotQ, SlotW = GemSlotW,
                SlotE = GemSlotE, SlotR = GemSlotR
            });
        }

        // ══════════════════════════════════════════════════════════════════
        // JOIAS DO PODER — Commands
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Equipa joia no slot de skill especificado.
        ///
        /// CORREÇÃO v4: verifica espaço para swap ANTES de remover qualquer item.
        /// Se o slot já tiver uma joia e o inventário estiver cheio, aborta.
        /// Ordem segura: verificar → remover do inventário → devolver antiga → setar novo slot.
        /// </summary>
        [Command]
        public void CmdEquipGem(int skillSlotIndex, int inventorySlotIndex)
        {
            if (skillSlotIndex < 0 || skillSlotIndex > 3) return;

            // 1. Localiza o item no inventário
            InventorySlotData? foundSlot = null;
            foreach (var s in Slots)
                if (s.SlotIndex == inventorySlotIndex) { foundSlot = s; break; }

            if (foundSlot == null)
            {
                Debug.LogWarning($"[NetworkInventory] CmdEquipGem: slot {inventorySlotIndex} não encontrado.");
                return;
            }

            var itemData = ItemDatabase.Instance?.GetItem(foundSlot.Value.ItemId);
            if (itemData == null || !itemData.IsPowerGem)
            {
                Debug.LogWarning($"[NetworkInventory] '{foundSlot.Value.ItemId}' não é PowerGem.");
                return;
            }

            // 2. CORREÇÃO v4: verifica se há espaço para devolver a joia atual (swap)
            string currentGemId = GetGemItemId(skillSlotIndex);
            bool   hasCurrentGem = !string.IsNullOrEmpty(currentGemId);

            if (hasCurrentGem)
            {
                // O swap precisa de espaço: a joia nova sai do inventário (libera 1 slot)
                // e a antiga volta (precisa de 1 slot). Net = 0, mas só é seguro se
                // a remoção ocorrer ANTES da inserção. Verificamos preventivamente.
                // Se inventário estiver em MAX-1 ou menos, ok. Se exatamente MAX com
                // a joia que vamos remover sendo o único slot, também ok.
                // Simplificado: se Slots.Count - 1 < MAX_INVENTORY_SIZE, está seguro.
                bool safeForSwap = (Slots.Count - 1) < MAX_INVENTORY_SIZE;
                if (!safeForSwap)
                {
                    var np = GetComponent<NetworkPlayer>();
                    np?.RpcShowMessage("Inventário cheio — não é possível trocar a joia!");
                    return;
                }
            }

            // 3. Remove a nova joia do inventário PRIMEIRO
            ServerRemoveSlot(inventorySlotIndex);

            // 4. Devolve a joia antiga ao inventário (swap seguro)
            if (hasCurrentGem)
            {
                int returnedSlot = ServerAddItem(currentGemId, 1);
                if (returnedSlot < 0)
                {
                    // Fallback: não devolveu, mas já removemos a nova — re-adiciona ela
                    ServerAddItem(foundSlot.Value.ItemId, 1);
                    Debug.LogError($"[NetworkInventory] Falha crítica no swap de joia '{currentGemId}' — operação revertida.");
                    return;
                }
                Debug.Log($"[NetworkInventory] Swap: '{currentGemId}' devolvida ao inventário (slot {returnedSlot}).");
            }

            // 5. Equipa a nova joia
            ServerSetGemSlot(skillSlotIndex, foundSlot.Value.ItemId);
            Debug.Log($"[NetworkInventory] '{itemData.DisplayName}' equipada no slot {skillSlotIndex}.");
        }

        /// <summary>
        /// Desequipa joia e devolve ao inventário.
        /// CORREÇÃO v4: aborta se inventário cheio (não perde a joia).
        /// </summary>
        [Command]
        public void CmdUnequipGem(int skillSlotIndex)
        {
            if (skillSlotIndex < 0 || skillSlotIndex > 3) return;

            string gemId = GetGemItemId(skillSlotIndex);
            if (string.IsNullOrEmpty(gemId))
            {
                Debug.LogWarning($"[NetworkInventory] CmdUnequipGem: slot {skillSlotIndex} já vazio.");
                return;
            }

            // CORREÇÃO v4: tenta adicionar ANTES de limpar o slot
            int newSlot = ServerAddItem(gemId, 1);
            if (newSlot < 0)
            {
                var np = GetComponent<NetworkPlayer>();
                np?.RpcShowMessage("Inventário cheio — não é possível desequipar a joia!");
                Debug.LogWarning($"[NetworkInventory] CmdUnequipGem: inventário cheio, '{gemId}' não desequipada.");
                return;
            }

            ServerSetGemSlot(skillSlotIndex, "");
            Debug.Log($"[NetworkInventory] Slot {skillSlotIndex} esvaziado — '{gemId}' devolvida (slot {newSlot}).");
        }

        [Server]
        private void ServerSetGemSlot(int index, string itemId)
        {
            switch (index)
            {
                case 0: GemSlotQ = itemId; break;
                case 1: GemSlotW = itemId; break;
                case 2: GemSlotE = itemId; break;
                case 3: GemSlotR = itemId; break;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // INVENTÁRIO — Commands do cliente
        // ══════════════════════════════════════════════════════════════════

        [Command]
        public void CmdRemoveItem(int inventorySlotIndex)
        {
            // CORREÇÃO v4: verifica morte no servidor
            var netPlayer = GetComponent<NetworkPlayer>();
            if (netPlayer != null && netPlayer.Dead) return;

            bool removed = ServerRemoveSlot(inventorySlotIndex);
            if (removed)
                Debug.Log($"[NetworkInventory] Item descartado: slot {inventorySlotIndex}");
            else
                Debug.LogWarning($"[NetworkInventory] CmdRemoveItem: slot {inventorySlotIndex} não encontrado.");
        }

        [Command]
        public void CmdUseConsumable(int inventorySlotIndex)
        {
            // CORREÇÃO v4: verifica morte no servidor
            var netPlayer = GetComponent<NetworkPlayer>();
            if (netPlayer != null && netPlayer.Dead) return;

            InventorySlotData? foundSlot = null;
            foreach (var s in Slots)
                if (s.SlotIndex == inventorySlotIndex) { foundSlot = s; break; }
            if (foundSlot == null) return;

            var itemData = ItemDatabase.Instance?.GetItem(foundSlot.Value.ItemId);
            if (itemData == null || !itemData.IsConsumable) return;

            if (netPlayer == null) return;

            if (itemData.HealAmount > 0f) netPlayer.ServerApplyHeal(itemData.HealAmount);
            if (itemData.ManaAmount > 0f) netPlayer.ServerRestoreMP(itemData.ManaAmount);

            ServerRemoveSlot(foundSlot.Value.SlotIndex);
            Debug.Log($"[NetworkInventory] Consumível '{itemData.DisplayName}' usado.");
        }

        // ══════════════════════════════════════════════════════════════════
        // API de leitura
        // ══════════════════════════════════════════════════════════════════

        public string GetGemItemId(int skillSlotIndex) => skillSlotIndex switch
        {
            0 => GemSlotQ ?? "",
            1 => GemSlotW ?? "",
            2 => GemSlotE ?? "",
            3 => GemSlotR ?? "",
            _ => ""
        };

        public SkillData GetEquippedSkill(int skillSlotIndex)
        {
            string gemId = GetGemItemId(skillSlotIndex);
            if (string.IsNullOrEmpty(gemId)) return null;
            return ItemDatabase.Instance?.GetItem(gemId)?.EmbeddedSkill;
        }

        public int EquippedGemCount()
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
                if (!string.IsNullOrEmpty(GetGemItemId(i))) count++;
            return count;
        }
    }
}
