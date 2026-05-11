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
    /// NetworkInventory v4.1
    ///
    /// CORREÇÃO v4.1 — BUG "Command called without active client":
    ///   CmdAutoEquip invocava CmdEquipItem internamente, mas Mirror não
    ///   permite que um [Command] chame outro [Command]. Resultado: nada
    ///   acontecia ao clicar em "Equipar" e o servidor logava o erro
    ///   "Command CmdEquipItem called on NetworkPlayerPrefab(Clone)
    ///    without an active client."
    ///
    ///   Solução: a lógica de equip foi extraída para o método [Server]
    ///   privado ServerEquipItem. Tanto CmdEquipItem quanto CmdAutoEquip
    ///   agora são wrappers finos que delegam a esse método.
    ///
    /// MUDANÇAS v4 — SISTEMA DE EQUIPAMENTOS COMPLETO:
    ///
    ///   NOVO — SyncList<EquippedItemData> EquippedItems:
    ///     Lista sincronizada de itens equipados (Weapon, Shield, Helmet, Armor,
    ///     Gloves, Boots, Ring1, Ring2 — extensível para Cape, Necklace, etc).
    ///     Apenas o servidor modifica; clientes recebem via SyncList callback.
    ///
    ///   NOVO — CmdEquipItem(int inventorySlotIndex, byte targetSlot):
    ///     Cliente solicita equipar; servidor valida tipo, requisitos
    ///     (nível, atributos, raça), realiza swap atômico se já houver item no
    ///     slot, e dispara recálculo de stats no NetworkPlayer.
    ///
    ///   NOVO — CmdUnequipItem(byte slot):
    ///     Devolve o equipamento ao inventário e dispara recálculo de stats.
    ///
    ///   NOVO — CmdAutoEquip(int inventorySlotIndex):
    ///     Determina automaticamente o slot apropriado (especialmente útil
    ///     para itens não-anel). Para anéis, prefere Ring1 vazio, depois Ring2.
    ///
    ///   NOVO — Persistência completa via DatabaseManager.LoadEquipped/SaveEquipped.
    ///
    ///   NOVO — Eventos cliente:
    ///     OnEquipmentChanged → disparado quando QUALQUER slot equipado muda.
    ///
    ///   Todas as correções v3 mantidas (BUG-01 swap de joia, BUG-02 nextSlot,
    ///   BUG-17 hooks individuais).
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class NetworkInventory : NetworkBehaviour
    {
        // ── SyncList — Inventário (slots livres) ───────────────────────────
        public readonly SyncList<InventorySlotData> Slots = new SyncList<InventorySlotData>();

        // ── SyncList — Equipamentos (slots ocupados por equip) ─────────────
        public readonly SyncList<EquippedItemData> EquippedItems = new SyncList<EquippedItemData>();

        // ── SyncVars — Joias Equipadas (mantidas como estavam) ─────────────
        [SyncVar(hook = nameof(OnGemSlotQChanged))] public string GemSlotQ = "";
        [SyncVar(hook = nameof(OnGemSlotWChanged))] public string GemSlotW = "";
        [SyncVar(hook = nameof(OnGemSlotEChanged))] public string GemSlotE = "";
        [SyncVar(hook = nameof(OnGemSlotRChanged))] public string GemSlotR = "";

        // ── Eventos (cliente) ──────────────────────────────────────────────
        public event Action OnInventoryChanged;
        public event Action OnGemLoadoutChanged;
        public event Action OnEquipmentChanged;

        private int _nextSlotIndex = 0;

        // Referência cacheada para o NetworkPlayer (mesma GameObject)
        private NetworkPlayer _netPlayer;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            _netPlayer = GetComponent<NetworkPlayer>();
        }

        public override void OnStartClient()
        {
            Slots.Callback         += OnSlotsChanged;
            EquippedItems.Callback += OnEquippedItemsChangedClient;
        }

        public override void OnStartLocalPlayer()
        {
            StartCoroutine(BindUIDelayed());
        }

        public override void OnStopClient()
        {
            Slots.Callback         -= OnSlotsChanged;
            EquippedItems.Callback -= OnEquippedItemsChangedClient;
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

        private void OnEquippedItemsChangedClient(SyncList<EquippedItemData>.Operation op,
                                                  int index, EquippedItemData oldItem, EquippedItemData newItem)
            => OnEquipmentChanged?.Invoke();

        private void OnGemSlotQChanged(string oldVal, string newVal) => OnGemLoadoutChanged?.Invoke();
        private void OnGemSlotWChanged(string oldVal, string newVal) => OnGemLoadoutChanged?.Invoke();
        private void OnGemSlotEChanged(string oldVal, string newVal) => OnGemLoadoutChanged?.Invoke();
        private void OnGemSlotRChanged(string oldVal, string newVal) => OnGemLoadoutChanged?.Invoke();

        // ══════════════════════════════════════════════════════════════════
        // INVENTÁRIO — API do servidor (mantido)
        // ══════════════════════════════════════════════════════════════════

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

            var slot = new InventorySlotData
            {
                SlotIndex = _nextSlotIndex++,
                ItemId    = itemId,
                Quantity  = quantity
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

        // ══════════════════════════════════════════════════════════════════
        // EQUIPAMENTOS — API do servidor (NOVO)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Carrega equipamentos do banco para a SyncList.
        /// Chamado durante NetworkPlayer.ServerInitialize.
        /// </summary>
        [Server]
        public void ServerLoadEquippedFromDatabase(string characterId)
        {
            var db = Managers.DatabaseManager.Instance;
            if (db == null) return;

            EquippedItems.Clear();

            var rows = db.LoadEquipped(characterId);
            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.ItemId)) continue;
                EquippedItems.Add(new EquippedItemData
                {
                    Slot          = (byte)row.Slot,
                    ItemId        = row.ItemId,
                    Durability    = row.Durability,
                    MaxDurability = row.MaxDurability
                });
            }

            Debug.Log($"[NetworkInventory] {EquippedItems.Count} equipamentos carregados para char:{characterId}");
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
            db.SaveEquipped(characterId, new List<EquippedItemData>(EquippedItems));
        }

        /// <summary>Encontra o índice na SyncList do item equipado naquele slot, ou -1.</summary>
        [Server]
        private int ServerFindEquippedIndex(EquipmentSlot slot)
        {
            for (int i = 0; i < EquippedItems.Count; i++)
                if (EquippedItems[i].Slot == (byte)slot) return i;
            return -1;
        }

        /// <summary>
        /// Versão server-side de GetEquipped: retorna o ItemId no slot, ou string vazia.
        /// </summary>
        [Server]
        public string ServerGetEquipped(EquipmentSlot slot)
        {
            int idx = ServerFindEquippedIndex(slot);
            return idx >= 0 ? EquippedItems[idx].ItemId : "";
        }

        /// <summary>
        /// API pública (cliente e servidor) para consultar item equipado em um slot.
        /// </summary>
        public string GetEquipped(EquipmentSlot slot)
        {
            for (int i = 0; i < EquippedItems.Count; i++)
                if (EquippedItems[i].Slot == (byte)slot) return EquippedItems[i].ItemId;
            return "";
        }

        public bool IsSlotOccupied(EquipmentSlot slot) => !string.IsNullOrEmpty(GetEquipped(slot));

        // ══════════════════════════════════════════════════════════════════
        // EQUIPAMENTOS — Commands do cliente
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Equipa o item no slot escolhido pelo jogador.
        ///
        /// FLUXO:
        ///   1. Servidor valida que o item existe no inventário do solicitante.
        ///   2. Valida que o item é Equipment.
        ///   3. Valida que o slot escolhido aceita esse tipo de item.
        ///   4. Valida requisitos (nível, atributos, raça) via NetworkPlayer.ServerStats.
        ///   5. Se já há item no slot alvo, devolve ao inventário (swap atômico).
        ///   6. Remove o item escolhido do inventário.
        ///   7. Adiciona à lista EquippedItems.
        ///   8. Notifica o NetworkPlayer para recalcular stats.
        ///
        /// targetSlot pode ser EquipmentSlot.None — neste caso usa o EquipSlot do
        /// próprio item; isto é equivalente ao "auto-equip" do botão Equipar.
        /// </summary>
        [Command]
        public void CmdEquipItem(int inventorySlotIndex, byte targetSlotByte)
            => ServerEquipItem(inventorySlotIndex, targetSlotByte);

        /// <summary>
        /// CORREÇÃO v4.1 — Implementação server-side compartilhada por
        /// CmdEquipItem e CmdAutoEquip.
        ///
        /// MOTIVO: um [Command] NÃO pode invocar outro [Command]. Quando
        /// CmdAutoEquip chamava CmdEquipItem internamente, o Mirror lançava:
        ///   "Command CmdEquipItem called on NetworkPlayerPrefab(Clone)
        ///    without an active client."
        /// O fluxo correto é cada Cmd delegar a um método [Server] privado.
        /// </summary>
        [Server]
        private void ServerEquipItem(int inventorySlotIndex, byte targetSlotByte)
        {
            if (_netPlayer == null) return;
            if (_netPlayer.Dead)    return;

            // 1. Encontra o item no inventário
            InventorySlotData? foundSlot = null;
            foreach (var s in Slots)
                if (s.SlotIndex == inventorySlotIndex) { foundSlot = s; break; }

            if (foundSlot == null)
            {
                _netPlayer.RpcShowMessageToOwner("Item não encontrado no inventário.");
                return;
            }

            // 2. Valida tipo
            var itemData = ItemDatabase.Instance?.GetItem(foundSlot.Value.ItemId);
            if (itemData == null || !itemData.IsEquipment)
            {
                _netPlayer.RpcShowMessageToOwner("Este item não pode ser equipado.");
                return;
            }

            // 3. Determina slot final
            EquipmentSlot itemSlot   = itemData.EquipSlot;
            EquipmentSlot targetSlot = (EquipmentSlot)targetSlotByte;

            if (targetSlot == EquipmentSlot.None)
                targetSlot = ResolveAutoEquipSlot(itemSlot);

            if (!EquipmentSlotEx.IsActive(targetSlot))
            {
                _netPlayer.RpcShowMessageToOwner("Slot de equipamento inválido.");
                return;
            }

            if (!EquipmentSlotEx.CanItemFitInSlot(itemSlot, targetSlot))
            {
                _netPlayer.RpcShowMessageToOwner($"Este item não vai no slot {EquipmentSlotEx.DisplayName(targetSlot)}.");
                return;
            }

            // 4. Valida requisitos (server-authoritative)
            if (!ServerValidateRequirements(itemData, out string reason))
            {
                _netPlayer.RpcShowMessageToOwner(reason);
                return;
            }

            // 5. Swap se já houver item no slot alvo
            int existingIdx = ServerFindEquippedIndex(targetSlot);
            if (existingIdx >= 0)
            {
                var current = EquippedItems[existingIdx];
                if (!string.IsNullOrEmpty(current.ItemId))
                {
                    int returnedSlot = ServerAddItem(current.ItemId, 1);
                    if (returnedSlot < 0)
                    {
                        Debug.LogError($"[NetworkInventory] Swap falhou — item '{current.ItemId}' não pôde voltar ao inventário.");
                        _netPlayer.RpcShowMessageToOwner("Inventário cheio para realizar a troca.");
                        return;
                    }
                }
                EquippedItems.RemoveAt(existingIdx);
            }

            // 6. Remove do inventário
            if (!ServerRemoveSlot(inventorySlotIndex))
            {
                Debug.LogError($"[NetworkInventory] ServerEquipItem: ServerRemoveSlot({inventorySlotIndex}) falhou após validar.");
                return;
            }

            // 7. Adiciona à lista de equipamentos
            int maxDur = Mathf.Max(0, itemData.MaxDurability);
            EquippedItems.Add(new EquippedItemData
            {
                Slot          = (byte)targetSlot,
                ItemId        = itemData.ItemId,
                Durability    = maxDur > 0 ? maxDur : -1,
                MaxDurability = maxDur
            });

            // 8. Recalcular stats (no servidor)
            _netPlayer.ServerOnEquipmentChanged();

            Debug.Log($"[NetworkInventory] {_netPlayer.CharacterName} equipou '{itemData.DisplayName}' em {EquipmentSlotEx.DisplayName(targetSlot)}.");
        }

        /// <summary>
        /// Desequipa o item do slot e o devolve ao inventário.
        /// </summary>
        [Command]
        public void CmdUnequipItem(byte slotByte)
        {
            if (_netPlayer == null) return;
            if (_netPlayer.Dead)    return;

            EquipmentSlot slot = (EquipmentSlot)slotByte;
            int idx = ServerFindEquippedIndex(slot);
            if (idx < 0)
            {
                _netPlayer.RpcShowMessageToOwner("Slot já está vazio.");
                return;
            }

            string itemId = EquippedItems[idx].ItemId;
            if (string.IsNullOrEmpty(itemId))
            {
                EquippedItems.RemoveAt(idx);
                _netPlayer.ServerOnEquipmentChanged();
                return;
            }

            // Devolve ao inventário ANTES de remover do slot equipado
            int returnedSlot = ServerAddItem(itemId, 1);
            if (returnedSlot < 0)
            {
                _netPlayer.RpcShowMessageToOwner("Inventário cheio — não foi possível desequipar.");
                return;
            }

            EquippedItems.RemoveAt(idx);
            _netPlayer.ServerOnEquipmentChanged();

            Debug.Log($"[NetworkInventory] {_netPlayer.CharacterName} desequipou {EquipmentSlotEx.DisplayName(slot)}.");
        }

        /// <summary>
        /// Atalho: equipa no slot natural do item, com auto-resolução para anéis/brincos.
        /// Chamado pelo botão "Equipar" do InventoryUI.
        ///
        /// CORREÇÃO v4.1: agora chama ServerEquipItem (método [Server] privado)
        /// em vez de invocar outro [Command]. Ver explicação em ServerEquipItem.
        /// </summary>
        [Command]
        public void CmdAutoEquip(int inventorySlotIndex)
            => ServerEquipItem(inventorySlotIndex, (byte)EquipmentSlot.None);

        // ── Validação de requisitos ────────────────────────────────────────

        /// <summary>
        /// Verifica se o personagem cumpre os requisitos para equipar este item.
        /// Usa os atributos TOTAIS já calculados no _netPlayer (base + raça + alocados + buffs).
        /// </summary>
        [Server]
        private bool ServerValidateRequirements(ItemData item, out string failReason)
        {
            failReason = null;
            if (item?.Requirements == null) return true;

            int level = _netPlayer.Level;

            // Recompõe atributos totais a partir dos SyncVars
            var bonus = StatsCalculator.GetRaceBonus((CharacterRace)Enum.Parse(typeof(CharacterRace), _netPlayer.RaceStr));

            int totalSTR = _netPlayer.BaseSTR + bonus.STR + _netPlayer.AllocatedSTR;
            int totalAGI = _netPlayer.BaseAGI + bonus.AGI + _netPlayer.AllocatedAGI;
            int totalVIT = _netPlayer.BaseVIT + bonus.VIT + _netPlayer.AllocatedVIT;
            int totalDEX = _netPlayer.BaseDEX + bonus.DEX + _netPlayer.AllocatedDEX;
            int totalINT = _netPlayer.BaseINT + bonus.INT + _netPlayer.AllocatedINT;
            int totalLUK = _netPlayer.BaseLUK + bonus.LUK + _netPlayer.AllocatedLUK;

            CharacterRace race = (CharacterRace)Enum.Parse(typeof(CharacterRace), _netPlayer.RaceStr);

            return item.Requirements.Check(
                level, totalSTR, totalAGI, totalVIT, totalDEX, totalINT, totalLUK,
                race, out failReason);
        }

        /// <summary>
        /// Determina o slot adequado quando o cliente pede auto-equip.
        /// Para itens não-anel/brinco: usa o slot natural.
        /// Para anéis: prefere Ring1 vazio, depois Ring2, depois Ring1 (sobrescreve).
        /// Para brincos: idem.
        /// </summary>
        [Server]
        private EquipmentSlot ResolveAutoEquipSlot(EquipmentSlot itemSlot)
        {
            if (EquipmentSlotEx.IsRing(itemSlot))
            {
                if (string.IsNullOrEmpty(ServerGetEquipped(EquipmentSlot.Ring1))) return EquipmentSlot.Ring1;
                if (string.IsNullOrEmpty(ServerGetEquipped(EquipmentSlot.Ring2))) return EquipmentSlot.Ring2;
                return EquipmentSlot.Ring1; // ambos cheios — usa Ring1 (swap)
            }

            if (EquipmentSlotEx.IsEarring(itemSlot))
            {
                if (string.IsNullOrEmpty(ServerGetEquipped(EquipmentSlot.Earring1))) return EquipmentSlot.Earring1;
                if (string.IsNullOrEmpty(ServerGetEquipped(EquipmentSlot.Earring2))) return EquipmentSlot.Earring2;
                return EquipmentSlot.Earring1;
            }

            return itemSlot;
        }

        // ══════════════════════════════════════════════════════════════════
        // JOIAS DO PODER — Commands (mantidos)
        // ══════════════════════════════════════════════════════════════════

        [Command]
        public void CmdEquipGem(int skillSlotIndex, int inventorySlotIndex)
        {
            if (skillSlotIndex < 0 || skillSlotIndex > 3) return;

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

            string currentGemId = GetGemItemId(skillSlotIndex);
            if (!string.IsNullOrEmpty(currentGemId))
            {
                ServerAddItem(currentGemId, 1);
                Debug.Log($"[NetworkInventory] Swap: '{currentGemId}' devolvida ao inventário.");
            }

            ServerRemoveSlot(inventorySlotIndex);

            ServerSetGemSlot(skillSlotIndex, foundSlot.Value.ItemId);
            Debug.Log($"[NetworkInventory] '{itemData.DisplayName}' equipada no slot {skillSlotIndex}.");
        }

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

            int newSlot = ServerAddItem(gemId, 1);
            if (newSlot < 0)
            {
                Debug.LogError($"[NetworkInventory] Falha ao devolver '{gemId}' ao inventário! Item perdido.");
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
        // INVENTÁRIO — Commands diversos (mantidos)
        // ══════════════════════════════════════════════════════════════════

        [Command]
        public void CmdRemoveItem(int inventorySlotIndex)
        {
            bool removed = ServerRemoveSlot(inventorySlotIndex);
            if (removed)
                Debug.Log($"[NetworkInventory] Item descartado: slot {inventorySlotIndex}");
            else
                Debug.LogWarning($"[NetworkInventory] CmdRemoveItem: slot {inventorySlotIndex} não encontrado.");
        }

        [Command]
        public void CmdUseConsumable(int inventorySlotIndex)
        {
            InventorySlotData? foundSlot = null;
            foreach (var s in Slots)
                if (s.SlotIndex == inventorySlotIndex) { foundSlot = s; break; }
            if (foundSlot == null) return;

            var itemData = ItemDatabase.Instance?.GetItem(foundSlot.Value.ItemId);
            if (itemData == null || !itemData.IsConsumable) return;

            var netPlayer = _netPlayer ?? GetComponent<NetworkPlayer>();
            if (netPlayer == null || netPlayer.Dead) return;

            if (itemData.HealAmount > 0f) netPlayer.ServerApplyHeal(itemData.HealAmount);
            if (itemData.ManaAmount > 0f) netPlayer.ServerRestoreMP(itemData.ManaAmount);

            ServerRemoveSlot(foundSlot.Value.SlotIndex);
            Debug.Log($"[NetworkInventory] Consumível '{itemData.DisplayName}' usado.");
        }

        // ══════════════════════════════════════════════════════════════════
        // API de leitura — Joias (mantida)
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

        // ══════════════════════════════════════════════════════════════════
        // API de leitura — Equipamentos
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Constrói EquipmentBonuses agregado de TODOS os itens equipados.
        /// Pode ser chamado tanto no servidor quanto no cliente.
        /// </summary>
        public EquipmentBonuses BuildEquipmentBonuses()
            => EquipmentSlotEx.AggregateBonuses(EquippedItems);

        public int EquippedItemCount() => EquippedItems.Count;
    }
}