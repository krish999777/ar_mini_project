using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARPlacementReticle : MonoBehaviour
{
    private const string RAYCAST_LAYER = "ARMeshLiDAR";

    public PlacedObjectState placedObject = new PlacedObjectState();

    public Vector3 offset = Vector3.zero;

    public UnityEvent OnObjectPlaced = new UnityEvent();

    private bool objectPlaced = false;

    public Camera mainCamera = null;

    private GameObject customReticle;

    private TextMeshProUGUI reticleOverlayText;

    private ARRaycastManager arRaycastManager;

    private static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

    private float searchTimer = 0f;

    private Camera GetCamera()
    {
        if (mainCamera != null) return mainCamera;

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            var origin = FindObjectOfType<ARSessionOrigin>();
            if (origin != null && origin.camera != null)
            {
                mainCamera = origin.camera;
            }
        }
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }
        return mainCamera;
    }

    private void EnsureReticle()
    {
        if (customReticle != null) return;

        var prefab = Resources.Load<GameObject>("Prefabs/Reticle");
        if (prefab != null)
        {
            customReticle = Instantiate(prefab);
            customReticle.transform.parent = transform;
            customReticle.transform.position = Vector3.zero;
            customReticle.SetActive(false);
            reticleOverlayText = customReticle.GetComponentInChildren<TextMeshProUGUI>();
        }
        else
        {
            customReticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            customReticle.name = "FallbackReticle";
            customReticle.transform.parent = transform;
            customReticle.transform.localScale = new Vector3(0.6f, 0.005f, 0.6f);
            var col = customReticle.GetComponent<Collider>();
            if (col != null) Destroy(col);
            customReticle.SetActive(false);
        }
    }

    void Awake()
    {
        EnsureReticle();
    }

    void Start()
    {
        GetCamera();

        arRaycastManager = FindObjectOfType<ARRaycastManager>();
        if (arRaycastManager == null)
        {
            var origin = FindObjectOfType<ARSessionOrigin>();
            if (origin != null)
            {
                arRaycastManager = origin.gameObject.AddComponent<ARRaycastManager>();
            }
        }
    }

    void Update()
    {
        if (placedObject.Placement != null || objectPlaced) return;

        Camera cam = GetCamera();
        if (cam == null)
        {
            Logger.Instance.LogInfo("Searching for Camera...");
            return;
        }

        EnsureReticle();

        bool hasHit = false;
        Vector3 hitPoint = Vector3.zero;
        Vector3 hitNormal = Vector3.up;

        if (arRaycastManager == null)
        {
            arRaycastManager = FindObjectOfType<ARRaycastManager>();
        }

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // 1. ARRaycastManager against detected planes (Floor / Surface)
        if (arRaycastManager != null && arRaycastManager.Raycast(screenCenter, s_Hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds))
        {
            hitPoint = s_Hits[0].pose.position;
            hitNormal = s_Hits[0].pose.up;
            hasHit = true;
        }

        // 2. Physics Raycast against LiDAR layer or floor colliders
        if (!hasHit)
        {
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            int lidarMask = LayerMask.GetMask(RAYCAST_LAYER);
            if (lidarMask != 0 && Physics.Raycast(ray, out var hit, 10, lidarMask))
            {
                hitPoint = hit.point;
                hitNormal = hit.normal;
                hasHit = true;
            }
        }

        // 3. Fallback: AR Raycast against any trackable (feature points, estimated planes)
        if (!hasHit && arRaycastManager != null && arRaycastManager.Raycast(screenCenter, s_Hits, TrackableType.AllTypes))
        {
            hitPoint = s_Hits[0].pose.position;
            hitNormal = s_Hits[0].pose.up;
            hasHit = true;
        }

        // 4. Fallback: Physics raycast against ANY scene collider (e.g. driving surface or room bounds)
        if (!hasHit)
        {
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out var hit, 10, ~LayerMask.GetMask("UI", "Ignore Raycast")))
            {
                hitPoint = hit.point;
                hitNormal = hit.normal;
                hasHit = true;
            }
        }

        // 5. Responsive Fallback: If device has not found a plane yet after 0.5s, project 1.5m in front of camera
        if (!hasHit)
        {
            searchTimer += Time.deltaTime;
            if (searchTimer > 0.5f)
            {
                Vector3 forwardFlat = cam.transform.forward;
                forwardFlat.y = 0;
                if (forwardFlat.sqrMagnitude < 0.01f) forwardFlat = Vector3.forward;
                forwardFlat.Normalize();

                hitPoint = cam.transform.position + forwardFlat * 1.5f + Vector3.down * 0.8f;
                hitNormal = Vector3.up;
                hasHit = true;
            }
            else
            {
                customReticle.SetActive(false);
                Logger.Instance.LogInfo("Aim at floor to detect surface...");
                return;
            }
        }

        if (hasHit)
        {
            customReticle.SetActive(true);
            customReticle.transform.position = hitPoint;
            customReticle.transform.up = hitNormal;

            var renderer = customReticle.GetComponentInChildren<MeshRenderer>();

            if (PlayerMissionManager.Instance != null && PlayerMissionManager.Instance.CarWasPlaced && CarController.Instance != null)
            {
                var distance = Vector3.Distance(CarController.Instance.transform.position, hitPoint);
                if (reticleOverlayText != null)
                {
                    reticleOverlayText.text = $"{string.Format("{0:0.#}", distance)}m";
                }

                if (distance >= placedObject.PlayerItem.MinDistance)
                {
                    if (renderer != null && GameManager.Instance != null && GameManager.Instance.GlobalGameSettings != null)
                        renderer.material = GameManager.Instance.GlobalGameSettings.AvailableReticleMaterial;

                    Logger.Instance.LogInfo("Tap to place Flag");
                    PlaceObject(hitPoint);
                }
                else
                {
                    if (renderer != null && GameManager.Instance != null && GameManager.Instance.GlobalGameSettings != null)
                        renderer.material = GameManager.Instance.GlobalGameSettings.UnavailableReticleMaterial;

                    Logger.Instance.LogInfo("Move further from car to place Flag");
                }
            }
            else
            {
                if (renderer != null && GameManager.Instance != null && GameManager.Instance.GlobalGameSettings != null)
                    renderer.material = GameManager.Instance.GlobalGameSettings.AvailableReticleMaterial;

                if (reticleOverlayText != null)
                {
                    reticleOverlayText.text = "TAP TO PLACE CAR";
                }

                Logger.Instance.LogInfo("Surface found! Tap screen to place Car.");
                PlaceObject(hitPoint);
            }
        }
    }

    void PlaceObject(Vector3 location)
    {
        bool isTouchBegan = false;
        Vector2 touchPos = Vector2.zero;

        // 1. EnhancedTouch
        if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
        {
            var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                isTouchBegan = true;
                touchPos = touch.finger.screenPosition;
            }
        }

        // 2. Touchscreen direct
        if (!isTouchBegan && UnityEngine.InputSystem.Touchscreen.current != null)
        {
            var primary = UnityEngine.InputSystem.Touchscreen.current.primaryTouch;
            if (primary.press.wasPressedThisFrame)
            {
                isTouchBegan = true;
                touchPos = primary.position.ReadValue();
            }
        }

        // 3. Pointer fallback (Mouse/Touch)
        if (!isTouchBegan && UnityEngine.InputSystem.Pointer.current != null)
        {
            if (UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame)
            {
                isTouchBegan = true;
                touchPos = UnityEngine.InputSystem.Pointer.current.position.ReadValue();
            }
        }

        if (isTouchBegan)
        {
            // check if we are over UI but ignore the logger label otherwise block it
            bool isOverUI = touchPos.IsPointOverUIObject(new string[] { "Logger" });
            if (isOverUI) return;

            if (placedObject != null && placedObject.Placement == null)
            {
                placedObject.Placement = Instantiate(placedObject.Prefab, location, Quaternion.identity);
                placedObject.Placement.transform.parent = transform;

                var placedObjectItem = placedObject.Placement.AddComponent<PlacedObjectItem>();
                placedObjectItem.PlayerItem = placedObject.PlayerItem;

                // Ensure an invisible ground driving surface exists so the car with gravity can drive without falling
                if (GameObject.Find("AR_DrivingSurface") == null)
                {
                    GameObject drivingSurface = new GameObject("AR_DrivingSurface");
                    drivingSurface.transform.position = new Vector3(location.x, location.y - 0.02f, location.z);
                    BoxCollider floorCol = drivingSurface.AddComponent<BoxCollider>();
                    floorCol.size = new Vector3(200f, 0.04f, 200f);
                }

                Logger.Instance.LogInfo($"{placedObject.PlayerItem.ItemType} Placed!");

                OnObjectPlaced?.Invoke();
                objectPlaced = true;

                customReticle.SetActive(false);
            }
        }
    }
}
