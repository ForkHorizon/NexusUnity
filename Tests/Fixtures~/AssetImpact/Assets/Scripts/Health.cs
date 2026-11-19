// AssetImpact fixture: Health component with value, UnityEvent and SerializeReference fields.
using UnityEngine;
using UnityEngine.Events;

namespace Fixture
{
    public interface IEffect { }

    [System.Serializable]
    public class BurnEffect : IEffect
    {
        public float dps = 1f;
    }

    public class Health : MonoBehaviour
    {
        public int maxHealth = 100;
        [SerializeField] private float speed = 3.5f;
        public string displayName = "Unit";
        public UnityEvent onDeath = new UnityEvent();
        [SerializeReference] public IEffect effect;

        public void Die() { }
        public float Speed => speed;
    }
}
