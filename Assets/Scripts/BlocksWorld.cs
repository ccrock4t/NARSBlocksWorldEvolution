using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BlocksWorld : MonoBehaviour
{
    /*
     * Ontology:
     *   On(x, y)     --- block x is on top of block y
     *   OnTable(x)   --- block x is on the table
     *   Clear(x)     --- nothing is on top of block x
     */

    [Header("Block Settings")]
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private GameObject canvas;
    [SerializeField][Range(1, 10)] private int numberOfBlocks = 3;

    // Canvas-space spacing (in pixels if using Screen Space)
    [SerializeField] private float horizontalSpacing = 150f;
    [SerializeField] private float verticalSpacing = 80f;
    [SerializeField] private float tableY = -150f;  // base Y for the "table"

    // FOL-style representation of the CURRENT world
    // On(x, y) is encoded as: on[x] = y  (y is either another block name or TABLE)
    private Dictionary<string, string> on = new Dictionary<string, string>();

    // Clear(x) is represented as a set of block names that are clear.
    private HashSet<string> clear = new HashSet<string>();

    // FOL-style representation of a GOAL world (independent, for now only printed)
    private Dictionary<string, string> goalOn = new Dictionary<string, string>();
    private HashSet<string> goalClear = new HashSet<string>();

    // Block data structure
    private class BlockData
    {
        public string name;
        public GameObject instance;
    }

    private readonly Dictionary<string, BlockData> blocks = new Dictionary<string, BlockData>();

    // Constant for table representation
    private const string TABLE = "Table";

    private void Start()
    {
        canvas = this.gameObject;
        if (blockPrefab == null)
        {
            Debug.LogError("BlocksWorldManager: blockPrefab is not assigned.");
            return;
        }

        if (canvas == null)
        {
            Debug.LogError("BlocksWorldManager: canvas is not assigned.");
            return;
        }

        InitializeBlocks();
        GenerateRandomState(on, clear);        // current world
        LayoutBlocksFromState(on);             // position GameObjects on canvas
        GenerateRandomState(goalOn, goalClear); // separate random goal
        PrintGoalState();
    }

    #region Initialization

    private void InitializeBlocks()
    {
        blocks.Clear();
        on.Clear();
        clear.Clear();

        for (int i = 0; i < numberOfBlocks; i++)
        {
            char labelChar = (char)('A' + i);
            string blockName = labelChar.ToString();

            GameObject blockGO = Instantiate(blockPrefab, Vector3.zero, Quaternion.identity, canvas.transform);
            blockGO.name = $"Block_{blockName}";
            var rt = blockGO.GetComponent<RectTransform>();
            rt.SetParent(canvas.GetComponent<RectTransform>(), false);
            rt.anchoredPosition = Vector2.zero;

            // Get the Image component and color it deterministically by letter
            var image = blockGO.GetComponent<Image>();
            if (image != null)
            {
                image.color = GetColorForBlock(blockName);
            }

            // Label via TextMeshProUGUI (preferred)
            var labelUGUI = blockGO.GetComponentInChildren<TextMeshProUGUI>();
            if (labelUGUI != null)
            {
                labelUGUI.text = blockName;
            }
            else
            {
                // fallback for TextMeshPro (world-space)
                var labelTMP = blockGO.GetComponentInChildren<TextMeshPro>();
                if (labelTMP != null)
                    labelTMP.text = blockName;
            }

            BlockData data = new BlockData
            {
                name = blockName,
                instance = blockGO
            };

            blocks[blockName] = data;
        }
    }

    private Color GetColorForBlock(string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return Color.white;

        char c = blockName[0];

        switch (c)
        {
            case 'A': return Color.red;
            case 'B': return Color.blue;
            case 'C': return Color.green;
            case 'D': return Color.yellow;
            case 'E': return new Color(1f, 0.5f, 0f); // orange
            case 'F': return Color.cyan;
            // add more if you like…
            default:
                // fallback: deterministic color based on letter index
                int idx = c - 'A';
                float hue = (idx * 0.13f) % 1f;
                return Color.HSVToRGB(hue, 0.7f, 0.9f);
        }
    }


    #endregion

    #region FOL-style state generation

    /// <summary>
    /// Generate a random valid blocks world configuration:
    /// Each block is either on the table (OnTable) or stacked on some clear block.
    /// This fills the given 'onState' (for On) and 'clearState' (for Clear) collections.
    /// </summary>
    private void GenerateRandomState(Dictionary<string, string> onState, HashSet<string> clearState)
    {
        onState.Clear();
        clearState.Clear();

        // Start with supports = { Table }
        List<string> supports = new List<string> { TABLE };

        // Random order of blocks
        List<string> blockNames = new List<string>(blocks.Keys);
        Shuffle(blockNames);

        foreach (string block in blockNames)
        {
            // Choose a random support from current supports (Table or a clear block)
            int idx = Random.Range(0, supports.Count);
            string support = supports[idx];

            // On(block, support)
            onState[block] = support;

            // If support is a block, it is no longer clear
            if (support != TABLE)
            {
                supports.Remove(support);
                clearState.Remove(support);
            }

            // The new block itself is now clear (top of its stack)
            supports.Add(block);
            clearState.Add(block);
        }
    }

    #endregion

    #region Layout (Canvas)

    /// <summary>
    /// Position the GameObjects according to the given 'onState' mapping
    /// using RectTransform.anchoredPosition on the Canvas.
    /// </summary>
    private void LayoutBlocksFromState(Dictionary<string, string> onState)
    {
        if (canvas == null) return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            Debug.LogError("BlocksWorldManager: Canvas object has no RectTransform.");
            return;
        }

        // Find all blocks directly on the table (roots of stacks)
        List<string> roots = new List<string>();
        foreach (var kvp in onState)
        {
            string block = kvp.Key;
            string support = kvp.Value;
            if (support == TABLE) // OnTable(block)
            {
                roots.Add(block);
            }
        }

        if (roots.Count == 0) return;

        // Place roots along X axis in canvas space (centered)
        float totalWidth = (roots.Count - 1) * horizontalSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < roots.Count; i++)
        {
            string root = roots[i];
            float x = startX + i * horizontalSpacing;
            Vector2 rootPos = new Vector2(x, tableY);
            SetBlockPosition(root, rootPos);

            // Recursively place any blocks above this one
            PlaceStackAbove(root, onState);
        }
    }

    /// <summary>
    /// Recursively position blocks that are on top of 'supportBlock' in canvas space.
    /// </summary>
    private void PlaceStackAbove(string supportBlock, Dictionary<string, string> onState)
    {
        Vector2 supportPos = GetBlockPosition(supportBlock);
        float yTop = supportPos.y + verticalSpacing;

        // Find any block that is on top of 'supportBlock'
        foreach (var kvp in onState)
        {
            string block = kvp.Key;
            string support = kvp.Value;
            if (support == supportBlock)
            {
                // Place this block just above the support
                Vector2 childPos = new Vector2(supportPos.x, yTop);
                SetBlockPosition(block, childPos);

                // Recursively place any blocks stacked on it
                PlaceStackAbove(block, onState);
            }
        }
    }

    private void SetBlockPosition(string blockName, Vector2 anchoredPos)
    {
        if (blocks.TryGetValue(blockName, out BlockData data))
        {
            RectTransform rt = data.instance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = anchoredPos;
            }
            else
            {
                data.instance.transform.localPosition = new Vector3(anchoredPos.x, anchoredPos.y, 0f);
            }
        }
    }

    private Vector2 GetBlockPosition(string blockName)
    {
        if (blocks.TryGetValue(blockName, out BlockData data))
        {
            RectTransform rt = data.instance.GetComponent<RectTransform>();
            if (rt != null)
            {
                return rt.anchoredPosition;
            }
            return data.instance.transform.localPosition;
        }
        return Vector2.zero;
    }

    #endregion

    #region FOL helper predicates

    /// <summary>
    /// FOL predicate: On(x, y) --- block x is on top of block y in the current state.
    /// </summary>
    public bool OnPredicate(string x, string y)
    {
        return on.TryGetValue(x, out string support) && support == y;
    }

    /// <summary>
    /// FOL predicate: OnTable(x) --- block x is on the table.
    /// Implemented as On(x, Table).
    /// </summary>
    public bool OnTablePredicate(string x)
    {
        return OnPredicate(x, TABLE);
    }

    /// <summary>
    /// FOL predicate: Clear(x) --- nothing is on top of block x.
    /// </summary>
    public bool ClearPredicate(string x)
    {
        return clear.Contains(x);
    }

    /// <summary>
    /// Recompute the 'clear' set from the current 'on' mapping.
    /// Clear(x) is true iff no block has support = x.
    /// </summary>
    private void RecomputeClear()
    {
        clear.Clear();
        // Initially assume all blocks are clear
        foreach (string block in blocks.Keys)
        {
            clear.Add(block);
        }

        // Any block that appears as a support is not clear
        foreach (var kvp in on)
        {
            string support = kvp.Value;
            if (support != TABLE) // table doesn't count as a block
            {
                clear.Remove(support);
            }
        }
    }

    #endregion

    #region Actions: Stack and Unstack

    /// <summary>
    /// Stack(top, bottom): place 'top' on 'bottom'.
    /// Valid iff both blocks are Clear.
    /// </summary>
    public bool Stack(string top, string bottom)
    {
        if (top == bottom)
        {
            Debug.LogWarning("Stack invalid: cannot stack a block on itself.");
            return false;
        }

        if (!blocks.ContainsKey(top) || !blocks.ContainsKey(bottom))
        {
            Debug.LogWarning($"Stack invalid: unknown block(s) {top}, {bottom}.");
            return false;
        }

        if (!ClearPredicate(top) || !ClearPredicate(bottom))
        {
            Debug.LogWarning($"Stack invalid: both {top} and {bottom} must be Clear.");
            return false;
        }

        // Update FOL state: On(top, bottom)
        on[top] = bottom;

        // Recompute Clear and reposition blocks visually
        RecomputeClear();
        LayoutBlocksFromState(on);

        Debug.Log($"Action: Stack({top}, {bottom})");
        return true;
    }

    /// <summary>
    /// Unstack(block): take 'block' and put it on the table.
    /// Valid iff 'block' is Clear.
    /// This corresponds to: OnTable(block).
    /// </summary>
    public bool Unstack(string block)
    {
        if (!blocks.ContainsKey(block))
        {
            Debug.LogWarning($"Unstack invalid: unknown block {block}.");
            return false;
        }

        if (!ClearPredicate(block))
        {
            Debug.LogWarning($"Unstack invalid: block {block} must be Clear.");
            return false;
        }

        // OnTable(block) == On(block, Table)
        on[block] = TABLE;

        // Recompute Clear and reposition blocks visually
        RecomputeClear();
        LayoutBlocksFromState(on);

        Debug.Log($"Action: Unstack({block})  // OnTable({block})");
        return true;
    }

    #endregion

    #region Goal state printing

    private void PrintGoalState()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("Random goal state (FOL): ");

        // On / OnTable for EVERY block
        foreach (var kvp in blocks)
        {
            string block = kvp.Key;

            if (!goalOn.TryGetValue(block, out string support))
            {
                // Shouldn't happen, but just in case
                continue;
            }

            if (support == TABLE)
                sb.Append($" OnTable({block})");
            else
                sb.Append($" On({block}, {support})");
        }

        // Clear info for EVERY block (either Clear or ¬Clear)
        foreach (var kvp in blocks)
        {
            string block = kvp.Key;

            if (goalClear.Contains(block))
                sb.Append($" Clear({block})");
            else
                sb.Append($" ¬Clear({block})");
        }

        Debug.Log(sb.ToString());
    }



    #endregion

    #region Utility

    /// <summary>
    /// Fisher–Yates shuffle for lists.
    /// </summary>
    private void Shuffle<T>(IList<T> list)
    {
        int n = list.Count;
        for (int i = 0; i < n - 1; i++)
        {
            int j = Random.Range(i, n);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    #endregion
}
