using UnityEngine;

public class BlocksWorldGridManager : MonoBehaviour
{
    [SerializeField] private RectTransform parent;   // UI container under main Canvas
    [SerializeField] private GameObject worldPrefab;   // UI container under main Canvas
    [SerializeField] private int rows = 5;
    [SerializeField] private int cols = 5;

    // size and spacing of each mini-world
    [SerializeField] private Vector2 cellSize = new Vector2(300f, 300f);
    [SerializeField] private Vector2 cellSpacing = new Vector2(20f, 20f);

    private void Start()
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                // instantiate one BlocksWorld
                BlocksWorld world = Instantiate(worldPrefab, parent).GetComponent<BlocksWorld>();

                // position its root RectTransform in the grid
                RectTransform rt = world.GetComponent<RectTransform>();

                float x = c * (cellSize.x + cellSpacing.x);
                float y = -r * (cellSize.y + cellSpacing.y); // negative to go down

                rt.sizeDelta = cellSize;
                rt.anchoredPosition = new Vector2(x, y);

                // OPTIONAL: tweak per-world parameters
                // world.numberOfBlocks = 3;  // if you expose it as public or property
            }
        }
    }
}
