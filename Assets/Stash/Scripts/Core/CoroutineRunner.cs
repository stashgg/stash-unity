using UnityEngine;

namespace Stash.Core
{
    /// <summary>
    /// Singleton MonoBehaviour for running coroutines from non-MonoBehaviour classes
    /// </summary>
    public class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner instance;

        public static CoroutineRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("StashCoroutineRunner");
                    instance = go.AddComponent<CoroutineRunner>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }
    }
}

