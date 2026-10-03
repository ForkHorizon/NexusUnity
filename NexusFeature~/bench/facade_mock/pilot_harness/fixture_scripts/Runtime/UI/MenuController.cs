using UnityEngine;

namespace Saltmarsh.UI
{
    public class MenuController : MonoBehaviour
    {
        public static string LastAction;

        public void OnPlayPressed()
        {
            LastAction = "play";
            Debug.Log("[Menu] Play pressed");
        }

        public void OnSettingsPressed()
        {
            LastAction = "settings";
            Debug.Log("[Menu] Settings pressed");
        }

        public void OnQuitPressed()
        {
            LastAction = "quit";
            Debug.Log("[Menu] Quit pressed");
        }
    }
}
