using DilmerGames.Core.Singletons;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

using UnityEngine.XR.ARFoundation;

public class GameManager : Singleton<GameManager>
{
    [SerializeField]
    private GlobalGameSettings globalGameSettings;

    public GlobalGameSettings GlobalGameSettings
    {
        get
        {
            return globalGameSettings;
        }
    }
    
    void Awake() 
    {
        EnhancedTouchSupport.Enable();

        var origin = FindObjectOfType<ARSessionOrigin>();
        if (origin != null)
        {
            if (origin.GetComponent<ARRaycastManager>() == null)
            {
                origin.gameObject.AddComponent<ARRaycastManager>();
            }
            if (origin.GetComponent<ARPlaneManager>() == null)
            {
                origin.gameObject.AddComponent<ARPlaneManager>();
            }
        }
    }
}
