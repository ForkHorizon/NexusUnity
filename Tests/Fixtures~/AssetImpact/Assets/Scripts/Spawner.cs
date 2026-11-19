// AssetImpact fixture: Spawner component whose methods are UnityEvent targets.
using UnityEngine;

namespace Fixture
{
    public class Spawner : MonoBehaviour
    {
        public GameObject prefab;
        public int count = 1;

        public void Spawn() { }
        public void SpawnMany(int amount) { }
    }
}
