using UnityEngine;

namespace FartRaft
{
    // Floating can of beans. Touch it for a few seconds of MEGA FART power; it comes back after a while.
    public class BeansPickup : MonoBehaviour
    {
        public Transform visual;
        public float respawnTime = 10f;

        Collider trigger;
        float hiddenUntil;

        void Awake()
        {
            trigger = GetComponent<Collider>();
        }

        void Update()
        {
            bool visible = Time.time >= hiddenUntil;
            if (trigger != null) trigger.enabled = visible;
            if (visual == null) return;
            visual.gameObject.SetActive(visible);
            visual.localRotation = Quaternion.Euler(0f, Time.time * 120f, 0f);
            visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 3f) * 0.25f, 0f);
        }

        void OnTriggerEnter(Collider other)
        {
            var raft = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<Raft>() : null;
            if (raft == null || GameManager.Instance == null) return;
            if (!GameManager.Instance.OnBeans(raft, transform.position)) return;
            hiddenUntil = Time.time + respawnTime;
        }
    }
}
