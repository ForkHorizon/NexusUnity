using UnityEngine;

namespace Saltmarsh.Quests
{
    // Shows the current harbour notices on a board in the world.
    public class QuestBoard : MonoBehaviour
    {
        public GameObject noticeTemplate;
        public Transform noticeRoot;
        [Tooltip("Optional")] public Sprite headerIcon;
        public int maxNotices = 6;

        void Start()
        {
            for (int i = 0; i < maxNotices; i++)
            {
                var notice = Instantiate(noticeTemplate, noticeRoot);
                notice.name = "Notice_" + i;
            }
        }
    }
}
