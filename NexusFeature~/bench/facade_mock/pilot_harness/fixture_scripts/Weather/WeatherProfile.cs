using UnityEngine;

namespace Saltmarsh.Weather
{
    [CreateAssetMenu(menuName = "Saltmarsh/Weather Profile")]
    public class WeatherProfile : ScriptableObject
    {
        public float fogDensity = 0.02f;
        public float lightBudget = 1f;
        public Color ambient = new Color(0.55f, 0.6f, 0.7f);
    }
}
