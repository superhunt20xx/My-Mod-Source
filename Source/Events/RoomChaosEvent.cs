using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// RoomChao - the whole school starts running around screaming.
    ///
    /// Implemented as a plain RandomEvent subclass so it plugs into the vanilla
    /// event scheduler. <see cref="EventRegistration"/> creates one prefab instance
    /// and calls <see cref="Register"/>, the game clones it per activation.
    /// </summary>
    public sealed class RoomChaosEvent : RandomEvent
    {
        private const float Duration = 20f;
        private const float NoiseInterval = 1.5f;
        private const int NoiseValue = 80;

        private static RandomEvent prefab;

        private float noiseTimer;

        internal static void Register(RandomEvent built)
        {
            prefab = built;
            if (prefab != null)
            {
                prefab.name = "SH20XX_Event_RoomChao";
            }
        }

        internal static RandomEvent Prefab
        {
            get { return prefab; }
        }

        /// <summary>
        /// Called by the game right after the event prefab is spawned.
        /// </summary>
        public override void Initialize(EnvironmentController controller, System.Random rng)
        {
            base.Initialize(controller, rng);
            eventType = RandomEventType.Party;
        }

        public override void Begin()
        {
            base.Begin();
            active = true;
            noiseTimer = NoiseInterval;

            MakeChaosNoise();
        }

        public override void End()
        {
            active = false;
            base.End();
        }

        private void Update()
        {
            if (!active || ec == null)
            {
                return;
            }

            float delta = Time.deltaTime * ec.EnvironmentTimeScale;
            noiseTimer -= delta;
            remainingTime -= delta;

            if (noiseTimer <= 0f)
            {
                noiseTimer = NoiseInterval;
                MakeChaosNoise();
            }

            if (remainingTime <= 0f)
            {
                ec.EventOver(this);
            }
        }

        /// <summary>
        /// Pulls every character towards a random spot, which reads as a stampede.
        /// </summary>
        private void MakeChaosNoise()
        {
            if (ec == null)
            {
                return;
            }

            System.Collections.Generic.List<NPC> npcs = ec.Npcs;
            for (int i = 0; i < npcs.Count; i++)
            {
                NPC npc = npcs[i];
                if (npc == null || npc.Navigator == null)
                {
                    continue;
                }

                Cell target = ec.RandomCell(false, false, false);
                if (target == null)
                {
                    continue;
                }

                npc.Navigator.FindPath(target.CenterWorldPosition);
                npc.behaviorStateMachine.ChangeState(new NpcState(npc));
            }

            Vector3 origin = transform.position;
            ec.MakeNoise(origin, NoiseValue, false);
        }
    }
}
