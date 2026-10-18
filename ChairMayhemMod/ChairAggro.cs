using UnityEngine;

namespace Superhunt.ChairMayhem
{
    /// <summary>
    /// Tracks chair damage on one NPC: random-colour glitch on every hit,
    /// 3-second stun, chase behavior, and disappearance after enough hits.
    /// The character's corruption lives on after the body is gone.
    /// </summary>
    public sealed class ChairAggro : MonoBehaviour
    {
        private NPC _npc;
        private ChairChaseState _chase;
        private Color[] _baseColors;
        private int _hits;
        private bool _corrupted;
        private bool _stunned;
        private float _stunUntil;
        private float _glitchUntil;
        private float _nextGlitchVisual;
        private Vector3 _spriteBasePosition;
        private Vector3 _spriteBaseScale;
        private bool _hasSpriteBase;

        internal bool IsStunned => _stunned;

        private void Awake()
        {
            _npc = GetComponent<NPC>();
            CacheBaseColors();
            CacheSpriteBase();
        }

        private void CacheBaseColors()
        {
            if (_npc == null || _npc.spriteRenderer == null)
            {
                _baseColors = new Color[0];
                return;
            }

            _baseColors = new Color[_npc.spriteRenderer.Length];
            for (int i = 0; i < _baseColors.Length; i++)
            {
                _baseColors[i] = _npc.spriteRenderer[i] != null
                    ? _npc.spriteRenderer[i].color
                    : Color.white;
            }
        }

        private void CacheSpriteBase()
        {
            if (_npc == null || _npc.spriteBase == null)
            {
                _hasSpriteBase = false;
                return;
            }

            _hasSpriteBase = true;
            _spriteBasePosition = _npc.spriteBase.transform.localPosition;
            _spriteBaseScale = _npc.spriteBase.transform.localScale;
        }

        internal void RegisterHit(PlayerManager player)
        {
            if (_corrupted || _npc == null || player == null)
            {
                return;
            }

            _hits++;
            StartGlitch();

            int hitsToCorrupt = Mathf.Max(1, Plugin.HitsToKill.Value);
            if (_hits >= hitsToCorrupt)
            {
                Corrupt();
                return;
            }

            Stun();

            if (_chase == null)
            {
                _chase = new ChairChaseState(_npc, this, player);
                _npc.behaviorStateMachine.ChangeState(_chase);
            }
            else
            {
                _chase.SetTarget(player);

                if (_npc.behaviorStateMachine.CurrentState != _chase)
                {
                    _npc.behaviorStateMachine.ChangeState(_chase);
                }
            }

            Plugin.Log.LogInfo(
                $"{_npc.Character} was hit by a chair ({_hits}/{hitsToCorrupt}); " +
                "it is now chasing the player.");
        }

        private void StartGlitch()
        {
            float duration = Mathf.Max(0.1f, Plugin.GlitchSeconds.Value);
            _glitchUntil = Time.time + duration;
            _nextGlitchVisual = 0f;
            CacheSpriteBase();
        }

        private void Stun()
        {
            _stunUntil = Time.time + Mathf.Max(0.1f, Plugin.StunSeconds.Value);

            if (_stunned)
            {
                return;
            }

            _stunned = true;

            if (_npc.Entity != null)
            {
                _npc.Entity.SetFrozen(true);
            }

            if (_npc.Navigator != null)
            {
                _npc.Navigator.SetSpeed(0f);
                _npc.Navigator.ClearDestination();
            }
        }

        private void Update()
        {
            if (_corrupted || !_stunned || Time.time < _stunUntil)
            {
                return;
            }

            _stunned = false;

            if (_npc == null)
            {
                return;
            }

            if (_npc.Entity != null)
            {
                _npc.Entity.SetFrozen(false);
            }

            if (_npc.Navigator != null)
            {
                _npc.Navigator.SetSpeed(Mathf.Max(1f, Plugin.ChaseSpeed.Value));
            }

            _chase?.Retarget();
        }

        private void LateUpdate()
        {
            if (_hits <= 0 || _npc == null)
            {
                return;
            }

            if (Time.time < _glitchUntil)
            {
                AnimateGlitch();
                return;
            }

            if (_hasSpriteBase && _npc.spriteBase != null)
            {
                _npc.spriteBase.transform.localPosition = _spriteBasePosition;
                _npc.spriteBase.transform.localScale = _spriteBaseScale;
            }

            ApplyTint();

            if (!_corrupted && _chase != null && !_stunned &&
                _npc.behaviorStateMachine.CurrentState != _chase)
            {
                // Some NPC states try to take control back. Keep the
                // chair-induced chase in charge until corruption takes over.
                _npc.behaviorStateMachine.ChangeState(_chase);
            }
        }

        private void AnimateGlitch()
        {
            if (Time.time < _nextGlitchVisual || _npc.spriteRenderer == null)
            {
                return;
            }

            _nextGlitchVisual = Time.time + 0.035f;

            for (int i = 0; i < _npc.spriteRenderer.Length; i++)
            {
                SpriteRenderer renderer = _npc.spriteRenderer[i];
                if (renderer == null)
                {
                    continue;
                }

                Color random = Color.HSVToRGB(
                    UnityEngine.Random.value,
                    UnityEngine.Random.Range(0.7f, 1f),
                    UnityEngine.Random.Range(0.75f, 1f));
                random.a = i < _baseColors.Length ? _baseColors[i].a : 1f;
                renderer.color = random;
            }

            if (_hasSpriteBase && _npc.spriteBase != null)
            {
                _npc.spriteBase.transform.localScale =
                    _spriteBaseScale * UnityEngine.Random.Range(0.92f, 1.08f);
                _npc.spriteBase.transform.localPosition =
                    _spriteBasePosition + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.12f);
            }
        }

        private void ApplyTint()
        {
            if (_npc == null || _npc.spriteRenderer == null)
            {
                return;
            }

            int hitsToCorrupt = Mathf.Max(1, Plugin.HitsToKill.Value);
            float amount = _corrupted ? 1f : Mathf.Clamp01((float)_hits / hitsToCorrupt);
            Color red = new Color(1f, 0.08f, 0.08f, 1f);

            for (int i = 0; i < _npc.spriteRenderer.Length; i++)
            {
                SpriteRenderer renderer = _npc.spriteRenderer[i];
                if (renderer == null)
                {
                    continue;
                }

                Color baseColor = i < _baseColors.Length ? _baseColors[i] : Color.white;
                Color target = new Color(red.r, red.g, red.b, baseColor.a);
                renderer.color = Color.Lerp(baseColor, target, amount);
            }
        }

        private void Corrupt()
        {
            if (_corrupted)
            {
                return;
            }

            _corrupted = true;

            Character character = _npc.Character;

            // Start the harassment timers first, while the PlayTime source is
            // still alive so its jump-rope template can be captured.
            Plugin.CorruptCharacter(character, _npc);

            Plugin.Log.LogInfo(
                $"{character} vanished after {_hits} chair hits; " +
                "its corruption will keep interfering with the player.");

            _npc.Despawn();
        }
    }
}
