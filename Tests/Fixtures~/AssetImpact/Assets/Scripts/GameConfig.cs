// AssetImpact fixture: ScriptableObject that references other assets.
using UnityEngine;

namespace Fixture
{
    [CreateAssetMenu]
    public class GameConfig : ScriptableObject
    {
        public int startingLives = 3;
        public GameConfig parent;
        public Health playerPrefab;
    }
}
