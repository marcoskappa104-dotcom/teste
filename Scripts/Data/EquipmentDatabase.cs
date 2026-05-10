using UnityEngine;
using System.Collections.Generic;

namespace RPG.Data
{
    /// <summary>
    /// EquipmentDatabase — Registry singleton de todos os EquipmentData do jogo.
    ///
    /// Funciona igual ao ItemDatabase, mas para dados de equipamento.
    /// O servidor usa GetEquipment(itemId) para obter os bônus ao equipar/desequipar.
    /// O cliente usa para exibir informações na EquipmentUI e tooltips.
    ///
    /// SETUP:
    ///   1. Adicione este componente no mesmo GameObject do ItemDatabase.
    ///   2. Arraste TODOS os EquipmentData ScriptableObjects para a lista 'allEquipments'.
    ///   3. O ItemId do EquipmentData DEVE ser igual ao ItemId do ItemData correspondente.
    /// </summary>
    public class EquipmentDatabase : MonoBehaviour
    {
        public static EquipmentDatabase Instance { get; private set; }

        [Header("Registre TODOS os EquipmentData do jogo aqui")]
        [SerializeField] private List<EquipmentData> allEquipments = new List<EquipmentData>();

        private readonly Dictionary<string, EquipmentData> _lookup = new Dictionary<string, EquipmentData>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildLookup();
        }

        private void BuildLookup()
        {
            _lookup.Clear();
            foreach (var eq in allEquipments)
            {
                if (eq == null) continue;
                if (string.IsNullOrEmpty(eq.ItemId))
                {
                    Debug.LogError($"[EquipmentDatabase] EquipmentData '{eq.name}' tem ItemId vazio!");
                    continue;
                }
                if (_lookup.ContainsKey(eq.ItemId))
                {
                    Debug.LogError($"[EquipmentDatabase] ID duplicado: '{eq.ItemId}' em '{eq.name}'!");
                    continue;
                }
                _lookup[eq.ItemId] = eq;
            }
            Debug.Log($"[EquipmentDatabase] {_lookup.Count} equipamentos registrados.");
        }

        /// <summary>Retorna null se o equipamento não existir no database.</summary>
        public EquipmentData GetEquipment(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            _lookup.TryGetValue(itemId, out var eq);
            return eq;
        }

        public bool Contains(string itemId) => _lookup.ContainsKey(itemId);

        public List<EquipmentData> GetAllEquipments() => new List<EquipmentData>(allEquipments);
    }
}
