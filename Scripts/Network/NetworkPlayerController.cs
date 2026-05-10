using UnityEngine;
using UnityEngine.AI;
using Mirror;
using RPG.Character;
using RPG.UI;
using RPG.Combat;

namespace RPG.Network
{
    /// <summary>
    /// NetworkPlayerController v14
    ///
    /// CORREÇÕES v14:
    ///
    ///   CORREÇÃO — CmdMoveTo agora usa NetworkPlayer.CanSendMoveCmd() para throttle
    ///     global (inclusive em cliques livres, não só no chase).
    ///     Antes, cliques rápidos em sequência enviavam um comando por frame
    ///     (60+ cmd/s), causando sobrecarga de rede desnecessária.
    ///     Agora o throttle de CMD_MOVE_THROTTLE (0.05s = 20 cmd/s max) é aplicado
    ///     em TODOS os caminhos que enviam CmdMoveTo.
    ///
    ///   MELHORIA — TryMoveToGround valida NavMesh antes de enviar CmdMoveTo.
    ///     Se o destino do clique não tiver ponto válido no NavMesh dentro de 3m,
    ///     o movimento é ignorado silenciosamente (evita spam de comandos inválidos).
    ///
    ///   MELHORIA — HandleMouseInput tem guarda explícito contra Dead.
    ///     Quando o player está morto, cliques no mundo não enviam comandos.
    ///
    ///   Todas as correções v13 mantidas (IsTypingInField, anti-oclusão câmera,
    ///   remoção de RpcMoveConfirmed).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NetworkPlayerController : NetworkBehaviour
    {
        [Header("Layers — configure no Inspector")]
        [SerializeField] private LayerMask terrainLayer;
        [SerializeField] private LayerMask targetableLayer;
        [SerializeField] private LayerMask itemLayer;
        [Tooltip("Layers que bloqueiam a câmera (terreno + paredes). Normalmente = terrainLayer.")]
        [SerializeField] private LayerMask cameraOcclusionLayer;

        [Header("Câmera")]
        [SerializeField] private float orbitSensitivity = 3f;
        [SerializeField] private float zoomSensitivity  = 5f;
        [SerializeField] private float cameraSmoothTime = 0.05f;
        [SerializeField] private float cameraHeight     = 1.5f;

        [Header("Indicador de Movimento")]
        [SerializeField] private GameObject moveIndicatorPrefab;

        [Header("Debug")]
        [SerializeField] private bool debugMovement = false;

        // ── Componentes ────────────────────────────────────────────────────
        private NavMeshAgent       _agent;
        private PlayerEntity       _playerEntity;
        private SkillSystem        _skillSystem;
        private BasicAttackSystem  _basicAttack;
        private NetworkPlayer      _networkPlayer;
        private Camera             _cam;

        // ── Câmera ─────────────────────────────────────────────────────────
        private float   _yaw         = 45f;
        private float   _pitch       = 45f;
        private float   _distance    = 12f;
        private bool    _orbiting;
        private Vector3 _camVelocity = Vector3.zero;

        private const float PITCH_MIN      = 10f;
        private const float PITCH_MAX      = 80f;
        private const float DIST_MIN       = 3f;
        private const float DIST_MAX       = 30f;
        private const float MAX_MOVE_DIST  = 120f;
        private const float CAM_SKIN_WIDTH = 0.3f;

        private float _lastSecurityWarnTime = -999f;
        private const float SECURITY_WARN_INTERVAL = 2f;

        // ── Awake ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _agent       = GetComponent<NavMeshAgent>();
            _basicAttack = GetComponent<BasicAttackSystem>();
        }

        private void OnEnable()
        {
            Cursor.visible   = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void OnDisable()
        {
            _orbiting        = false;
            Cursor.visible   = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // ── OnStartLocalPlayer ─────────────────────────────────────────────

        public override void OnStartLocalPlayer()
        {
            _playerEntity  = GetComponent<PlayerEntity>();
            _skillSystem   = GetComponent<SkillSystem>();
            _basicAttack   = GetComponent<BasicAttackSystem>();
            _networkPlayer = GetComponent<NetworkPlayer>();
            _cam           = Camera.main;

            if (_cam == null)
                Debug.LogWarning("[NetworkPlayerController] Camera.main não encontrada!");

            if (_agent != null && _playerEntity != null && _playerEntity.Stats != null)
                _agent.speed = Mathf.Clamp(_playerEntity.Stats.MoveSpeed, 3f, 7f);

            Cursor.visible   = true;
            Cursor.lockState = CursorLockMode.None;

            if (terrainLayer == 0)
                Debug.LogWarning("[NetworkPlayerController] terrainLayer não configurado!");
            if (targetableLayer == 0)
                Debug.LogWarning("[NetworkPlayerController] targetableLayer não configurado!");

            UIManager.Instance?.BindLocalPlayer(_playerEntity);
            Debug.Log("[NetworkPlayerController] Controlador local iniciado.");
        }

        // ── Update / LateUpdate ────────────────────────────────────────────

        private void Update()
        {
            if (!isLocalPlayer) return;
            HandleMouseInput();
            HandleSkillInput();
            HandleCameraOrbit();
            HandleUIInput();
        }

        private void LateUpdate()
        {
            if (!isLocalPlayer) return;
            UpdateCameraPosition();
        }

        // ── Mouse Input ────────────────────────────────────────────────────

        private void HandleMouseInput()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (_cam == null) return;

            // CORREÇÃO v14: não processa input se player está morto
            if (_playerEntity != null && _playerEntity.IsDead) return;

            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

            if (TryPickupItem(ray))         return;
            if (TryHandleMonsterClick(ray)) return;
            if (TrySelectTargetable(ray))   return;
            TryMoveToGround(ray);
        }

        private bool TryHandleMonsterClick(Ray ray)
        {
            if (targetableLayer == 0) return false;
            if (!Physics.Raycast(ray, out RaycastHit hit, 300f, targetableLayer)) return false;

            var monster = hit.collider.GetComponentInParent<NetworkMonsterEntity>();
            if ((UnityEngine.Object)monster == null) return false;
            if (monster.IsDead) return false;

            bool targetChanged = _playerEntity != null &&
                                 _playerEntity.CurrentTarget != (ITargetable)monster;

            if (targetChanged && _basicAttack != null && _basicAttack.IsAutoAttacking)
                _basicAttack.CancelAutoAttack();

            _skillSystem?.CancelPendingWalk();
            _playerEntity?.SetTarget(monster);
            UIManager.Instance?.UpdateTargetPanel(monster);
            _basicAttack?.TryRegisterClick(monster);

            return true;
        }

        private bool TryPickupItem(Ray ray)
        {
            if (itemLayer == 0) return false;
            if (!Physics.Raycast(ray, out RaycastHit hit, 300f, itemLayer)) return false;

            var worldItem = hit.collider.GetComponentInParent<WorldItem>();
            if (worldItem == null) return false;

            uint myNetId = GetComponent<NetworkIdentity>().netId;
            worldItem.CmdPickUp(myNetId);
            return true;
        }

        private bool TrySelectTargetable(Ray ray)
        {
            if (targetableLayer == 0) return false;
            if (!Physics.Raycast(ray, out RaycastHit hit, 300f, targetableLayer)) return false;

            var targetable = hit.collider.GetComponentInParent<ITargetable>();
            if (targetable == null || targetable.IsDead) return false;

            _skillSystem?.CancelPendingWalk();
            _basicAttack?.CancelAutoAttack();
            _playerEntity?.SetTarget(targetable);
            UIManager.Instance?.UpdateTargetPanel(targetable);
            return true;
        }

        private void TryMoveToGround(Ray ray)
        {
            int moveLayerMask = terrainLayer != 0
                ? (int)terrainLayer
                : ~(1 << LayerMask.NameToLayer("Targetable"));

            if (!Physics.Raycast(ray, out RaycastHit hit, 300f, moveLayerMask)) return;

            _skillSystem?.CancelPendingWalk();
            _basicAttack?.CancelAutoAttack();
            _playerEntity?.ClearTarget();
            UIManager.Instance?.ClearTargetPanel();

            Vector3 dest = hit.point;

            // CORREÇÃO v14: valida NavMesh antes de qualquer coisa
            if (!NavMesh.SamplePosition(dest, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
            {
                if (debugMovement)
                    Debug.LogWarning("[NetworkPlayerController] TryMoveToGround: ponto fora do NavMesh, ignorado.");
                return;
            }

            dest = navHit.position;

            // Aplica localmente (client prediction)
            if (_agent != null && _agent.isOnNavMesh)
                _agent.SetDestination(dest);

            // CORREÇÃO v14: throttle via NetworkPlayer.CanSendMoveCmd()
            if (_networkPlayer == null || _networkPlayer.CanSendMoveCmd())
                CmdMoveTo(dest);

            SpawnMoveIndicator(hit.point);
        }

        // ── Skills ─────────────────────────────────────────────────────────

        private void HandleSkillInput()
        {
            if (_skillSystem == null) return;
            if (_playerEntity != null && _playerEntity.IsDead) return;
            if (IsTypingInField()) return;

            if (Input.GetKeyDown(KeyCode.Q)) _skillSystem.TryUseSkill(0);
            if (Input.GetKeyDown(KeyCode.W)) _skillSystem.TryUseSkill(1);
            if (Input.GetKeyDown(KeyCode.E)) _skillSystem.TryUseSkill(2);
            if (Input.GetKeyDown(KeyCode.R)) _skillSystem.TryUseSkill(3);
            if (Input.GetKeyDown(KeyCode.C)) AttributeWindowUI.Instance?.Toggle();
        }

        private static bool IsTypingInField()
        {
            var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (selected == null) return false;
            return selected.GetComponent<TMPro.TMP_InputField>() != null
                || selected.GetComponent<UnityEngine.UI.InputField>() != null;
        }

        private void HandleUIInput()
        {
            if (IsTypingInField()) return;

            if (Input.GetKeyDown(KeyCode.I))
            {
                EnsureCursorVisible();
                InventoryUI.Instance?.Toggle();
            }
            if (Input.GetKeyDown(KeyCode.P))
            {
                EnsureCursorVisible();
                PowerGemUI.Instance?.Toggle();
            }
        }

        private void EnsureCursorVisible()
        {
            if (!_orbiting)
            {
                Cursor.visible   = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        // ── Câmera ─────────────────────────────────────────────────────────

        private void HandleCameraOrbit()
        {
            if (Input.GetMouseButtonDown(1))
            {
                _orbiting = true;
                Cursor.visible   = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            if (Input.GetMouseButtonUp(1))
            {
                _orbiting        = false;
                Cursor.visible   = true;
                Cursor.lockState = CursorLockMode.None;
            }

            if (_orbiting)
            {
                _yaw   += Input.GetAxis("Mouse X") * orbitSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * orbitSensitivity;
                _pitch  = Mathf.Clamp(_pitch, PITCH_MIN, PITCH_MAX);

                if (_yaw > 360f)  _yaw -= 360f;
                if (_yaw < -360f) _yaw += 360f;
            }

            _distance -= Input.GetAxis("Mouse ScrollWheel") * zoomSensitivity;
            _distance  = Mathf.Clamp(_distance, DIST_MIN, DIST_MAX);
        }

        private void UpdateCameraPosition()
        {
            if (_cam == null) return;

            Quaternion rot   = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3    pivot = transform.position + Vector3.up * cameraHeight;
            Vector3    dir   = rot * new Vector3(0f, 0f, -1f);

            float effectiveDistance = _distance;
            int   occlusionMask = cameraOcclusionLayer != 0 ? (int)cameraOcclusionLayer : (int)terrainLayer;
            if (occlusionMask != 0 &&
                Physics.SphereCast(pivot, CAM_SKIN_WIDTH, dir, out RaycastHit camHit,
                                   _distance, occlusionMask))
            {
                effectiveDistance = Mathf.Max(DIST_MIN, camHit.distance - CAM_SKIN_WIDTH);
            }

            Vector3 target = pivot + dir * effectiveDistance;

            if (target.y < transform.position.y + 0.5f)
                target.y = transform.position.y + 0.5f;

            _cam.transform.position = Vector3.SmoothDamp(
                _cam.transform.position, target, ref _camVelocity, cameraSmoothTime);
            _cam.transform.LookAt(pivot);
        }

        // ── Commands ───────────────────────────────────────────────────────

        /// <summary>
        /// Envia destino de movimento ao servidor.
        /// O servidor valida distância máxima e aplica ao NavMeshAgent autoritativo.
        /// O cliente já aplicou SetDestination localmente (client prediction) —
        /// não enviamos RpcMoveConfirmed de volta para evitar double SetDestination.
        /// </summary>
        [Command]
        public void CmdMoveTo(Vector3 destination)
        {
            var netPlayer = GetComponent<NetworkPlayer>();
            if (netPlayer == null) return;
            if (netPlayer.Dead) return;

            float dist = Vector3.Distance(transform.position, destination);
            if (dist > MAX_MOVE_DIST)
            {
                if (Time.time - _lastSecurityWarnTime >= SECURITY_WARN_INTERVAL)
                {
                    _lastSecurityWarnTime = Time.time;
                    Debug.LogWarning($"[Server] CmdMoveTo suspeito: dist={dist:0.0} para {netPlayer.CharacterName}");
                }
                return;
            }

            if (_agent == null) return;

            Vector3 finalDest = destination;
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 3f, NavMesh.AllAreas) ||
                NavMesh.SamplePosition(destination, out hit, 6f, NavMesh.AllAreas))
            {
                finalDest = hit.position;
            }
            else if (debugMovement)
            {
                Debug.LogWarning($"[Server] CmdMoveTo: destino fora do NavMesh para {netPlayer.CharacterName}");
                return; // CORREÇÃO v14: não aplica movimento inválido no servidor
            }

            _agent.SetDestination(finalDest);
        }

        // ── API pública ────────────────────────────────────────────────────

        public void SetEnabled(bool value)
        {
            enabled = value;
            if (!value)
            {
                _basicAttack?.CancelAutoAttack();
                _skillSystem?.CancelPendingWalk();

                _orbiting        = false;
                Cursor.visible   = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private void SpawnMoveIndicator(Vector3 pos)
        {
            if (moveIndicatorPrefab == null) return;
            var go = Instantiate(moveIndicatorPrefab, pos + Vector3.up * 0.02f, Quaternion.Euler(90f, 0f, 0f));
            Destroy(go, 0.8f);
        }
    }
}
