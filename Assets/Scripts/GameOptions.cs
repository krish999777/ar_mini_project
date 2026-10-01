using DilmerGames.Core.Singletons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOptions : Singleton<GameOptions>
{
    private bool meshVisibilityOn = true;

    [SerializeField]
    private Material meshMaterial;

    [SerializeField]
    private Niantic.ARDK.Extensions.Meshing.ARMeshManager arMeshManager;

    public void ToggleMeshVisibility(Button button)
    {
        if (arMeshManager != null)
        {
            arMeshManager.SetUseInvisibleMaterial(meshVisibilityOn);
        }

        meshVisibilityOn = !meshVisibilityOn;
        
        if (button != null)
        {
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = meshVisibilityOn ? "MESHING ON" : "MESHING OFF";
            }
        }

        if (meshMaterial != null)
        {
            meshMaterial.color = meshVisibilityOn ? new Color(meshMaterial.color.r, meshMaterial.color.g, meshMaterial.color.b, 1)
            : new Color(meshMaterial.color.r, meshMaterial.color.g, meshMaterial.color.b, 0);
        }
    }
}
