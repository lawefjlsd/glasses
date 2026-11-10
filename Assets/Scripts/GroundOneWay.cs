using UnityEngine;

public class GroundOneWay : MonoBehaviour
{
    public const string LayerName = "GroundOneWay";

    public enum ExitDirection
    {
        W,
        A,
        S,
        D
    }

    public string colorName;
    public ExitDirection exitDirection;
}
