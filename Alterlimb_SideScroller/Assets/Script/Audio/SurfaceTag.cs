using UnityEngine;

public enum SurfaceType
{
    Terre,
    Metal,
    Pierre,
    Herbe
}

public class SurfaceTag : MonoBehaviour
{
    [Header("Surface")]
    public SurfaceType surface = SurfaceType.Terre;
}