// NetworkPlayer_EquipmentPatch.cs
//
// Este arquivo documenta as MODIFICAÇÕES necessárias no NetworkPlayer.cs existente
// para integrar o sistema de equipamento.
//
// NÃO é um arquivo standalone — use como guia de edição do NetworkPlayer.cs.
// Todas as modificações são aditivas (não removem nada existente).
//
// ═══════════════════════════════════════════════════════════════════════════
// MODIFICAÇÃO 1 — Expor campos internos necessários pelo NetworkEquipment
// ═══════════════════════════════════════════════════════════════════════════
//
// No NetworkPlayer.cs, altere os campos privados usados pelo NetworkEquipment
// de 'private' para 'internal' OU adicione properties públicas:
//
//   // Antes:
//   private CharacterData _serverCharData;
//   private DerivedStats  _serverStats;
//   private NavMeshAgent  _agent;
//
//   // Depois (mude o modificador de acesso):
//   internal CharacterData _serverCharData;
//   internal DerivedStats  _serverStats;
//   internal NavMeshAgent  _agent;
//
//   // OU adicione as constantes públicas (já existem como private):
//   public const float MAX_HP_CAP = 500_000f;
//   public const float MAX_MP_CAP = 200_000f;
//
// ═══════════════════════════════════════════════════════════════════════════
// MODIFICAÇÃO 2 — Adicionar NetworkEquipment no Awake e ServerInitialize
// ═══════════════════════════════════════════════════════════════════════════
//
// No Awake() do NetworkPlayer, adicione:
//
//   private NetworkEquipment _equipment;
//
//   private void Awake()
//   {
//       _agent        = GetComponent<NavMeshAgent>();
//       _animator     = GetComponentInChildren<Animator>();
//       _playerEntity = GetComponent<PlayerEntity>();
//       _inventory    = GetComponent<NetworkInventory>();
//       _equipment    = GetComponent<NetworkEquipment>(); // ADICIONAR
//   }
//
// No ServerInitialize(), APÓS a linha "_inventory?.ServerLoadGemLoadout(...)":
//
//   _equipment?.ServerLoadFromDatabase(charData.CharacterId); // ADICIONAR
//
// No ServerSaveCharacterForced(), APÓS "_inventory?.ServerSaveAll(...)":
//
//   _equipment?.ServerSaveAll(_serverCharData.CharacterId, _serverAccountUsername); // ADICIONAR
//
// ═══════════════════════════════════════════════════════════════════════════
// MODIFICAÇÃO 3 — Adicionar RpcShowMessage (usado pelo NetworkEquipment)
// ═══════════════════════════════════════════════════════════════════════════
//
// Adicione no NetworkPlayer.cs (já existe padrão similar com RpcSkillRejected):
//
//   [ClientRpc]
//   public void RpcShowMessage(string message)
//   {
//       if (!isLocalPlayer) return;
//       UIManager.Instance?.ShowMessage(message);
//   }
//
// ═══════════════════════════════════════════════════════════════════════════
// MODIFICAÇÃO 4 — Iniciar EquipmentBonuses na inicialização do servidor
// ═══════════════════════════════════════════════════════════════════════════
//
// No ServerInitialize(), após carregar o equipment loadout, recalcule os stats:
//
//   _equipment?.ServerLoadFromDatabase(charData.CharacterId);
//   // Reconstrói bônus de equipamento ANTES de calcular stats iniciais
//   if (_equipment != null)
//   {
//       _serverCharData.EquipmentBonuses = _equipment.BuildEquipmentBonuses();
//       _serverStats = _serverCharData.GetDerivedStats();
//       // Atualiza MaxHP/MaxMP com os bônus de equipamento
//       float maxHP = Mathf.Min(_serverStats.MaxHP, MAX_HP_CAP);
//       float maxMP = Mathf.Min(_serverStats.MaxMP, MAX_MP_CAP);
//       MaxHP = maxHP; MaxMP = maxMP;
//       CurrentHP = Mathf.Min(CurrentHP, maxHP);
//       CurrentMP = Mathf.Min(CurrentMP, maxMP);
//   }
//
// ═══════════════════════════════════════════════════════════════════════════
// RESUMO DAS ALTERAÇÕES (checklist)
// ═══════════════════════════════════════════════════════════════════════════
//
//  [ ] _serverCharData: private → internal
//  [ ] _serverStats:    private → internal
//  [ ] _agent:          private → internal (ou já é GetComponent no Awake)
//  [ ] MAX_HP_CAP:      private → public const
//  [ ] MAX_MP_CAP:      private → public const
//  [ ] Awake(): adicionar _equipment = GetComponent<NetworkEquipment>()
//  [ ] ServerInitialize(): adicionar _equipment?.ServerLoadFromDatabase(...)
//  [ ] ServerInitialize(): recalcular stats com bônus de equipamento
//  [ ] ServerSaveCharacterForced(): adicionar _equipment?.ServerSaveAll(...)
//  [ ] Adicionar RpcShowMessage(string)
//
// Todas as mudanças são backwards-compatible — nada é removido.

namespace RPG.Network
{
    // Classe placeholder para evitar erros de compilação neste arquivo.
    internal static class NetworkPlayerEquipmentPatch { }
}
