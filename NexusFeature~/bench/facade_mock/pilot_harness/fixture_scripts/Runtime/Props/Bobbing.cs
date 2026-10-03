using UnityEngine;

namespace Saltmarsh.Props
{
    public class Bobbing : MonoBehaviour
    {
        public float amplitude = 0.15f;
        public float speed = 0.8f;
        Vector3 _origin;

        void Awake() => _origin = transform.localPosition;

        void Update()
        {
            transform.localPosition = _origin + Vector3.up * Mathf.Sin(Time.time * speed) * amplitude;
        }
    }
}
