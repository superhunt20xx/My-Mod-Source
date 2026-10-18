using UnityEngine;

namespace Superhunt.ChairMayhem
{
    /// <summary>
    /// Generic chase behavior used by every NPC hit with a chair. It uses the
    /// game's own Navigator/pathfinding, opens doors and breaks windows.
    /// </summary>
    internal sealed class ChairChaseState : NpcState
    {
        private readonly ChairAggro _aggro;
        private PlayerManager _target;
        private float _retargetIn;
        private float _windowScanIn;

        internal ChairChaseState(NPC npc, ChairAggro aggro, PlayerManager target)
            : base(npc)
        {
            _aggro = aggro;
            _target = target;
        }

        internal void SetTarget(PlayerManager target)
        {
            if (target != null)
            {
                _target = target;
            }
        }

        internal void Retarget()
        {
            _retargetIn = 0f;
            _windowScanIn = 0f;

            if (npc.Navigator != null)
            {
                npc.Navigator.SetSpeed(Mathf.Max(1f, Plugin.ChaseSpeed.Value));
            }
        }

        public override void Enter()
        {
            base.Enter();

            ChangeNavigationState(new NavigationState_DoNothing(npc, 0));

            if (npc.Navigator != null)
            {
                npc.Navigator.SetSpeed(Mathf.Max(1f, Plugin.ChaseSpeed.Value));
            }

            if (_target != null)
            {
                npc.Navigator.FindPath(_target.transform.position);
            }

            _retargetIn = 0.15f;
            _windowScanIn = 0.5f;
        }

        public override void Update()
        {
            base.Update();

            if (_aggro == null || _aggro.IsStunned)
            {
                return;
            }

            if (_target == null || _target.ec == null)
            {
                _target = FindNearestPlayer();
                if (_target == null)
                {
                    return;
                }
            }

            if (npc.Navigator == null)
            {
                return;
            }

            _retargetIn -= Time.deltaTime;
            if (_retargetIn <= 0f)
            {
                npc.Navigator.SetSpeed(Mathf.Max(1f, Plugin.ChaseSpeed.Value));
                npc.Navigator.FindPath(_target.transform.position);
                _retargetIn = 0.25f;
            }

            _windowScanIn -= Time.deltaTime;
            if (_windowScanIn <= 0f)
            {
                BreakNearbyWindows();
                _windowScanIn = 0.6f;
            }

            CheckCatch();
        }

        private PlayerManager FindNearestPlayer()
        {
            CoreGameManager core = Singleton<CoreGameManager>.Instance;
            if (core == null)
            {
                return null;
            }

            PlayerManager nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < core.TotalPlayers; i++)
            {
                PlayerManager player = core.GetPlayer(i);
                if (player == null || player.ec == null || player.invincible)
                {
                    continue;
                }

                float distance = (player.transform.position - npc.transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = player;
                }
            }

            return nearest;
        }

        private void CheckCatch()
        {
            if (_target == null || _target.invincible)
            {
                return;
            }

            Vector3 delta = _target.transform.position - npc.transform.position;
            float distance = delta.magnitude;

            if (distance > 8f)
            {
                return;
            }

            bool sighted = true;
            if (npc.looker != null)
            {
                npc.looker.Raycast(_target.transform, distance, out sighted);
            }

            if (sighted)
            {
                ChairDamage.Jumpscare(_target, npc);
            }
        }

        public override void OnStateTriggerStay(Entity otherEntity, Collider other, bool validCollision)
        {
            base.OnStateTriggerStay(otherEntity, other, validCollision);

            if (!validCollision || other == null || !other.CompareTag("Player"))
            {
                return;
            }

            PlayerManager player = other.GetComponent<PlayerManager>();
            if (player != null && !player.invincible)
            {
                ChairDamage.Jumpscare(player, npc);
            }
        }

        public override void DoorHit(StandardDoor door)
        {
            if (door == null)
            {
                return;
            }

            if (door.locked)
            {
                door.Unlock();
            }

            door.OpenTimed(3f, false);
            Retarget();
        }

        public override void WindowHit(Window window)
        {
            if (window == null)
            {
                return;
            }

            if (Plugin.BreakWindows.Value)
            {
                window.Break(true);
            }

            Retarget();
        }

        private void BreakNearbyWindows()
        {
            if (!Plugin.BreakWindows.Value || _target == null)
            {
                return;
            }

            Vector3 toPlayer = _target.transform.position - npc.transform.position;
            if (toPlayer.sqrMagnitude < 1f)
            {
                return;
            }

            toPlayer.Normalize();
            Window[] windows = Object.FindObjectsOfType<Window>();

            for (int i = 0; i < windows.Length; i++)
            {
                Window window = windows[i];
                if (window == null || window.open)
                {
                    continue;
                }

                Vector3 toWindow = window.transform.position - npc.transform.position;
                if (toWindow.sqrMagnitude > 225f)
                {
                    continue;
                }

                if (Vector3.Dot(toWindow.normalized, toPlayer) > 0.15f)
                {
                    window.Break(true);
                }
            }
        }
    }
}
