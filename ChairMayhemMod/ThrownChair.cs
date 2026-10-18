using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Superhunt.ChairMayhem
{
    /// <summary>
    /// The physical chair that is thrown. After it hits something it falls to
    /// the ground and becomes a pickup again so the same chair can be reused.
    /// </summary>
    public sealed class ThrownChair : MonoBehaviour
    {
        private PlayerManager _owner;
        private Rigidbody _rigidbody;
        private BoxCollider _collider;
        private bool _damageEnabled = true;
        private bool _impacted;
        private bool _pickupArmed;

        public static ThrownChair Spawn(GameObject template, PlayerManager owner)
        {
            if (template == null || owner == null || owner.ec == null)
            {
                return null;
            }

            Vector3 forward = owner.transform.forward;
            Vector3 position = owner.transform.position + forward * 2.5f + Vector3.up * 2f;

            GameObject clone = Object.Instantiate(
                template,
                position,
                Quaternion.LookRotation(forward, Vector3.up));

            clone.name = "ChairMayhem_ThrownChair";
            clone.layer = 0;
            clone.SetActive(false);

            foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Object.Destroy(collider);
            }

            foreach (NavMeshObstacle obstacle in clone.GetComponentsInChildren<NavMeshObstacle>(true))
            {
                obstacle.enabled = false;
                Object.Destroy(obstacle);
            }

            foreach (Rigidbody body in clone.GetComponentsInChildren<Rigidbody>(true))
            {
                Object.Destroy(body);
            }

            foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Object.Destroy(behaviour);
            }

            clone.SetActive(true);

            Bounds bounds = CalculateBounds(clone);
            Vector3 worldSize = bounds.size;
            if (worldSize.sqrMagnitude < 1f)
            {
                worldSize = new Vector3(5f, 5f, 5f);
            }

            Rigidbody rigidbody = clone.AddComponent<Rigidbody>();
            rigidbody.mass = 4f;
            rigidbody.drag = 0f;
            rigidbody.angularDrag = 0.4f;
            rigidbody.useGravity = true;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            BoxCollider box = clone.AddComponent<BoxCollider>();
            Vector3 scale = clone.transform.lossyScale;
            box.center = clone.transform.InverseTransformPoint(bounds.center);
            box.size = new Vector3(
                worldSize.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                worldSize.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
                worldSize.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
            box.isTrigger = false;

            ThrownChair thrown = clone.AddComponent<ThrownChair>();
            thrown._owner = owner;
            thrown._rigidbody = rigidbody;
            thrown._collider = box;

            rigidbody.velocity = forward * 40f + Vector3.up * 5f;
            rigidbody.angularVelocity = Random.insideUnitSphere * 9f;

            thrown.StartCoroutine(thrown.ArmPickupAfterDelay(1.1f));
            return thrown;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return bounds;
        }

        private void Update()
        {
            if (transform.position.y < -80f)
            {
                Object.Destroy(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleHit(collision.collider);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleHit(other);
        }

        private void HandleHit(Collider other)
        {
            if (!_damageEnabled || other == null)
            {
                return;
            }

            if (other.GetComponentInParent<PlayerManager>() != null)
            {
                return;
            }

            NPC npc = other.GetComponentInParent<NPC>();
            if (npc != null)
            {
                ChairDamage.Hit(npc, _owner);
                Impact();
                return;
            }

            Window window = other.GetComponentInParent<Window>();
            if (window != null)
            {
                window.Break(true);
                Impact();
                return;
            }

            if (!other.isTrigger)
            {
                Impact();
            }
        }

        private void Impact()
        {
            if (_impacted)
            {
                return;
            }

            _impacted = true;
            _damageEnabled = false;
            StartCoroutine(ArmPickupAfterDelay(0.7f));
        }

        private IEnumerator ArmPickupAfterDelay(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (_pickupArmed || this == null || gameObject == null)
            {
                yield break;
            }

            _pickupArmed = true;

            // Keep the physical collider out of the interaction raycast so the
            // clickable trigger below is the collider the player actually hits.
            gameObject.layer = 2;

            GameObject marker = new GameObject("ChairMayhem_Pickup");
            marker.layer = 0;
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = _collider != null ? _collider.center : Vector3.zero;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one;

            BoxCollider trigger = marker.AddComponent<BoxCollider>();
            trigger.center = Vector3.zero;
            trigger.size = _collider != null ? _collider.size * 1.12f : new Vector3(8f, 8f, 8f);
            trigger.isTrigger = true;

            ChairPickup pickup = marker.AddComponent<ChairPickup>();
            pickup.Initialize(gameObject, Plugin.ChairItemObject);
        }
    }
}
